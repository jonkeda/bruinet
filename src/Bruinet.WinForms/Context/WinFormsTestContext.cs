using Bruinet.Core.Calls;
using Bruinet.Core.Recording;
using Bruinet.Core.Scopes;
using Bruinet.WinForms.FlaUI;
using Bruinet.WinForms.Pages;

namespace Bruinet.WinForms.Context;

/// <summary>
/// One WinForms app under test, and its main window as the outermost scope: ready while the app runs.
/// A closed app is gone, not "not ready yet": the call ends with <see cref="AppUnavailableException"/> (R0).
/// </summary>
public sealed class WinFormsTestContext : IWinFormsTestContext, IRecordable
{
    private readonly WinFormsTestContextOptions _options;
    private readonly bool _ownsDriver;
    private bool _disposed;

    /// <summary>The context for the app <paramref name="options"/> names.</summary>
    /// <exception cref="ArgumentException">The options name no driver, window, process or executable.</exception>
    public WinFormsTestContext(WinFormsTestContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Timeouts = options.Timeouts ?? TimeoutSettings.Default;
        Logger = options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance;

        if (options.Driver is { } driver)
        {
            Driver = driver;
        }
        else
        {
            Driver = options switch
            {
                { WindowHandle: { } handle } when handle != IntPtr.Zero => FlaUIWinFormsDriver.Attach(handle),
                { ProcessId: { } pid } => FlaUIWinFormsDriver.Attach(Process.GetProcessById(pid)),
                { ProcessName: { Length: > 0 } name } => FlaUIWinFormsDriver.Attach(
                    Process.GetProcessesByName(name).FirstOrDefault()
                    ?? throw new InvalidOperationException($"No running process is named '{name}'.")),
                { ExecutablePath: { Length: > 0 } path } => FlaUIWinFormsDriver.Launch(path, options.Arguments, options.Environment, options.StartTimeout),
                _ => throw new ArgumentException("Either Driver, WindowHandle, ProcessId, ProcessName, or ExecutablePath must be provided.", nameof(options)),
            };
            _ownsDriver = true;
        }
    }

    /// <inheritdoc />
    public IWinFormsDriver Driver { get; }

    /// <inheritdoc />
    public string? Name => _options.Name;

    /// <inheritdoc />
    public NearMissSettings NearMiss => _options.NearMiss;

    /// <inheritdoc />
    public TimeoutSettings Timeouts { get; }

    /// <inheritdoc />
    public ITestLogger Logger { get; }

    /// <inheritdoc />
    public IWinFormsTestContext Context => this;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.AutomationId;

    /// <summary>None: the context is the app's window, not a page.</summary>
    public IWinFormsPage? Page => null;

    /// <inheritdoc />
    public RecordingTarget? RecordingTarget
        => _disposed ? null : Driver is IRecordable recordable ? recordable.RecordingTarget : RecordingTarget.ForWindow(Driver.CurrentWindowHandle);

    /// <summary>Ready while the app runs.</summary>
    /// <exception cref="AppUnavailableException">The app has exited.</exception>
    public ScopeReadiness ProbeReadiness()
        => Driver.HasExited
            ? throw new AppUnavailableException("the application process has exited.")
            : ScopeReadiness.Ready(nameof(WinFormsTestContext));

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new AppRoot(this).WaitFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => new AppRoot(this).WaitReady(timeoutMs);

    // One attempt (R2): the call around it is what waits.
    /// <inheritdoc />
    public IWinFormsElement? TryFindElement(Locator locator) => Driver.FindElements(locator).FirstOrDefault();

    /// <inheritdoc />
    public IWinFormsElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator) => Driver.FindElements(locator);

    /// <summary>A WinForms app has no destinations to go to: its page objects navigate by its own controls.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void NavigateTo(string destination)
        => throw new NotSupportedException("A WinForms app has no destination to navigate to; navigate with its page objects' controls.");

    /// <summary>A WinForms app has no history to go back in.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void NavigateBack()
        => throw new NotSupportedException("A WinForms app has no history to go back in; navigate with its page objects' controls.");

    /// <summary>A WinForms app has no page to reload.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void Refresh()
        => throw new NotSupportedException("A WinForms app has no page to reload; start a new context for a fresh app.");

    /// <inheritdoc />
    public byte[] TakeScreenshot() => Driver.TakeScreenshot();

    /// <inheritdoc />
    public void SaveScreenshot(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        File.WriteAllBytes(path, Driver.TakeScreenshot());
    }

    /// <summary>Closes the app; a new context starts it again.</summary>
    public void ResetAppState() => Driver.Quit();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsDriver)
        {
            Driver.Dispose();
        }

        Logger.Flush();
        Logger.Dispose();
    }

    /// <summary>
    /// A call log in the folder <c>BRUINET_CALL_LOG</c> names, when it is set: the same switch as
    /// the other stacks', and only when the options give no logger of their own.
    /// </summary>
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : new CsvTestLogger(Path.Combine(folder,
                $"calls-winforms-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
    }
}
