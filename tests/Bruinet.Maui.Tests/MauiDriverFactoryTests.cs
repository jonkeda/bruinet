using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Tests;

// What the factory says when it cannot start a driver: each message names what to set.
public sealed class MauiDriverFactoryTests
{
    [Fact]
    public void Headless_HasNoAppToLaunch_AndSaysWhereTheFixtureRendersIt()
    {
        var failure = Assert.Throws<ArgumentException>(() => MauiDriverFactory.Create(new MauiDriverOptions { Platform = MauiPlatform.Headless }));

        Assert.Contains("MauiHeadless has no app to launch", failure.Message);
    }

    [Theory]
    [InlineData(MauiPlatform.Android)]
    [InlineData(MauiPlatform.iOS)]
    public void AnAppiumPlatform_WithoutAnAppPath_SaysWhichVariableSetsIt(MauiPlatform platform)
    {
        var failure = Assert.Throws<ArgumentException>(() => MauiDriverFactory.Create(new MauiDriverOptions { Platform = platform }));

        Assert.Contains("APPIUM_APP_PATH", failure.Message);
    }

    [Fact]
    public void APlatformItDoesNotKnow_IsNamed()
    {
        var failure = Assert.Throws<ArgumentException>(() => MauiDriverFactory.Create(new MauiDriverOptions { Platform = (MauiPlatform)99 }));

        Assert.Contains("Unsupported platform: 99", failure.Message);
    }
}
