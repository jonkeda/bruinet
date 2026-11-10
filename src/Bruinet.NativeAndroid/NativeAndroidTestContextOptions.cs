namespace Bruinet.NativeAndroid;

/// <summary>
/// Runtime settings used to create a native Android Appium session.
/// </summary>
public sealed class NativeAndroidTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    public Uri AppiumServerUri { get; set; } = new("http://127.0.0.1:4723");

    public string DeviceName { get; set; } = "emulator-5554";

    public string AutomationName { get; set; } = "UiAutomator2";

    public string? PlatformVersion { get; set; }

    public string? AppPath { get; set; }

    public string? AppPackage { get; set; }

    public string? AppActivity { get; set; }

    public bool AutoGrantPermissions { get; set; } = true;

    public bool NoReset { get; set; }

    public bool FullReset { get; set; }





    public Dictionary<string, object> AdditionalCapabilities { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads the options from the <c>APPIUM_*</c> environment variables; an unset one keeps its default.</summary>
    public static NativeAndroidTestContextOptions FromEnvironment() => FromEnvironment(Environment.GetEnvironmentVariable);

    /// <summary>Reads the options from <c>APPIUM_*</c> variables through <paramref name="read"/>.</summary>
    /// <param name="read">Returns a variable's value, or null when it is not set.</param>
    public static NativeAndroidTestContextOptions FromEnvironment(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var options = new NativeAndroidTestContextOptions();

        if (Present(read("APPIUM_SERVER_URI")) is { } server)
        {
            options.AppiumServerUri = Uri.TryCreate(server, UriKind.Absolute, out var uri)
                ? uri
                : throw new FormatException($"APPIUM_SERVER_URI '{server}' is not an absolute URI.");
        }

        options.AppPath = Present(read("APPIUM_APP_PATH")) ?? options.AppPath;
        options.AppPackage = Present(read("APPIUM_APP_PACKAGE")) ?? options.AppPackage;
        options.AppActivity = Present(read("APPIUM_APP_ACTIVITY")) ?? options.AppActivity;
        options.PlatformVersion = Present(read("APPIUM_PLATFORM_VERSION")) ?? options.PlatformVersion;
        options.DeviceName = Present(read("APPIUM_DEVICE_NAME")) ?? options.DeviceName;
        options.AutomationName = Present(read("APPIUM_AUTOMATION_NAME")) ?? options.AutomationName;
        options.AutoGrantPermissions = Flag(read, "APPIUM_AUTO_GRANT_PERMISSIONS") ?? options.AutoGrantPermissions;
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
