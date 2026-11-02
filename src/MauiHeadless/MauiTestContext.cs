using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Storage;
using MauiHeadless.Hosting;
using MauiHeadless.Inspection;
using MauiHeadless.Internals;
using MauiHeadless.Rendering;
using MauiApplication = Microsoft.Maui.Controls.Application;

namespace MauiHeadless;

/// <summary>
/// One test's headless MAUI host: its services, its dispatcher and virtual clock, in-memory
/// preferences and secure storage, and the application its rendered windows belong to. bUnit's
/// <c>BunitContext</c> for MAUI.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="Render{T}(Action{RenderOptions}?)"/> puts the element in a <see cref="Window"/> of its
/// own whose parent is the context's <see cref="Application"/>. That is all MAUI needs to run its own
/// page lifecycle, navigation and Shell without a platform; no handler is created.
/// </para>
/// <para>
/// Contexts are independent and may run in parallel, with one exception:
/// <see cref="MauiApplication.Current"/> is process-wide. MauiHeadless sets it to the context's application,
/// under one lock, while it makes an element (so <c>StaticResource</c> finds the app's resources) and,
/// unless <see cref="MauiTestContextOptions.SetApplicationCurrent"/> is off, while it runs the context's
/// work (so <c>Shell.Current</c> finds the context's Shell); then it puts it back.
/// </para>
/// </remarks>
public sealed class MauiTestContext : IDisposable
{
    private static readonly object ApplicationGate = new();
    private static readonly ConditionalWeakTable<Window, MauiTestContext> Windows = new();

    private readonly List<IDisposable> _rendered = [];
    private MauiApplication? _application;
    private IMauiContext? _mauiContext;
    private bool _disposed;

    /// <summary>A context with the default options.</summary>
    public MauiTestContext()
        : this(new MauiTestContextOptions())
    {
    }

    /// <summary>A context with <paramref name="options"/>.</summary>
    public MauiTestContext(MauiTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Adapters = [.. options.Adapters];
        Dispatcher = new TestDispatcher { FollowsWallClock = options.TimeFollowsWallClock };
        if (options.SetApplicationCurrent)
            Dispatcher.OnEnter = () => _application is { } application ? new ApplicationScope(application) : null;
        TestDispatcherProvider.Install(Dispatcher);
        // App code calls MainThread directly; it runs on this context's dispatcher, as its UI thread.
        MauiInternals.SetMainThread(() => !Dispatcher.IsDispatchRequired, action => Dispatcher.Dispatch(action));

        Services = new TestServiceProvider();
        Services.AddSingleton(this);
        Services.AddSingleton<IDispatcher>(Dispatcher);
        Services.AddSingleton<IPreferences>(Preferences);
        Services.AddSingleton<ISecureStorage>(SecureStorage);
        Services.AddSingleton<Microsoft.Maui.Animations.IAnimationManager>(
            new Microsoft.Maui.Animations.AnimationManager(new HeadlessTicker()));
    }

    /// <summary>The options the context was made with.</summary>
    public MauiTestContextOptions Options { get; }

    /// <summary>
    /// The services elements are made from: register fakes and mocks here before the first render.
    /// The dispatcher, preferences and secure storage are registered already.
    /// </summary>
    public TestServiceProvider Services { get; }

    /// <summary>The dispatcher every element of this context dispatches to, and its virtual clock.</summary>
    public TestDispatcher Dispatcher { get; }

    /// <summary>The <see cref="IPreferences"/> the services give out.</summary>
    public HeadlessPreferences Preferences { get; } = new();

    /// <summary>The <see cref="ISecureStorage"/> the services give out.</summary>
    public HeadlessSecureStorage SecureStorage { get; } = new();

    /// <summary>The adapters asked before the default one.</summary>
    public IReadOnlyList<IElementAdapter> Adapters { get; }

    /// <summary>The application the rendered windows belong to, made at first use.</summary>
    public MauiApplication Application => _application ??= MakeApplication();

    private IMauiContext MauiContext => _mauiContext ??= new HeadlessMauiContext(Services.Build());

