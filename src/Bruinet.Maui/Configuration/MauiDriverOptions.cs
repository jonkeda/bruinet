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
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
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
    [System.Text.Json.Serialization.JsonConverter(typeof(Bruinet.Core.Configuration.WindowHandleJsonConverter))]
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

    /// <summary>The device's UDID, for Appium (<c>udid</c>), when it must be pinned.</summary>
    public string? Udid { get; set; }

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
    /// Copies the named environment variables of the test process into <see cref="LaunchSettings"/>,
    /// skipping the ones that are unset or empty. A device app does not inherit the test process's
    /// environment, so a setting the fixture put there reaches the app this way.
    /// </summary>
    /// <param name="names">The environment variable names.</param>
    /// <returns>These options.</returns>
    public MauiDriverOptions ForwardEnvironment(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (var name in names)
        {
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
                LaunchSettings[name] = value;
        }

        return this;
    }

    /// <summary>
    /// Where this app's window goes on Windows (<c>right</c>, <c>offscreen</c>, <c>secondary</c>,
    /// <c>left-half</c>, <c>right-half</c>), for this launch only: two apps of one test can be
    /// placed side by side. If null, <c>BRUINET_AUT_PLACE</c> decides, as for every app.
    /// </summary>
    public string? Placement { get; set; }

    /// <summary>
    /// Timeout settings for waits and polling.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeoutSettings? Timeouts { get; internal set; }

    /// <summary>
    /// Logger for driver operations.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ITestLogger? Logger { get; internal set; }

    /// <summary>
    /// Overrides these options with the <c>APPIUM_*</c> variables that are set, so a CI job or a local
    /// script can choose the platform and device without editing the settings file:
    /// <c>APPIUM_PLATFORM</c>, <c>APPIUM_SERVER_URI</c>, <c>APPIUM_APP_PATH</c>, <c>APPIUM_DEVICE_NAME</c>,
    /// <c>APPIUM_PLATFORM_VERSION</c> and <c>APPIUM_UDID</c>. An unset or blank variable keeps the file's value.
    /// The fixture applies them after the file and before its own code.
    /// </summary>
    /// <param name="read">Returns a variable's value, or null when it is not set.</param>
    /// <exception cref="InvalidOperationException">A variable holds a value it cannot take.</exception>
    public void ApplyEnvironment(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (Present(read("APPIUM_PLATFORM")) is { } platform)
        {
            Platform = platform.ToLowerInvariant() switch
            {
                "windows" => MauiPlatform.Windows,
                "android" => MauiPlatform.Android,
                "ios" => MauiPlatform.iOS,
                _ => throw new InvalidOperationException(
                    $"APPIUM_PLATFORM has unrecognized value '{platform}'. Expected 'windows', 'android', or 'ios'."),
            };
        }

        if (Present(read("APPIUM_SERVER_URI")) is { } server)
        {
            AppiumServerUri = Uri.TryCreate(server, UriKind.Absolute, out var uri)
                ? uri
                : throw new InvalidOperationException($"APPIUM_SERVER_URI '{server}' is not an absolute URI.");
        }

        AppPath = Present(read("APPIUM_APP_PATH")) ?? AppPath;
        DeviceName = Present(read("APPIUM_DEVICE_NAME")) ?? DeviceName;
        PlatformVersion = Present(read("APPIUM_PLATFORM_VERSION")) ?? PlatformVersion;
        Udid = Present(read("APPIUM_UDID")) ?? Udid;
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
