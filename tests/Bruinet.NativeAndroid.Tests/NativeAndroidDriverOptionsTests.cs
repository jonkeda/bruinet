namespace Bruinet.NativeAndroid.Tests;

public sealed class NativeAndroidDriverOptionsTests
{
    [Fact]
    public void AdditionalCapabilities_Is_Case_Insensitive()
    {
        var options = new NativeAndroidTestContextOptions();

        options.AdditionalCapabilities["appWaitActivity"] = ".MainActivity";
        options.AdditionalCapabilities["APPWAITACTIVITY"] = ".LoginActivity";

        Assert.Single(options.AdditionalCapabilities);
        Assert.Equal(".LoginActivity", options.AdditionalCapabilities["appWaitActivity"]);
    }

    [Fact]
    public void FromEnvironment_ReadsEachVariable()
    {
        var variables = new Dictionary<string, string>
        {
            ["APPIUM_SERVER_URI"] = "http://10.0.0.5:4724",
            ["APPIUM_APP_PATH"] = @"C:\apps\gallery.apk",
            ["APPIUM_APP_PACKAGE"] = "com.bruinet.gallery",
            ["APPIUM_APP_ACTIVITY"] = ".MainActivity",
            ["APPIUM_PLATFORM_VERSION"] = "15",
            ["APPIUM_DEVICE_NAME"] = "emulator-5556",
            ["APPIUM_AUTOMATION_NAME"] = "Espresso",
            ["APPIUM_AUTO_GRANT_PERMISSIONS"] = "false",
            ["APPIUM_NO_RESET"] = "1",
            ["APPIUM_FULL_RESET"] = "true",
        };

        var options = NativeAndroidTestContextOptions.FromEnvironment(name => variables.GetValueOrDefault(name));

        Assert.Equal(new Uri("http://10.0.0.5:4724"), options.AppiumServerUri);
        Assert.Equal(@"C:\apps\gallery.apk", options.AppPath);
        Assert.Equal("com.bruinet.gallery", options.AppPackage);
        Assert.Equal(".MainActivity", options.AppActivity);
        Assert.Equal("15", options.PlatformVersion);
        Assert.Equal("emulator-5556", options.DeviceName);
        Assert.Equal("Espresso", options.AutomationName);
        Assert.False(options.AutoGrantPermissions);
        Assert.True(options.NoReset);
        Assert.True(options.FullReset);
    }

    [Fact]
    public void FromEnvironment_KeepsTheDefaults_ForUnsetVariables()
    {
        var options = NativeAndroidTestContextOptions.FromEnvironment(_ => null);

        Assert.Equal(new Uri("http://127.0.0.1:4723"), options.AppiumServerUri);
        Assert.Equal("emulator-5554", options.DeviceName);
        Assert.Equal("UiAutomator2", options.AutomationName);
        Assert.True(options.AutoGrantPermissions);
    }

    [Fact]
    public void FromEnvironment_RejectsAFlagItCannotRead()
    {
        var error = Assert.Throws<FormatException>(() => NativeAndroidTestContextOptions.FromEnvironment(name => name == "APPIUM_NO_RESET" ? "maybe" : null));

        Assert.Contains("APPIUM_NO_RESET", error.Message);
    }
}
