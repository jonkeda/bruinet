using Bruinet.Core.Calls;
using Bruinet.Core.Configuration;
using Bruinet.Core.Diagnostics;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Logging;
using Bruinet.Core.Scopes;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Microsoft.Playwright;
using static Bruinet.Html.Playwright.PlaywrightErrors;

namespace Bruinet.Html.Playwright;

public sealed class PlaywrightTestContext : IHtmlTestContext, IHtmlPageProbe, Bruinet.Core.Interfaces.IScreenshotSource, IAsyncDisposable, Bruinet.Core.Recording.IRecordable
{
    private readonly IPage _page;
    private readonly IFrame? _frame;
    private readonly PlaywrightHtmlDriver _driver;
    private readonly bool _ownsLifecycle;

    // The browser is the process's (SharedBrowsers): disposing closes this context's browser context only.
    private bool _sharedBrowser;
    private readonly TimeoutSettings _timeouts;
    private readonly ITestLogger _logger;
    private readonly HtmlTestContextOptions _options;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private bool _disposed;

    private PlaywrightTestContext(
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext browserContext,
        IPage page,
        HtmlTestContextOptions options)
    {
        _options = options;
        _playwright = playwright;
        _browser = browser;
        _browserContext = browserContext;
        _page = page;
        _driver = new PlaywrightHtmlDriver(page);
        _timeouts = options.Timeouts;
        _logger = options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance;
        _ownsLifecycle = true;

        // The browser's console errors and warnings, and its uncaught errors, kept by the step that caused them (StepLog).
        // Raised on Playwright's own loop, which carries whatever scope it started in: elsewhere, so the latest step
        // files them. Unsubscribed with the page, which ends with this context.
        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                StepLog.Add(message.Type == "error" ? StepLogLevel.Error : StepLogLevel.Warning, "browser", message.Text, elsewhere: true);
            }
        };
        page.PageError += (_, error) => StepLog.Add(StepLogLevel.Error, "browser", error, elsewhere: true);
    }

    private PlaywrightTestContext(
        IPage page,
        IFrame? frame,
        HtmlTestContextOptions options,
        ITestLogger logger,
        bool ownsLifecycle)
    {
        _options = options;
        _page = page;
        _frame = frame;
        _driver = new PlaywrightHtmlDriver(page, frame);
        _timeouts = options.Timeouts;
        _logger = logger;
        _ownsLifecycle = ownsLifecycle;
    }

    public static async Task<PlaywrightTestContext> CreateAsync(HtmlTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Someone is watching (BRUINET_AUT_WINDOW=shown, the Presenter): a window to see, opened
        // where it will be placed, and a page that fills it.
        var shown = AutWindow.IsShown();
        var shared = options.ShareBrowser && !shown;
        var (playwright, browser) = shared
            ? await SharedBrowsers.GetAsync(options.BrowserType, options.Headless).ConfigureAwait(false)
            : await LaunchAsync(options.BrowserType, options.Headless && !shown, shown).ConfigureAwait(false);

        var browserContext = await browser.NewContextAsync(shown ? new BrowserNewContextOptions { ViewportSize = ViewportSize.NoViewport } : null).ConfigureAwait(false);
        var page = await browserContext.NewPageAsync().ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            await page.GotoAsync(options.BaseUrl).ConfigureAwait(false);
        }

        return new PlaywrightTestContext(playwright, browser, browserContext, page, options) { _sharedBrowser = shared };
    }

    // A browser of its own: Playwright, the type, the launch.
    private static async Task<(IPlaywright Playwright, IBrowser Browser)> LaunchAsync(string type, bool headless, bool shown)
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
        var browserType = type.ToLowerInvariant() switch
        {
            "firefox" => playwright.Firefox,
            "webkit" => playwright.Webkit,
            _ => playwright.Chromium
        };

        var launchOptions = new BrowserTypeLaunchOptions { Headless = headless };
        if (shown && AutWindow.Bounds() is { } bounds && ReferenceEquals(browserType, playwright.Chromium))
        {
            launchOptions.Args = [$"--window-position={bounds.X},{bounds.Y}", $"--window-size={bounds.Width},{bounds.Height}"];
        }

        return (playwright, await browserType.LaunchAsync(launchOptions).ConfigureAwait(false));
    }

    // The process's shared browsers (HtmlTestContextOptions.ShareBrowser), one per type and headless mode, launched on first
    // use and again when one has gone (crashed or closed). Never closed here: Playwright's driver is a child of this process
    // and ends with it. Closing them from ProcessExit blocked there, so a suite's own exit handlers (WebAppHost stopping the
    // app it started) never ran: the app outlived the run, held its bin, and the next build and run hung.
    private static class SharedBrowsers
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly Dictionary<(string Type, bool Headless), (IPlaywright Playwright, IBrowser Browser)> Open = [];

        public static async Task<(IPlaywright Playwright, IBrowser Browser)> GetAsync(string type, bool headless)
        {
            var key = (type.ToLowerInvariant(), headless);
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Open.TryGetValue(key, out var open) && open.Browser.IsConnected)
                {
                    return open;
                }

                return Open[key] = await LaunchAsync(type, headless, shown: false).ConfigureAwait(false);
            }
            finally
            {
                Gate.Release();
            }
        }
    }

    public static PlaywrightTestContext ForPage(IPage page, HtmlTestContextOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        options ??= new HtmlTestContextOptions();

        return new PlaywrightTestContext(
            page,
            frame: null,
            options,
            options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance,
            ownsLifecycle: false);
    }

    public IHtmlTestContext Context => this;

    public IHtmlDriver Driver => _driver;

    public string? Name => _options.Name;

    public NearMissSettings NearMiss => _options.NearMiss;

    public TimeoutSettings Timeouts => _timeouts;

    public ITestLogger Logger => _logger;

    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.Css;

    public IHtmlPage? Page => null;

    public string CurrentUrl => _driver.CurrentUrl;

    public string PageTitle => _driver.PageTitle;

    public IPage InternalPage => _page;

    /// <summary>The browser page; null once it is closed.</summary>
    public Bruinet.Core.Recording.RecordingTarget? RecordingTarget
        => _page.IsClosed ? null : new Bruinet.Core.Recording.RecordingTarget.BrowserPage(_page);

    public IFrame? InternalFrame => _frame;

    /// <summary>Ready while the page is open; a closed page is gone, not "not ready yet" (R0).</summary>
    public ScopeReadiness ProbeReadiness()
        => _page.IsClosed
            ? throw new AppUnavailableException("the browser page has closed.")
            : ScopeReadiness.Ready(nameof(PlaywrightTestContext));

    /// <summary>The page is either open or gone; there is nothing to wait for.</summary>
    public bool WaitReady(int? timeoutMs = null) => ProbeReadiness().IsReady;

    public IHtmlElement? TryFindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return _driver.FindElements(locator).FirstOrDefault();
    }

    // One attempt (R2): the call around it is what waits.
    public IHtmlElement FindElement(Locator locator)
        => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        return _driver.FindElements(locator);
    }

    public void NavigateTo(string destination)
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(NavigateTo));
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        _driver.GoTo(destination);
    }

    public async Task NavigateToAsync(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        await _page.GotoAsync(destination).ConfigureAwait(false);
    }

    public void NavigateBack()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(NavigateBack));
        _driver.GoBack();
    }

    public void GoForward() => _driver.GoForward();

    public bool IsIdle() => _driver.IsIdle();

    public PageProbe ProbePage(string busyAttribute) => _driver.ProbePage(busyAttribute);

    public PageProbe ProbePageWhenReady(string busyAttribute, Bruinet.Html.Scopes.BusySignal signal, int timeoutMs)
        => _driver.ProbePageWhenReady(busyAttribute, signal, timeoutMs);

    public void Refresh()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(Refresh));
        _driver.Reload();
    }

    public byte[] TakeScreenshot() => _driver.TakeScreenshot();

    public void SaveScreenshot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _driver.SaveScreenshot(path);
    }

    private Bruinet.Core.Services.ScreenshotService? _screenshots;

    /// <summary>
    /// This page's screenshots, PNGs in the run's screenshots folder; what a failing UAT scenario
    /// leaves as its evidence when its fixture holds this context.
    /// </summary>
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
    /// The app as a new visitor sees it: the browser context's cookies, and the current origin's
    /// <c>localStorage</c> and <c>sessionStorage</c> (a tab a page kept). The browser context
    /// itself stays, with its HTTP cache: a new one per scenario loaded every script, style and
    /// font cold and made the run a fifth slower. A Blazor circuit ends with the next page load.
    /// </summary>
    public void ResetAppState()
    {
        using var span = Bruinet.Core.Logging.Timing.Span("navigate", nameof(ResetAppState));
        (_browserContext ?? _page.Context).ClearCookiesAsync().GetAwaiter().GetResult();
        if (!_page.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Live(() => _page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }").GetAwaiter().GetResult());
        }
        catch (StaleElementException)
        {
            // The page was navigating: clear what the document it lands on can reach.
            _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded).GetAwaiter().GetResult();
            Live(() => _page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }").GetAwaiter().GetResult());
        }
    }

    /// <summary>
    /// A call log in the folder <c>BRUINET_CALL_LOG</c> names, when it is set: one CSV file per
    /// context, with every call's entry and exit, near-miss warnings included. Only when the
    /// options give no logger of their own. The same switch as MAUI's.
    /// </summary>
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : new CsvTestLogger(Path.Combine(folder,
                $"calls-html-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
    }

    public void Dispose()
    {
        DisposeAsyncCore(isAsync: false).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore(isAsync: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task DisposeAsyncCore(bool isAsync)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (!_ownsLifecycle)
        {
            return;
        }

        if (_browserContext != null)
        {
            if (isAsync)
            {
                await _browserContext.CloseAsync().ConfigureAwait(false);
            }
            else
            {
                _browserContext.CloseAsync().GetAwaiter().GetResult();
            }

            _browserContext = null;
        }

        if (_sharedBrowser)
        {
            _browser = null;
            _playwright = null;
        }

        if (_browser != null)
        {
            if (isAsync)
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            else
            {
                _browser.CloseAsync().GetAwaiter().GetResult();
            }

            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        _logger.Flush();
        _logger.Dispose();
    }
}
