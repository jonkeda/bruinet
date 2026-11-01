using Bruinet.Core.Configuration;
using Bruinet.Core.Artifacts;
using Bruinet.Core.Testing;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Testing;

/// <summary>
/// Base fixture for HTML UI tests.
/// Configuration is loaded from bruinet.html.config.json
/// </summary>
public abstract class HtmlTestFixtureBase
{
    private IHtmlTestContext? _context;

    /// <summary>
    /// Current HTML configuration loaded from bruinet.html.config.json
    /// </summary>
    protected BruinetHtmlConfiguration Configuration { get; } = BruinetHtmlConfiguration.Load();

    protected IHtmlTestContext Context => _context
        ?? throw new InvalidOperationException("Context not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Allows per-test configuration overrides.
    /// </summary>
    protected void SetupWith(Action<BruinetHtmlConfiguration> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        configureAction(Configuration);
    }

    protected virtual HtmlTestContextOptions CreateOptions() => new()
    {
        BaseUrl = Configuration?.Html?.AppUrl,
        Headless = Configuration?.Browser?.Headless ?? true,
        BrowserType = Configuration?.Browser?.BrowserType ?? "chromium"
    };

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
            : await CreateContextAsync(CreateOptions()).ConfigureAwait(false);
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
        var options = CreateOptions();
        var baseUrl = options.BaseUrl?.TrimEnd('/') ?? string.Empty;
        Context.NavigateTo($"{baseUrl}{path}");
    }

    protected virtual ITestArtifactPathProvider GetArtifactPathProvider()
    {
        return DefaultTestArtifactPathProvider.Create(Configuration.Artifacts, GetType().Assembly.GetName().Name);
    }
}
