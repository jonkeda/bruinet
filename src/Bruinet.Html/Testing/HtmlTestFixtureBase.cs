using Bruinet.Core.Configuration;
using Bruinet.Core.Artifacts;
using Bruinet.Core.Testing;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Testing;

/// <summary>
/// Base fixture for HTML UI tests. Its options are loaded from <c>bruinet.html.config.json</c> beside the
/// test assembly, which holds <see cref="HtmlTestContextOptions"/>' own names; code wins over the file.
/// </summary>
public abstract class HtmlTestFixtureBase
{
    private IHtmlTestContext? _context;

    /// <summary>The settings file's name, beside the test assembly.</summary>
    public const string SettingsFile = "bruinet.html.config.json";

    /// <summary>The options as loaded from <see cref="SettingsFile"/>, before <see cref="CreateOptions"/> adjusts them.</summary>
    protected HtmlTestContextOptions Options { get; } = TestContextOptionsBase.Load<HtmlTestContextOptions>(SettingsFile);

    protected IHtmlTestContext Context => _context
        ?? throw new InvalidOperationException("Context not initialized. Ensure InitializeAsync has completed.");

    /// <summary>Changes the loaded options in code, before the context is made.</summary>
    protected void SetupWith(Action<HtmlTestContextOptions> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Options);
    }

    /// <summary>The options the context is made with: the loaded ones, which a suite may adjust.</summary>
    protected virtual HtmlTestContextOptions CreateOptions(HtmlTestContextOptions loaded) => loaded;

    protected abstract Task<IHtmlTestContext> CreateContextAsync(HtmlTestContextOptions options);

    /// <summary>
    /// The suite's bUnit head (<see cref="WebHeads"/>): its root component and services rendered without a
    /// browser, a <c>BunitTestContext</c> from <c>Bruinet.Html.Bunit</c>. A suite that has one overrides this;
    /// by default a bUnit run of the suite says it has none.
    /// </summary>
    protected virtual IHtmlTestContext CreateBunitContext()
        => throw new InvalidOperationException(
            $"{GetType().Name} has no bUnit head: override CreateBunitContext, or run it without {WebHeads.Variable}=bunit.");

    public virtual async Task InitializeAsync()
    {
        _context = WebHeads.IsBunit
            ? CreateBunitContext()
            : await CreateContextAsync(CreateOptions(Options)).ConfigureAwait(false);
    }

    public virtual async Task DisposeAsync()
    {
        if (_context is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _context?.Dispose();
        }

        _context = null;
    }

    protected void NavigateTo(string path)
    {
        var options = CreateOptions(Options);
        var baseUrl = options.BaseUrl?.TrimEnd('/') ?? string.Empty;
        Context.NavigateTo($"{baseUrl}{path}");
    }

    protected virtual ITestArtifactPathProvider GetArtifactPathProvider()
    {
        return DefaultTestArtifactPathProvider.Create(Options.Artifacts, GetType().Assembly.GetName().Name);
    }
}
