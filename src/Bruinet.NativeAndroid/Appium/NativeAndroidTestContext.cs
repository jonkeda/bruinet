using Bruinet.Core.Calls;
using Bruinet.Core.Recording;
using Bruinet.Core.Scopes;
using Bruinet.NativeAndroid.Pages;

namespace Bruinet.NativeAndroid.Appium;

/// <summary>
/// One app under test on one device, and the screen as the outermost scope: ready while the app is
/// in front, not ready while another app or a system dialog covers it.
/// </summary>
public sealed class NativeAndroidTestContext : INativeAndroidTestContext, IRecordable
{
    // Android's permission prompts and other system dialogs come from these packages.
    private static readonly string[] SystemDialogPackages =
    [
        "com.google.android.permissioncontroller",
        "com.android.permissioncontroller",
        "com.android.packageinstaller",
        "android",
        "com.android.systemui",
    ];

    private readonly NativeAndroidTestContextOptions _options;
    private bool _disposed;

    /// <summary>The context over an open session.</summary>
    public NativeAndroidTestContext(INativeAndroidDriver driver, NativeAndroidTestContextOptions? options = null)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? new NativeAndroidTestContextOptions();
        Timeouts = _options.Timeouts ?? TimeoutSettings.Default;
        Logger = _options.Logger ?? (ITestLogger?)OpenCallLog() ?? NullTestLogger.Instance;
    }

    /// <summary>Opens a session with <paramref name="options"/> and makes its context.</summary>
    public static NativeAndroidTestContext Create(NativeAndroidTestContextOptions options)
        => new(AppiumNativeAndroidDriver.Create(options), options);

    /// <inheritdoc />
    public INativeAndroidDriver Driver { get; }

    /// <inheritdoc />
    public string? Name => _options.Name;

    /// <inheritdoc />
    public NearMissSettings NearMiss => _options.NearMiss;

    /// <inheritdoc />
    public TimeoutSettings Timeouts { get; }

    /// <inheritdoc />
    public ITestLogger Logger { get; }

    /// <inheritdoc />
    public INativeAndroidTestContext Context => this;

    /// <inheritdoc />
    public LocatorStrategy DefaultLocatorStrategy => LocatorStrategy.Id;

    /// <summary>None: the context is the screen, not a page.</summary>
    public INativeAndroidPage? Page => null;

    /// <inheritdoc />
    public RecordingTarget? RecordingTarget => _disposed ? null : Driver.RecordingTarget;

    /// <summary>
    /// Ready while the app under test is in front. An ended session is gone, not "not ready yet":
    /// the driver throws <see cref="AppUnavailableException"/>, which ends the call (R0).
    /// </summary>
    public ScopeReadiness ProbeReadiness()
    {
        if (Driver.IsAppInForeground())
        {
            return ScopeReadiness.Ready(nameof(NativeAndroidTestContext));
        }

        var front = Driver.CurrentPackage;
        var detail = front is not null && SystemDialogPackages.Contains(front, StringComparer.Ordinal)
            ? $"a system dialog ({front}) covers the app"
            : $"the app '{Driver.AppPackage}' is not in front; '{front ?? "nothing"}' is";
        return new ScopeReadiness(nameof(NativeAndroidTestContext), ScopeReadinessState.NotLoaded, detail);
    }

    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new AppRoot(this).WaitFirst(timeoutMs, outcomes);

    /// <inheritdoc />
    public bool WaitReady(int? timeoutMs = null) => new AppRoot(this).WaitReady(timeoutMs);

    /// <inheritdoc />
    public INativeAndroidElement? TryFindElement(Locator locator) => Driver.FindElements(locator).FirstOrDefault();

    // One attempt (R2): the call around it is what waits.
    /// <inheritdoc />
    public INativeAndroidElement FindElement(Locator locator) => TryFindElement(locator) ?? throw new ElementNotFoundException(locator);

    /// <inheritdoc />
    public IReadOnlyList<INativeAndroidElement> FindElements(Locator locator) => Driver.FindElements(locator);

    /// <summary>Opens a deep link in the app under test.</summary>
    public void NavigateTo(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        Logger.Write(LogEvent.Navigation(string.Empty, string.Empty, destination));
        Driver.OpenDeepLink(destination);
    }

    /// <summary>Presses the system Back key.</summary>
    public void NavigateBack() => Driver.Back();

    /// <summary>Restarts the app under test: Android has no page reload.</summary>
    public void Refresh() => Driver.ResetAppState();

    /// <inheritdoc />
    public byte[] TakeScreenshot() => Driver.TakeScreenshot();

    /// <inheritdoc />
    public void SaveScreenshot(string path) => Driver.SaveScreenshot(path);

    /// <inheritdoc />
    public void ResetAppState() => Driver.ResetAppState();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Driver.Dispose();
        Logger.Flush();
        Logger.Dispose();
    }

    /// <summary>
    /// A call log in the folder <c>BRUINET_CALL_LOG</c> names, when it is set: the same switch as
    /// MAUI's and Html's, and only when the options give no logger of their own.
    /// </summary>
    private static CsvTestLogger? OpenCallLog()
    {
        var folder = Environment.GetEnvironmentVariable("BRUINET_CALL_LOG");
        return string.IsNullOrWhiteSpace(folder)
            ? null
            : new CsvTestLogger(Path.Combine(folder,
                $"calls-nativeandroid-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.csv"));
    }
}
