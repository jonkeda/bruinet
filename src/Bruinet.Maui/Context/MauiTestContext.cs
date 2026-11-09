using Bruinet.Core.Utilities;
using Bruinet.Maui.Configuration;

namespace Bruinet.Maui.Context;

/// <summary>
/// MAUI test context implementation supporting multiple drivers (Appium, FlaUI).
/// Uses MauiDriverFactory to create the appropriate driver for the platform.
/// </summary>
public class MauiTestContext : IMauiTestContext, Bruinet.Core.Recording.IRecordable
{
    private readonly Interfaces.IMauiDriver _driver;
    private readonly TimeoutSettings _timeouts;
    private readonly ITestLogger _logger;
    private readonly NearMissSettings _nearMiss;

    /// <summary>The call log this context opened itself (<c>BRUINET_CALL_LOG</c>), disposed with it.</summary>
    private readonly CsvTestLogger? _callLog;
    private readonly MauiPlatform _platform;
    private readonly bool _ownsDriver;
    private bool _disposed;

    /// <summary>
    /// Creates a new MAUI test context with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the context.</param>
    public MauiTestContext(MauiTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Name = options.Name;
        _timeouts = options.Timeouts ?? TimeoutSettings.Default;
        _nearMiss = options.NearMiss ?? NearMissSettings.Default;
        _callLog = options.Logger == null ? OpenCallLog() : null;
        _logger = options.Logger ?? _callLog ?? (ITestLogger)NullTestLogger.Instance;

        if (options.Driver != null)
        {
            _driver = options.Driver;
            _platform = _driver.Platform;
            _ownsDriver = options.OwnsDriver; // The caller owns an injected driver, unless it says otherwise
        }
        else
        {
            ArgumentNullException.ThrowIfNull(options.DriverOptions, nameof(options.DriverOptions));

            var driverOptions = options.DriverOptions;
            driverOptions.Timeouts ??= options.Timeouts;
            driverOptions.Logger ??= options.Logger;

            _driver = MauiDriverFactory.Create(driverOptions);
            _platform = driverOptions.Platform;
            _ownsDriver = true; // We own factory-created driver
        }
    }

    /// <inheritdoc />
    public string? Name { get; }

    /// <inheritdoc />
    public Interfaces.IMauiDriver Driver => _driver;

    /// <inheritdoc />
    public IMauiTestContext Context => this;

    /// <inheritdoc />
    public MauiPlatform Platform => _platform;

    /// <summary>What the driver says it shows (the device, for Appium); else the app's window on Windows.</summary>
    public Bruinet.Core.Recording.RecordingTarget? RecordingTarget
        => _disposed ? null
            : _driver is Bruinet.Core.Recording.IRecordable recordable ? recordable.RecordingTarget
            : _platform == MauiPlatform.Windows ? Bruinet.Core.Recording.RecordingTarget.ForWindow(_driver.CurrentWindowHandle)
            : null;

    /// <inheritdoc />
    public IMauiElement AppElement => _driver.AppElement;

    /// <inheritdoc />
    public ShellChromeLocators ShellChrome => _driver.ShellChrome;

    /// <inheritdoc />
    public TimeoutSettings Timeouts => _timeouts;

    /// <inheritdoc />
    public ITestLogger Logger => _logger;

    /// <inheritdoc />
    public NearMissSettings NearMiss => _nearMiss;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    /// <inheritdoc />
    /// <remarks>
    /// Test context root scope has no associated page.
    /// </remarks>
    public IMauiPage? Page => null;

    /// <inheritdoc />
    /// <remarks>
    /// Ready while the session is open: the context is a lookup service, not a scope with content.
    /// </remarks>
    public ScopeReadiness ProbeReadiness()
        => _disposed
            ? new ScopeReadiness(nameof(MauiTestContext), ScopeReadinessState.MissingRoot, "the test context is disposed")
            : ScopeReadiness.Ready(nameof(MauiTestContext));

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new Bruinet.Maui.Pages.AppRoot(this).WaitFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    /// <remarks>
    /// Test context root is always ready (driver is connected).
    /// </remarks>
    public bool WaitReady(int? timeoutMs = null) => !_disposed;

    /// <inheritdoc />
    /// <remarks>Null means nothing matches now; a driver error propagates, never reads as absent.</remarks>
    public IMauiElement? TryFindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return _driver.TryFindElement(locator);
    }

    /// <inheritdoc />
    /// <remarks>One attempt, like every lookup (call model R2): waiting is the call's poll's job.</remarks>
    public IMauiElement FindElement(Locator locator)
        => TryFindElement(locator) ?? throw DescribeMiss(locator);

    /// <inheritdoc />
    public ElementNotFoundException DescribeMiss(Locator locator)
        => new($"Element not found with locator: {locator}");

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return _driver.FindElements(locator);
    }

    /// <inheritdoc />
    public void NavigateTo(string destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        _logger.LogNavigation("", "", destination);
        _driver.NavigateTo(destination);
    }

    /// <inheritdoc />
    public void NavigateBack()
    {
        _driver.NavigateBack();
    }

    /// <inheritdoc />
    public void Refresh()
    {
        _driver.Refresh();
    }

    /// <inheritdoc />
    public byte[] TakeScreenshot()
    {
        return _driver.TakeScreenshot();
    }

    /// <inheritdoc />
    public void SaveScreenshot(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var screenshot = _driver.TakeScreenshot();

        // A head with no pixels (MauiHeadless) saves the tree it reads instead, as text: an empty .png is no evidence.
        if (screenshot.Length == 0)
        {
            File.WriteAllText(Bruinet.Core.Services.ScreenshotService.TreePathFor(path), _driver.GetPageSource());
            return;
        }

        File.WriteAllBytes(path, screenshot);
    }

    /// <inheritdoc />
    public void ResetAppState()
    {
        _driver.ResetAppState();
    }

    /// <summary>
    /// A call log in the folder <c>BRUINET_CALL_LOG</c> names, when it is set: one CSV file per
    /// context, with every call's entry and exit, near-miss warnings included. Off unless asked
    /// for, and only when the options give no logger of their own.
    /// </summary>
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var log = new CsvTestLogger(Path.Combine(folder,
            $"calls-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                log.Flush();
            }
            catch (ObjectDisposedException)
            {
                // Already closed with its context.
            }
        };
        return log;
    }

    /// <summary>
    /// Disposes the test context and quits the driver.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _callLog?.Dispose();
        }

        if (disposing && _ownsDriver)
        {
            try
            {
                _driver?.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        _disposed = true;
    }
}
