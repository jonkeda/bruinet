namespace Bruinet.NativeIos;

/// <summary>
/// Runtime settings used to create a native iOS Appium session (XCUITest).
/// </summary>
public sealed class NativeIosTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    /// <summary>The Appium server; <c>APPIUM_SERVER_URI</c>.</summary>
    public Uri AppiumServerUri { get; set; } = new("http://127.0.0.1:4723");

    /// <summary>The simulator or device name; <c>APPIUM_DEVICE_NAME</c>.</summary>
    public string DeviceName { get; set; } = "iPhone 17";

    /// <summary>The simulator or device UDID; <c>APPIUM_UDID</c>. Null lets Appium pick by name and version.</summary>
    public string? Udid { get; set; }

    /// <summary>The Appium driver; <c>APPIUM_AUTOMATION_NAME</c>.</summary>
    public string AutomationName { get; set; } = "XCUITest";

    /// <summary>The iOS version; <c>APPIUM_PLATFORM_VERSION</c>.</summary>
    public string? PlatformVersion { get; set; }

    /// <summary>The <c>.app</c> to install; <c>APPIUM_APP_PATH</c>.</summary>
    public string? AppPath { get; set; }

    /// <summary>The app's bundle id; <c>APPIUM_BUNDLE_ID</c>.</summary>
    public string? BundleId { get; set; }

    /// <summary>Accepts system alerts (permissions) as they appear; <c>APPIUM_AUTO_ACCEPT_ALERTS</c>.</summary>
    public bool AutoAcceptAlerts { get; set; }

    /// <summary>Keeps the app and its data between sessions; <c>APPIUM_NO_RESET</c>.</summary>
    public bool NoReset { get; set; }

    /// <summary>Uninstalls the app after the session; <c>APPIUM_FULL_RESET</c>.</summary>
    public bool FullReset { get; set; }

    /// <summary>More capabilities, passed to Appium as they are.</summary>
    public Dictionary<string, object> AdditionalCapabilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the options from the <c>APPIUM_*</c> environment variables; an unset one keeps its default.</summary>
    public static NativeIosTestContextOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

    /// <summary>Reads the options from <c>APPIUM_*</c> variables through <paramref name="read"/>.</summary>
    /// <param name="read">Returns a variable's value, or null when it is not set.</param>
    public static NativeIosTestContextOptions FromEnvironment(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var options = new NativeIosTestContextOptions();

        if (Present(read("APPIUM_SERVER_URI")) is { } server)
        {
            options.AppiumServerUri = Uri.TryCreate(server, UriKind.Absolute, out var uri)
                ? uri
                : throw new FormatException($"APPIUM_SERVER_URI '{server}' is not an absolute URI.");
        }

        options.AppPath = Present(read("APPIUM_APP_PATH")) ?? options.AppPath;
        options.BundleId = Present(read("APPIUM_BUNDLE_ID")) ?? options.BundleId;
        options.Udid = Present(read("APPIUM_UDID")) ?? options.Udid;
        options.PlatformVersion = Present(read("APPIUM_PLATFORM_VERSION")) ?? options.PlatformVersion;
        options.DeviceName = Present(read("APPIUM_DEVICE_NAME")) ?? options.DeviceName;
        options.AutomationName = Present(read("APPIUM_AUTOMATION_NAME")) ?? options.AutomationName;
        options.AutoAcceptAlerts = Flag(read, "APPIUM_AUTO_ACCEPT_ALERTS") ?? options.AutoAcceptAlerts;
        options.NoReset = Flag(read, "APPIUM_NO_RESET") ?? options.NoReset;
        options.FullReset = Flag(read, "APPIUM_FULL_RESET") ?? options.FullReset;
        return options;
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool? Flag(Func<string, string?> read, string name)
        => Present(read(name)) switch
        {
            null => null,
            "1" => true,
            "0" => false,
            var text when bool.TryParse(text, out var value) => value,
            var text => throw new FormatException($"{name} '{text}' is not true, false, 1 or 0."),
        };
}
