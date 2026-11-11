using System.Text;
using AngleSharp.Dom;
using Bruinet.Core.Calls;
using Bruinet.Core.Configuration;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Logging;
using Bruinet.Core.Scopes;
using Bruinet.Html.Interfaces;
using ElementNotFoundException = Bruinet.Core.Exceptions.ElementNotFoundException;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Html.Bunit;

/// <summary>
/// The bUnit head (AD-030): the suite's root component rendered by bUnit, with the app's
/// services, behind the same <see cref="IHtmlTestContext"/> the browser head has, so page objects and
/// controls run unchanged. Navigation goes through bUnit's navigation manager; the root's router renders
/// the page.
/// </summary>
public sealed class BunitTestContext : IHtmlTestContext, IHtmlPageProbe, Bruinet.Core.Interfaces.IScreenshotSource, IAsyncDisposable
{
    private readonly BunitTestContextOptions _options;
    private readonly ITestLogger _logger;
    private readonly BunitHtmlDriver _driver;
    private readonly List<string> _back = [];
    private readonly List<string> _forward = [];

    // Every address the circuit has been at, for the page's own history.back(); _back is the test's back button.
    private readonly List<string> _history = [];
    private bool _goingBack;
    private BunitContext _bunit = null!;
    private global::Bunit.TestDoubles.BunitAuthorizationContext _authorization = null!;
    private IServiceScope? _fallbackScope;

    // Who is signed in, as a browser's cookie keeps it across page loads; null is signed out.
    private (string User, string[] Roles, System.Security.Claims.Claim[] Claims)? _signedIn;

    // The values users entered, by element, with the value attribute they were entered over (BunitHtmlElement.InputValue).
    private readonly Dictionary<string, (string? Attribute, string Value)> _entered = new(StringComparer.Ordinal);
    private IRenderedComponent<IComponent> _root = null!;

    public BunitTestContext(BunitTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _logger = options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance;
        _driver = new BunitHtmlDriver(this);
        _signedIn = options.User is { } user ? (user, [.. options.Roles], []) : null;
        Start();
    }

    public IHtmlTestContext Context => this;

    public IHtmlDriver Driver => _driver;

    public string? Name => _options.Name;

    public NearMissSettings NearMiss => _options.NearMiss;

    public TimeoutSettings Timeouts => _options.Timeouts;

    public ITestLogger Logger => _logger;

    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.Css;

    public IHtmlPage? Page => null;

    public string CurrentUrl => Navigation.Uri;

    // PageTitle renders into the head, which App.razor holds and the root does not: the last PageTitle
    // the page rendered, as a browser shows the last one, its content rendered on its own.
    public string PageTitle
    {
        get
        {
            var title = _root.FindComponents<PageTitle>().LastOrDefault()?.Instance.ChildContent;
            return title is null ? string.Empty : BunitLocators.Normalize(_bunit.Render(title).Nodes.Select(node => node.TextContent).Aggregate(string.Empty, string.Concat));
        }
    }

    /// <summary>The root's markup as it is now.</summary>
    public string Markup => _root.Markup;

    /// <summary>The services the app was rendered with, for a test that arranges through them.</summary>
    public IServiceProvider Services => _bunit.Services;

    private NavigationManager Navigation => _bunit.Services.GetRequiredService<NavigationManager>();

    public ScopeReadiness ProbeReadiness()
    {
        ThrowIfAppFailed();
        return ScopeReadiness.Ready(nameof(BunitTestContext));
    }

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new Bruinet.Html.Pages.AppRoot(this).WaitFirst(timeoutMs, outcomes);

    public bool WaitReady(int? timeoutMs = null) => ProbeReadiness().IsReady;

    public bool IsIdle() => true;

