using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Tests;

/// <summary>
/// How the <c>APPIUM_*</c> variables override the settings file's <see cref="MauiDriverOptions"/>.
/// </summary>
public class MauiConfigurationEnvironmentTests
{
    [Fact]
    public void ApplyEnvironment_WithIosVariables_OverridesDeviceSettings()
    {
        var configuration = new MauiDriverOptions();
        var variables = new Dictionary<string, string?>
        {
            ["APPIUM_PLATFORM"] = "ios",
            ["APPIUM_SERVER_URI"] = "http://10.0.0.5:4724",
            ["APPIUM_APP_PATH"] = "/apps/Sample.app",
            ["APPIUM_DEVICE_NAME"] = "iPhone 17 Pro Max",
            ["APPIUM_PLATFORM_VERSION"] = "26.5",
            ["APPIUM_UDID"] = "4CECE792-F46F-4C3D-AB6E-82E46862FC2D",
        };

        configuration.ApplyEnvironment(name => variables.GetValueOrDefault(name));

        Assert.Equal(MauiPlatform.iOS, configuration.Platform);
        Assert.Equal(new Uri("http://10.0.0.5:4724"), configuration.AppiumServerUri);
        Assert.Equal("/apps/Sample.app", configuration.AppPath);
        Assert.Equal("iPhone 17 Pro Max", configuration.DeviceName);
        Assert.Equal("26.5", configuration.PlatformVersion);
        Assert.Equal("4CECE792-F46F-4C3D-AB6E-82E46862FC2D", configuration.Udid);
    }

    [Fact]
    public void ApplyEnvironment_WithUnsetOrBlankVariables_KeepsFileValues()
    {
        var configuration = new MauiDriverOptions();
        configuration.DeviceName = "emulator-5556";
        configuration.AppPath = "app.apk";

        configuration.ApplyEnvironment(name => name == "APPIUM_DEVICE_NAME" ? "  " : null);

        Assert.Equal(MauiPlatform.Windows, configuration.Platform);
        Assert.Equal("emulator-5556", configuration.DeviceName);
        Assert.Equal("app.apk", configuration.AppPath);
        Assert.Null(configuration.Udid);
    }

    [Fact]
    public void ApplyEnvironment_WithRelativeServerUri_Throws()
    {
        var configuration = new MauiDriverOptions();

        Assert.Throws<InvalidOperationException>(
            () => configuration.ApplyEnvironment(name => name == "APPIUM_SERVER_URI" ? "localhost" : null));
    }
}
