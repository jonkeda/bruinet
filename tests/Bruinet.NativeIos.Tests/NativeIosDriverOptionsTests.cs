namespace Bruinet.NativeIos.Tests;

public sealed class NativeIosDriverOptionsTests
{
    [Fact]
    public void AdditionalCapabilities_Is_Case_Insensitive()
    {
        var options = new NativeIosTestContextOptions();

        options.AdditionalCapabilities["wdaLaunchTimeout"] = 60000;
        options.AdditionalCapabilities["WDALAUNCHTIMEOUT"] = 120000;

        Assert.Single(options.AdditionalCapabilities);
        Assert.Equal(120000, options.AdditionalCapabilities["wdaLaunchTimeout"]);
    }

    [Fact]
    public void FromEnvironment_ReadsEachVariable()
    {
        var variables = new Dictionary<string, string>
        {
            ["APPIUM_SERVER_URI"] = "http://10.0.0.5:4724",
            ["APPIUM_APP_PATH"] = "/apps/Gallery.app",
            ["APPIUM_BUNDLE_ID"] = "com.bruinet.gallery",
            ["APPIUM_UDID"] = "A4395365-6928-480E-BE73-4C04C175420F",
            ["APPIUM_PLATFORM_VERSION"] = "27.0",
            ["APPIUM_DEVICE_NAME"] = "iPhone 17 Pro",
            ["APPIUM_AUTOMATION_NAME"] = "XCUITest2",
            ["APPIUM_AUTO_ACCEPT_ALERTS"] = "true",
            ["APPIUM_NO_RESET"] = "1",
            ["APPIUM_FULL_RESET"] = "true",
        };

        var options = NativeIosTestContextOptions.FromEnvironment(name => variables.GetValueOrDefault(name));

        Assert.Equal(new Uri("http://10.0.0.5:4724"), options.AppiumServerUri);
        Assert.Equal("/apps/Gallery.app", options.AppPath);
        Assert.Equal("com.bruinet.gallery", options.BundleId);
        Assert.Equal("A4395365-6928-480E-BE73-4C04C175420F", options.Udid);
        Assert.Equal("27.0", options.PlatformVersion);
        Assert.Equal("iPhone 17 Pro", options.DeviceName);
        Assert.Equal("XCUITest2", options.AutomationName);
        Assert.True(options.AutoAcceptAlerts);
        Assert.True(options.NoReset);
        Assert.True(options.FullReset);
    }

    [Fact]
    public void FromEnvironment_KeepsTheDefaults_ForUnsetVariables()
    {
        var options = NativeIosTestContextOptions.FromEnvironment(_ => null);

        Assert.Equal(new Uri("http://127.0.0.1:4723"), options.AppiumServerUri);
        Assert.Equal("iPhone 17", options.DeviceName);
        Assert.Equal("XCUITest", options.AutomationName);
        Assert.False(options.AutoAcceptAlerts);
        Assert.Null(options.Udid);
    }

    [Fact]
    public void FromEnvironment_RejectsAFlagItCannotRead()
    {
        var error = Assert.Throws<FormatException>(() => NativeIosTestContextOptions.FromEnvironment(name => name == "APPIUM_NO_RESET" ? "maybe" : null));

        Assert.Contains("APPIUM_NO_RESET", error.Message);
    }
}
