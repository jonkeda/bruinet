using OpenQA.Selenium;

namespace Bruinet.NativeIos.Tests;

public sealed class NativeIosByExtensionsTests
{
    [Theory]
    [InlineData(LocatorStrategy.XPath, "//XCUIElementTypeButton", "By.XPath")]
    [InlineData(LocatorStrategy.ClassName, "XCUIElementTypeTextField", "ByIosNSPredicate")]
    [InlineData(LocatorStrategy.Id, "save", "ByIosNSPredicate")]
    [InlineData(LocatorStrategy.Text, "Save", "ByIosNSPredicate")]
    public void ToIosBy_Maps_Supported_Locators(LocatorStrategy strategy, string value, string expectedPrefix)
    {
        var by = new Locator(strategy, value).ToIosBy();

        Assert.StartsWith(expectedPrefix, by.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// XCUITest knows the identifier as "name": a predicate on "identifier" matches nothing
    /// (measured on the iOS 27 simulator, 2026-11-16).
    /// </summary>
    [Theory]
    [InlineData(LocatorStrategy.Id)]
    [InlineData(LocatorStrategy.AutomationId)]
    public void ToPredicate_SendsAnId_AsTheName(LocatorStrategy strategy)
        => Assert.Equal("name == 'search'", new Locator(strategy, "search").ToPredicate());

    [Theory]
    [InlineData(LocatorStrategy.Css)]
    [InlineData(LocatorStrategy.LinkText)]
    public void ToIosBy_Rejects_Web_Only_Locators(LocatorStrategy strategy)
    {
        var exception = Assert.Throws<LocatorNotSupportedException>(() => new Locator(strategy, "value").ToIosBy());

        Assert.Equal("Native iOS Appium", exception.DriverName);
    }

    [Fact]
    public void ToPredicate_Text_Matches_Label_Or_Value()
        => Assert.Equal("label == 'Contacts' OR value == 'Contacts'", Locator.ByText("Contacts").ToPredicate());

    [Fact]
    public void ToPredicate_ClassName_Matches_The_Type()
        => Assert.Equal("type == 'XCUIElementTypeCell'", Locator.ByClassName("XCUIElementTypeCell").ToPredicate());

    [Fact]
    public void ToPredicate_ControlTypeAndName_Matches_Only_That_Type()
        => Assert.Equal(
            "type == 'XCUIElementTypeButton' AND (label == 'Rename' OR value == 'Rename')",
            Locator.ByControlTypeAndName("XCUIElementTypeButton", "Rename").ToPredicate());

    [Fact]
    public void ToPredicate_Escapes_Quotes_And_Backslashes()
        => Assert.Equal(@"label == 'Bob\'s \\ list' OR value == 'Bob\'s \\ list'", Locator.ByText(@"Bob's \ list").ToPredicate());

    [Fact]
    public void ToPredicate_Is_Null_For_An_XPath()
        => Assert.Null(Locator.ByXPath("//XCUIElementTypeButton").ToPredicate());

    /// <summary>
    /// XCUITest evaluates a search under an element in that element's subtree, so an XPath is sent
    /// as written (measured, 2026-11-16); UiAutomator2 needed a rewrite.
    /// </summary>
    [Theory]
    [InlineData("./XCUIElementTypeButton[1]")]
    [InlineData(".//XCUIElementTypeStaticText")]
    [InlineData("//XCUIElementTypeStaticText")]
    public void ToIosChildBy_SendsAnXPathAsWritten(string xpath)
        => Assert.Equal(xpath, Locator.ByXPath(xpath).ToIosChildBy().Criteria);
}