    /// <summary>Makes a <typeparamref name="T"/> from the services and renders it.</summary>
    /// <typeparam name="T">A page, or a view or layout, which is put on a page of its own.</typeparam>
    public IRenderedElement<T> Render<T>(Action<RenderOptions>? configure = null) where T : Element
        => Render(services => ActivatorUtilities.GetServiceOrCreateInstance<T>(services), configure);

    /// <summary>Makes an element with <paramref name="factory"/> and renders it.</summary>
    public IRenderedElement<T> Render<T>(Func<IServiceProvider, T> factory, Action<RenderOptions>? configure = null)
        where T : Element
    {
        ArgumentNullException.ThrowIfNull(factory);
        ObjectDisposedException.ThrowIf(_disposed, this);
        var options = new RenderOptions();
        configure?.Invoke(options);
        Services.Build();
        var application = Application;

        return Dispatcher.Run(() =>
        {
            var element = WithApplication(application, () => factory(Services));
            if (options.BindingContext is { } bindingContext)
                element.BindingContext = bindingContext;

            Page page = element switch
            {
                Page shown => shown,
                View view => new ContentPage { Content = view },
                _ => throw new ArgumentException($"A {typeof(T).Name} cannot be shown on its own: render a Page, a View or a Layout."),
            };
            HeadlessBehaviors.Strip(page);
            if (options.InNavigationPage && page is not NavigationPage and not Shell)
                page = new NavigationPage(page);
            if (page is Shell { Handler: null } shell)
                shell.Handler = new HeadlessHandler(MauiContext);

            var window = new Window(page);
            HeadlessFocus.Attach(window);
            HeadlessNavigation.Attach(window, MauiContext);
            HeadlessPopups.Attach(window, Options.DialogPages);
            Windows.Add(window, this);
            MauiInternals.AddWindow(application, window);
            if (options.Appear)
                page.SendAppearing();

            var rendered = new RenderedElement<T>(this, element, window, page);
            _rendered.Add(rendered);
            return (IRenderedElement<T>)rendered;
        });
    }

    /// <summary>Renders an element already made.</summary>
    public IRenderedElement<T> Render<T>(T element, Action<RenderOptions>? configure = null) where T : Element
    {
        ArgumentNullException.ThrowIfNull(element);
        return Render(_ => element, configure);
    }

    /// <summary>Moves the virtual clock forward: delayed work and timers due by then run, in order.</summary>
    public void AdvanceTime(TimeSpan by) => Dispatcher.AdvanceTime(by);

    /// <summary>The context that rendered <paramref name="element"/>, or null when none did.</summary>
    public static MauiTestContext? Of(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        for (Element? current = element; current is not null; current = current.Parent)
        {
            if (current is Window window && Windows.TryGetValue(window, out var context))
                return context;
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        for (var i = _rendered.Count - 1; i >= 0; i--)
            _rendered[i].Dispose();
        Services.Dispose();
    }

    private MauiApplication MakeApplication()
    {
        lock (ApplicationGate)
        {
            // Application's constructor makes itself Current; the previous one is put back.
            var previous = MauiApplication.Current;
            try
            {
                return Dispatcher.Run(() =>
                {
                    var application = Options.Application?.Invoke(Services) ?? new MauiApplication();
                    HeadlessBehaviors.Install(application, Options.BehaviorHosts);
                    application.Handler = new HeadlessHandler(MauiContext);
                    return application;
                });
            }
            finally
            {
                MauiApplication.Current = previous;
            }
        }
    }

    private static T WithApplication<T>(MauiApplication application, Func<T> make)
    {
        using var _ = new ApplicationScope(application);
        return make();
    }

    /// <summary><see cref="MauiApplication.Current"/> set to one application, under the process-wide lock, until disposed.</summary>
    private sealed class ApplicationScope : IDisposable
    {
        private readonly MauiApplication? _previous;

        public ApplicationScope(MauiApplication application)
        {
            Monitor.Enter(ApplicationGate);
            _previous = MauiApplication.Current;
            MauiApplication.Current = application;
        }

        public void Dispose()
        {
            MauiApplication.Current = _previous;
            Monitor.Exit(ApplicationGate);
        }
    }
}
