using Bruinet.WinForms.FlaUI;

namespace Bruinet.WinForms.Context;

/// <summary>
/// WinForms test context implementation using FlaUI driver.
/// </summary>
public class WinFormsTestContext : IWinFormsTestContext, IDisposable, Bruinet.Core.Recording.IRecordable
{
    private readonly IWinFormsDriver _driver;
    private readonly TimeoutSettings _timeouts;
    private readonly ITestLogger _logger;
    private readonly bool _ownsDriver;
    private bool _disposed;

    /// <summary>
    /// Creates a new WinForms test context with the specified options.
    /// </summary>
    public WinFormsTestContext(WinFormsTestContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _timeouts = options.Timeouts ?? TimeoutSettings.Default;
        _logger = options.Logger ?? NullTestLogger.Instance;

        if (options.Driver != null)
        {
            _driver = options.Driver;
            _ownsDriver = false;
        }
        else if (options.WindowHandle is { } handle && handle != IntPtr.Zero)
        {
            _driver = new FlaUIWinFormsDriver(handle);
            _ownsDriver = true;
        }
        else if (options.ProcessId is { } pid)
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            _driver = new FlaUIWinFormsDriver(process);
            _ownsDriver = true;
        }
        else if (!string.IsNullOrEmpty(options.ExecutablePath))
        {
            _driver = new FlaUIWinFormsDriver(options.ExecutablePath, options.Arguments);
            _ownsDriver = true;
        }
        else
        {
            throw new ArgumentException(
                "Either Driver, WindowHandle, ProcessId, or ExecutablePath must be provided.",
                nameof(options));
        }
    }

    /// <inheritdoc />
    public IWinFormsDriver Driver => _driver;

    /// <inheritdoc />
    public IWinFormsTestContext Context => this;

    /// <inheritdoc />
    public TimeoutSettings Timeouts => _timeouts;

    /// <inheritdoc />
    public ITestLogger Logger => _logger;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    /// <inheritdoc />
    public IPageObject? Page => null;

    /// <summary>The app's window.</summary>
    public Bruinet.Core.Recording.RecordingTarget? RecordingTarget
        => _disposed ? null : Bruinet.Core.Recording.RecordingTarget.ForWindow((_driver as FlaUIWinFormsDriver)?.CurrentWindowHandle);

    /// <inheritdoc />
    public bool IsReady(int? timeoutMs = null) => !_disposed;

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => !_disposed;

    /// <inheritdoc />
    public IWinFormsElement? TryFindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        try
        {
            var elements = _driver.FindElements(locator);
            return elements.Count > 0 ? elements[0] : null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public IWinFormsElement FindElement(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);

        var timeout = TimeSpan.FromMilliseconds(_timeouts.ElementFind);
        var pollInterval = 100;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            var elements = _driver.FindElements(locator);
            if (elements.Count > 0)
                return elements[0];
            Bruinet.Core.Utilities.WaitHelper.Pause(pollInterval);
        }

        throw new ElementNotFoundException(
            $"Element not found with locator: {locator} after {_timeouts.ElementFind}ms");
    }

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator)
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
        File.WriteAllBytes(path, screenshot);
    }

    /// <inheritdoc />
    public void ResetAppState()
    {
        _driver.ResetAppState();
    }

    /// <inheritdoc />
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

        if (disposing && _ownsDriver && _driver is IDisposable disposable)
        {
            try
            { disposable.Dispose(); }
            catch { /* Ignore errors during cleanup */ }
        }

        _disposed = true;
    }
}
