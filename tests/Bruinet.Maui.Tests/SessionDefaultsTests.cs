using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Tests;

/// <summary>
/// The session settings are the factory's: every suite starts with the platform's defaults, and
/// a suite's own capability overrides one.
/// </summary>
public class SessionDefaultsTests
{
    [Fact]
    public void Android_BoundsTheIdleWait_AndTurnsWindowAnimationsOff()
    {
        var capabilities = MauiDriverFactory.SessionCapabilities(new MauiDriverOptions { Platform = MauiPlatform.Android });

        Assert.Equal(100, capabilities["settings[waitForIdleTimeout]"]);
        Assert.Equal(100, capabilities["settings[waitForSelectorTimeout]"]);
        Assert.Equal(true, capabilities["disableWindowAnimation"]);
        Assert.False(capabilities.ContainsKey("waitForQuiescence"));
    }

    [Fact]
    public void iOS_TurnsQuiescenceOff_AndSkipsTheIdleWait()
    {
        var capabilities = MauiDriverFactory.SessionCapabilities(new MauiDriverOptions { Platform = MauiPlatform.iOS });

        Assert.Equal(false, capabilities["waitForQuiescence"]);
        Assert.Equal(0, capabilities["settings[waitForIdleTimeout]"]);
        Assert.False(capabilities.ContainsKey("disableWindowAnimation"));
    }

    [Fact]
    public void ASuitesOwnCapability_OverridesTheDefault()
    {
        var options = new MauiDriverOptions { Platform = MauiPlatform.Android };
        options.AdditionalCapabilities["settings[waitForIdleTimeout]"] = 250;
        options.AdditionalCapabilities["appWaitPackage"] = "com.example";

        var capabilities = MauiDriverFactory.SessionCapabilities(options);

        Assert.Equal(250, capabilities["settings[waitForIdleTimeout]"]);
        Assert.Equal("com.example", capabilities["appWaitPackage"]);
        Assert.Equal(true, capabilities["disableWindowAnimation"]);
    }

    [Fact]
    public void iOS_WithNoDeviceNamed_AsksForTheRunbooksSimulator()
        => Assert.Equal(("iPhone 17 Pro Max", "26.5"), MauiDriverFactory.IosDevice(null, null));

    [Fact]
    public void iOS_ASuitesOwnDevice_OverridesTheDefault()
        => Assert.Equal(("iPhone 16", "18.4"), MauiDriverFactory.IosDevice("iPhone 16", "18.4"));

    [Fact]
    public void Windows_HasNone()
        => Assert.Empty(MauiDriverFactory.SessionCapabilities(new MauiDriverOptions { Platform = MauiPlatform.Windows }));
}
