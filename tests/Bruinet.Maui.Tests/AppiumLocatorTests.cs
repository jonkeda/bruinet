using Bruinet.Maui.Appium;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Tests;

/// <summary>How Bruinet locators become Appium selectors.</summary>
public class AppiumLocatorTests
{
    [Fact]
    public void ByName_OnAndroid_MatchesTheAccessibleName_NotACssName()
    {
        var by = Locator.ByName("Delete").ToBy(MauiPlatform.Android);

        Assert.Equal("By.XPath: //*[@content-desc='Delete' or @text='Delete']", by.ToString());
    }

    [Fact]
    public void ByName_OnAndroid_QuotesANameWithAnApostrophe()
    {
        var by = Locator.ByName("Don't save").ToBy(MauiPlatform.Android);

        Assert.Equal("By.XPath: //*[@content-desc=\"Don't save\" or @text=\"Don't save\"]", by.ToString());
    }

    [Fact]
    public void ByName_OnIos_StaysAName()
        => Assert.StartsWith("By.Name", Locator.ByName("Delete").ToBy(MauiPlatform.iOS).ToString(), StringComparison.Ordinal);

    [Fact]
    public void AnAutomationId_OnAndroid_IsAUiSelector_NotCssOrTheIdStrategy()
    {
        // By.Id goes out as the CSS selector "#TodoRow_Due", which UiAutomator2 lets escape the
        // element it is searched from; the id strategy returns nested matches inner first.
        var by = Locator.ByAutomationId("TodoRow_Due").ToChildBy(MauiPlatform.Android);

        Assert.Contains(
            "new UiSelector().resourceIdMatches(\".*:id/TodoRow_Due\")", by.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AListItem_UnderAnElement_OnAndroid_IsRelativeAndMayStartAtTheList()
    {
        var by = Locator.ByControlType("ListItem").ToChildBy(MauiPlatform.Android);

        Assert.StartsWith("./descendant-or-self::*[", by.Criteria, StringComparison.Ordinal);
    }

    [Fact]
    public void UnderAnElement_OnAndroid_ANameIsRelative()
    {
        var by = Locator.ByName("Delete").ToChildBy(MauiPlatform.Android);

        Assert.Equal("By.XPath: .//*[@content-desc='Delete' or @text='Delete']", by.ToString());
    }

    [Fact]
    public void UnderAnElement_OnIos_IsTheSameAsAtTheTop()
        => Assert.Equal(
            Locator.ByAutomationId("Row").ToBy(MauiPlatform.iOS).ToString(),
            Locator.ByAutomationId("Row").ToChildBy(MauiPlatform.iOS).ToString());

    [Fact]
    public void AControlTypeAndName_OnIos_MatchesTheTypeAndItsName()
    {
        var by = new Locator("Button", "Ok").ToBy(MauiPlatform.iOS);

        Assert.Contains("type == 'XCUIElementTypeButton' AND (name == 'Ok' OR label == 'Ok')", by.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AControlTypeAndName_UnderAnElement_OnAndroid_IsRelative()
    {
        var by = new Locator("Button", "Ok").ToChildBy(MauiPlatform.Android);

        Assert.Equal(".//android.widget.Button[@content-desc='Ok' or @text='Ok']", by.Criteria);
    }

    [Fact]
    public void LaunchExtras_ReadsTheStringExtrasBruinetWrites()
    {
        var extras = AndroidPlatform.LaunchExtras("--es MOCK_SERVER_PORT 9090 --es CONSTRUCTION_USE_MOCK_BACKEND true");

        Assert.Equal([["s", "MOCK_SERVER_PORT", "9090"], ["s", "CONSTRUCTION_USE_MOCK_BACKEND", "true"]], extras);
        Assert.Empty(AndroidPlatform.LaunchExtras(null));
    }
}
