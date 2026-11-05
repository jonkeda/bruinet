using Bruinet.Maui.Appium;

namespace Bruinet.Maui.Tests;

/// <summary>How the culture name is read from a <c>mobile: deviceInfo</c> answer.</summary>
public class AppiumDeviceCultureTests
{
    [Fact]
    public void Android_ReadsLocale()
    {
        var info = new Dictionary<string, object> { ["locale"] = "nl_NL" };

        Assert.Equal("nl-NL", AppiumMauiDriver.DeviceLocaleName(info));
    }

    [Fact]
    public void Ios_ReadsCurrentLocale_WithoutTheRegionOverride()
    {
        var info = new Dictionary<string, object> { ["currentLocale"] = "en_US@rg=nlzzzz" };

        Assert.Equal("en-US", AppiumMauiDriver.DeviceLocaleName(info));
    }

    [Fact]
    public void NoLocale_IsNull()
    {
        Assert.Null(AppiumMauiDriver.DeviceLocaleName(new Dictionary<string, object>()));
    }
}
