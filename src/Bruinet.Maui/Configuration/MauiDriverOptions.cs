namespace Bruinet.Maui.Configuration;

/// <summary>
/// Configuration options for MAUI driver creation.
/// Works with both FlaUI (Windows) and Appium (Android/iOS) drivers.
/// </summary>
public class MauiDriverOptions
{
    /// <summary>
    /// Target platform. Determines driver type:
    /// - Windows: FlaUI (always)
    /// - Android/iOS: Appium
    /// </summary>
    public MauiPlatform Platform { get; set; } = MauiPlatform.Windows;

    /// <summary>
    /// Path to application executable or package.
    /// - Windows: Path to .exe
    /// - Android: Path to .apk
    /// - iOS: Path to .app or bundle ID
    /// </summary>
    public string? AppPath { get; set; }

    /// <summary>
    /// Process name to attach to (alternative to AppPath).
    /// Windows FlaUI only. Attaches to running process.
    /// </summary>
    public string? ProcessName { get; set; }

    /// <summary>
    /// Window handle to attach to (alternative to AppPath).
    /// Windows FlaUI only.
    /// </summary>
    public IntPtr? WindowHandle { get; set; }

    /// <summary>
    /// Appium server URI. Required for Appium driver (Android/iOS).
    /// Default: http://127.0.0.1:4723
    /// </summary>
    public Uri AppiumServerUri { get; set; } = new Uri("http://127.0.0.1:4723");

    /// <summary>
    /// Device name for Android/iOS.
    /// </summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Platform version for iOS.
    /// </summary>
    public string? PlatformVersion { get; set; }

    /// <summary>
    /// Additional Appium capabilities.
    /// </summary>
    public Dictionary<string, object> AdditionalCapabilities { get; } = new();

    /// <summary>
    /// Named values handed to the app when it is launched: a database path, a backend URL, a
    /// fixed clock - whatever the app under test reads at startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Windows:</b> environment variables of the launched process only. The test process's own
    /// environment is untouched, so two fixtures can launch two apps with different settings.
    /// </para>
    /// <para>
    /// <b>Android:</b> string extras on the launch intent (<c>--es name value</c>, through
    /// UiAutomator2's <c>optionalIntentArguments</c>). The app reads them from its activity's
    /// intent. A value may not contain whitespace or quotes, because the arguments travel as one
    /// <c>am start</c> command line; such a value throws when the session is created.
    /// </para>
    /// <para>
    /// <b>iOS:</b> environment variables of the launched app, through XCUITest's
    /// <c>processArguments</c> (<c>{ env: { ... } }</c>). The app reads them with
    /// <c>Environment.GetEnvironmentVariable</c>.
    /// </para>
    /// </remarks>
    public Dictionary<string, string> LaunchSettings { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Where this app's window goes on Windows (<c>right</c>, <c>offscreen</c>, <c>secondary</c>,
    /// <c>left-half</c>, <c>right-half</c>), for this launch only: two apps of one test can be
    /// placed side by side. If null, <c>BRUINET_AUT_PLACE</c> decides, as for every app.
    /// </summary>
    public string? Placement { get; set; }

    /// <summary>
    /// Timeout settings for waits and polling.
    /// </summary>
    public TimeoutSettings? Timeouts { get; set; }

    /// <summary>
    /// Logger for driver operations.
    /// </summary>
    public ITestLogger? Logger { get; set; }


    /// <summary>
    /// Creates MAUI driver options from configuration objects.
    /// Configuration is required; no fallback to environment variables.
    /// </summary>
    public static MauiDriverOptions FromConfiguration(MauiOptions mauiConfig)
    {
        ArgumentNullException.ThrowIfNull(mauiConfig);

        var windowHandle = !string.IsNullOrWhiteSpace(mauiConfig.WindowHandle)
            ? ParseWindowHandle(mauiConfig.WindowHandle)
            : null;

        return new MauiDriverOptions
        {
            Platform = mauiConfig.Platform,
            AppPath = mauiConfig.AppPath,
            ProcessName = mauiConfig.ProcessName,
            WindowHandle = windowHandle,
            DeviceName = mauiConfig.DeviceName,
            PlatformVersion = mauiConfig.PlatformVersion,
            AppiumServerUri = new Uri(mauiConfig.ServerUri ?? "http://127.0.0.1:4723")
        };
    }

    private static IntPtr? ParseWindowHandle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (long.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out var hexResult))
        {
            return new IntPtr(hexResult);
        }

        if (long.TryParse(normalized, out var decimalResult))
        {
            return new IntPtr(decimalResult);
        }

        return null;
    }
}