    // The page's own busy signal: the last bruinetBusy.set call its layout made, which bUnit's JS interop
    // records. A root without that script is interactive from its first render, since nothing prerenders.
    public PageProbe ProbePage(string busyAttribute)
    {
        ThrowIfAppFailed();
        var busy = _bunit.JSInterop.Invocations
            .Where(call => call.Identifier == _options.BusyScript)
            .Select(call => call.Arguments.FirstOrDefault())
            .LastOrDefault() is true;
        return new PageProbe("complete", true, busy ? "true" : "false");
    }

    /// <summary>The script a Blazor app's layout calls to say whether it is busy (<c>bruinet-busy.js</c>).</summary>
    public const string BusyScript = "bruinetBusy.set";

    public IHtmlElement? TryFindElement(Locator locator) => _driver.FindElements(locator).FirstOrDefault();

    public IHtmlElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator) => _driver.FindElements(locator);

    // Opening an address is a page load, as Playwright's GoTo is: a fresh root and scope, as a new circuit has, still signed in.
    // A link inside the page stays in its circuit (Follow).
    public void NavigateTo(string destination)
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(NavigateTo));
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        RefuseAnotherApp(destination);
        _back.Add(CurrentUrl);
        _forward.Clear();
        Restart(destination);
    }

    // bUnit renders this app's components only: an address on another host, or one that is no page (about:blank), would
    // render nothing or fail later without a word, so it is refused by name, as a browser's failed navigation is.
    private void RefuseAnotherApp(string destination)
    {
        if (!Uri.TryCreate(destination, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https"))
        {
            if (Uri.TryCreate(destination, UriKind.Relative, out _) && !destination.Contains(':', StringComparison.Ordinal))
            {
                return;
            }

            throw new PageLoadException($"Could not open {destination}: the bUnit head renders this app's pages only, by a path or an http address.");
        }

        if (Uri.TryCreate(string.IsNullOrEmpty(_options.BaseUrl) ? Navigation.BaseUri : _options.BaseUrl, UriKind.Absolute, out var home)
            && !string.Equals(target.Authority, home.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new PageLoadException(
                $"Could not open {destination}: the bUnit head renders the app at {home.GetLeftPart(UriPartial.Authority)} only; "
                + "another host's page needs the browser head.");
        }
    }

    /// <summary>A link followed inside the page: the router's navigation in the same circuit, as Blazor's script does it.</summary>
    internal void Follow(string href)
    {
        _back.Add(CurrentUrl);
        _forward.Clear();
        Go(href);
    }

    public void NavigateBack()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(NavigateBack));
        if (_back.Count == 0)
        {
            return;
        }

        _forward.Add(CurrentUrl);
        Go(Pop(_back));
    }

    public void GoForward()
    {
        if (_forward.Count == 0)
        {
            return;
        }

        _back.Add(CurrentUrl);
        Go(Pop(_forward));
    }

    public void Refresh()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(Refresh));
        Restart(CurrentUrl);
    }

    // No pixels on this head: the markup stands in, so a failure still leaves what the page showed.
    public byte[] TakeScreenshot() => Encoding.UTF8.GetBytes(Markup);

    public void SaveScreenshot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(Path.ChangeExtension(path, ".html"), TakeScreenshot());
    }

    private Bruinet.Core.Services.ScreenshotService? _screenshots;

    // A failed test's evidence, as the browser head leaves its picture: the markup, where the run keeps its screenshots.
    public Bruinet.Core.Interfaces.IScreenshotService Screenshots => _screenshots ??= new(
        SaveScreenshot,
        _logger,
        new ScreenshotSettings
        {
            OutputDirectory = Bruinet.Core.Artifacts.DefaultTestArtifactPathProvider.Create().ScreenshotsDirectory,
            CaptureOnFailure = true,
            IncludeTimestamp = true,
            Format = ScreenshotFormat.Png,
        });

    /// <summary>
    /// Signs <paramref name="user"/> in, as a suite's sign-in step does on this head (an app's sign-in page is not a
    /// component): the rendered page sees the new authentication state at once.
    /// </summary>
    /// <param name="user">The user's name, which <c>AuthorizeView</c> shows.</param>
    /// <param name="roles">The user's roles.</param>
    /// <param name="claims">Further claims the app reads, such as the user's id.</param>
    public void SignIn(string user, IEnumerable<string>? roles = null, IEnumerable<System.Security.Claims.Claim>? claims = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        string[] granted = [.. roles ?? []];
        System.Security.Claims.Claim[] extra = [.. claims ?? []];
        _signedIn = (user, granted, extra);
        _root.InvokeAsync(() =>
        {
            _authorization.SetAuthorized(user);
            _authorization.SetRoles(granted);
            _authorization.SetClaims(extra);
            Authentication.TriggerAuthenticationStateChanged(user, granted, extra, "bUnit");
        }).GetAwaiter().GetResult();
    }

    /// <summary>Signs the user out; the rendered page sees it at once.</summary>
    public void SignOut()
    {
        _signedIn = null;
        _root.InvokeAsync(() =>
        {
            _authorization.SetNotAuthorized();
            Authentication.TriggerUnauthenticationStateChanged();
        }).GetAwaiter().GetResult();
    }

    // bUnit's own provider: setting the authorization context alone does not tell a rendered page.
    private global::Bunit.TestDoubles.BunitAuthenticationStateProvider Authentication
        => (global::Bunit.TestDoubles.BunitAuthenticationStateProvider)_bunit.Services.GetRequiredService<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>();

    // As clearing a browser's cookies and storage: signed out, a fresh root.
    public void ResetAppState()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(ResetAppState));
        _back.Clear();
        _forward.Clear();
        _signedIn = null;
        Restart();
    }

    internal IEnumerable<IElement> Nodes() => _root.Nodes.OfType<IElement>();

    /// <summary>How many times the app's markup has changed: unchanged over a spell, the renderer is quiet.</summary>
    internal int RenderCount => Volatile.Read(ref _renders);

    private int _renders;

    /// <summary>Runs <paramref name="raise"/> on the renderer's dispatcher, as a circuit runs an event between renders.</summary>
    internal Task Dispatch(Func<Task> raise) => _root.InvokeAsync(raise);

    internal IReadOnlyList<IElement> Select(Locator locator)
    {
        // A lookup inside the renderer's dispatcher (an event finding its target) is in the middle of the page, not a new look at it.
        if (!_bunit.Renderer.Dispatcher.CheckAccess())
        {
            LoadAskedPage();
        }

        ThrowIfAppFailed();
        return BunitLocators.FindAll(css => _root.Nodes.QuerySelectorAll(css), locator);
    }

    // The app's newest navigation, as bUnit's navigation manager keeps it.
    private NavigationHistory? Latest => ((BunitNavigationManager)Navigation).History.FirstOrDefault();

    // The page load the app asked for with forceLoad, on the next look at the page, as a browser loads it: a fresh root and
    // scope at that address, still signed in, the page before it behind the back button. An address no component serves (a
    // Razor Page, an endpoint) is the server's, which only a browser reaches.
    private void LoadAskedPage()
    {
        if (Latest is not { Options.ForceLoad: true } load || ReferenceEquals(load, _loaded))
        {
            return;
        }

        _loaded = load;
        var path = Navigation.ToBaseRelativePath(Navigation.ToAbsoluteUri(load.Uri).AbsoluteUri);
        if (!BunitRoutes.Serves(_root, path))
        {
            _serverPage = new BrowserOnlyException("NavigateTo(forceLoad)",
                $"The page asked for a page load of /{path.Split('?', '#')[0]}, which no component serves (a Razor Page or an endpoint): only a browser loads it.");
            return;
        }

        _back.Add(_history[^1]);
        _forward.Clear();
        Restart(load.Uri);
    }

    // The forceLoad navigation last loaded, so each is loaded once.
    private NavigationHistory? _loaded;

    // A page load no component serves: every look at the page says so, until the test opens another.
    private BrowserOnlyException? _serverPage;

    // A handler that threw after its first await reaches bUnit's renderer, not the event that raised it:
    // the next look at the page reports it.
    internal void ThrowIfAppFailed()
    {
        if (_serverPage is { } serverPage)
        {
            throw serverPage;
        }

        // An event refused for a handler a newer render dropped was the call's stale element, not the app failing.
        if (_bunit.Renderer.UnhandledException is { IsCompleted: true } failed && !BunitHtmlElement.GoneHandler(failed.Result))
        {
            throw HandlerFailed("a handler", failed.Result);
        }
    }

    internal static AppUnavailableException HandlerFailed(string handler, Exception error)
        => new($"the page's {handler} threw {error.GetType().Name}: {error.Message} (in a browser the circuit ends here).", error);

    public void Dispose()
    {
        _bunit.Dispose();
        _fallbackScope?.Dispose();
        _logger.Flush();
        _logger.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _bunit.DisposeAsync().ConfigureAwait(false);
        _fallbackScope?.Dispose();
        _logger.Flush();
        _logger.Dispose();
    }

    // The call log as the browser head writes it, its file named for this head: calls-bunit-….csv.
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : new CsvTestLogger(Path.Combine(folder,
                $"calls-bunit-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
    }

    // The root's first render is at the address it opens on, as a browser's page load renders only that page: no start page
    // rendered first and its data loaded for nothing.
    private void Start(string? address = null)
    {
        _bunit = new BunitContext();
        _bunit.JSInterop.Mode = _options.JsInterop;
        _bunit.JSInterop.SetupVoid(_options.BusyScript, _ => true).SetVoidResult();
        _bunit.JSInterop.AddInvocationHandler(new HistoryBackHandler(HistoryBack));
        _options.ConfigureJs?.Invoke(_bunit.JSInterop);
        _options.ConfigureServices?.Invoke(_bunit.Services);
        if ((_fallback ?? _options.FallbackServices) is { } fallback)
        {
            // A scope per render of the root, as each circuit has its own: no scoped state outlives a reset.
            _fallbackScope = fallback.GetService<IServiceScopeFactory>()?.CreateScope();
            _bunit.Services.AddFallbackServiceProvider(new CircuitServiceGuard(_fallbackScope?.ServiceProvider ?? fallback));
        }

        _authorization = _bunit.AddAuthorization();
        if (_signedIn is { } signedIn)
        {
            _authorization.SetAuthorized(signedIn.User);
            _authorization.SetRoles(signedIn.Roles);
            _authorization.SetClaims(signedIn.Claims);
        }

        // As a Blazor Server circuit renders, so a component asking RendererInfo gets the answer it has in the app; after the
        // services, since it makes the renderer.
        _bunit.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        if ((address ?? _options.BaseUrl) is { Length: > 0 } first)
        {
            Navigation.NavigateTo(first);
        }

        var blank = address is null && _options.StartBlank && string.IsNullOrEmpty(_options.BaseUrl);
        var root = _options.RootComponent;
        _root = _bunit.Render(builder =>
        {
            if (!blank)
            {
                builder.OpenComponent(0, root);
                builder.CloseComponent();
            }
        });
        _root.OnMarkupUpdated += (_, _) => Interlocked.Increment(ref _renders);
        _history.Clear();
        _history.Add(Navigation.Uri);
        Navigation.LocationChanged += (_, e) =>
        {
            // Another page: the folds toggled on this one are gone with it.
            _toggled.Clear();

            // A page load is LoadAskedPage's; a replaced entry is gone from the browser's history, as here.
            if (_goingBack || Latest is { Options.ForceLoad: true })
            {
                return;
            }

            if (Latest is { Options.ReplaceHistoryEntry: true } && _history.Count > 0)
            {
                _history[^1] = e.Location;
            }
            else
            {
                _history.Add(e.Location);
            }
        };
    }

    /// <summary>
    /// The app started again with <paramref name="fallbackServices"/> (its new composition), in this same context, as a
    /// browser context outlives a restart of the app it shows: what holds the context (a scenario's composition, its page
    /// objects) keeps it, still signed in. It opens as a new context does, blank or on the base address; the caller
    /// disposes the old services.
    /// </summary>
    public void RestartApp(IServiceProvider fallbackServices)
    {
        ArgumentNullException.ThrowIfNull(fallbackServices);
        _fallback = fallbackServices;
        Restart();
    }

    // The app's services after RestartApp; the options' until then.
    private IServiceProvider? _fallback;

    private void Restart(string? address = null)
    {
        _bunit.Dispose();
        _fallbackScope?.Dispose();
        _entered.Clear();
        _toggled.Clear();
        _serverPage = null;
        Start(address);
    }

    /// <summary>
    /// Keeps what the user entered into the element at <paramref name="key"/> while its value attribute stays
    /// <paramref name="attribute"/>, until the next action; <paramref name="value"/> null forgets it (the render wrote the value).
    /// An element is known by its place, which cannot tell a field rendered anew from the old one, so the entry lasts only as
    /// long as reading back what was just entered needs.
    /// </summary>
    internal void Enter(string key, string? attribute, string? value)
    {
        if (value is null)
        {
            _entered.Remove(key);
        }
        else
        {
            _entered[key] = (attribute, value);
        }
    }

    // The folds (<details>) the user opened or closed, by element, as a browser's DOM keeps them: Blazor's diff leaves `open`
    // alone unless the page renders it. Until the page changes, since an element is known by its place.
    private readonly HashSet<string> _toggled = new(StringComparer.Ordinal);

    /// <summary>Opens or closes the fold at <paramref name="key"/>, as a click on its summary does.</summary>
    internal void Toggle(string key)
    {
        if (!_toggled.Remove(key))
        {
            _toggled.Add(key);
        }
    }

    /// <summary>Whether the user toggled the fold at <paramref name="key"/> since the page loaded.</summary>
    internal bool Toggled(string key) => _toggled.Contains(key);

    /// <summary>Forgets every entry: the next action may render the fields anew.</summary>
    internal void ForgetEntries() => _entered.Clear();

    /// <summary>What the user entered at <paramref name="key"/>, while no render has changed its value attribute since; else null.</summary>
    internal string? EnteredValue(string key, string? attribute)
    {
        if (!_entered.TryGetValue(key, out var entered))
        {
            return null;
        }

        if (string.Equals(entered.Attribute, attribute, StringComparison.Ordinal))
        {
            return entered.Value;
        }

        _entered.Remove(key);
        return null;
    }

    // The page's own history.back(), on the renderer's dispatcher already: the entry before in this circuit, as the
    // browser goes back without a page load; at the first entry nothing happens, as in a browser with no history.
    private void HistoryBack()
    {
        if (_history.Count < 2)
        {
            return;
        }

        _history.RemoveAt(_history.Count - 1);
        _goingBack = true;
        try
        {
            Navigation.NavigateTo(_history[^1]);
        }
        finally
        {
            _goingBack = false;
        }
    }

    // Answers history.back() by going back in the circuit; bUnit's loose mode would answer it with nothing.
    private sealed class HistoryBackHandler(Action back)
        : JSRuntimeInvocationHandlerBase<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(invocation => invocation.Identifier == "history.back", isCatchAllHandler: false)
    {
        // Answers InvokeVoidAsync, which is how a page calls it.
        public override bool IsVoidResultHandler => true;

        protected override Task<Microsoft.JSInterop.Infrastructure.IJSVoidResult> HandleAsync(JSRuntimeInvocation invocation)
        {
            var result = base.HandleAsync(invocation);
            back();
            SetResultBase(default!);
            return result;
        }
    }

    private void Go(string url)
    {
        _entered.Clear();
        _root.InvokeAsync(() => Navigation.NavigateTo(url)).GetAwaiter().GetResult();
    }

    private static string Pop(List<string> stack)
    {
        var url = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return url;
    }
}
