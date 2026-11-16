namespace Bruinet.NativeIos.Tests;

public sealed class NativeIosLocatorTests
{
    [Fact]
    public void ByTextContains_Creates_Label_And_Value_XPath()
    {
        var locator = NativeIosLocator.ByTextContains("Contacts");

        Assert.Equal(LocatorStrategy.XPath, locator.Strategy);
        Assert.Contains("contains(@label, 'Contacts')", locator.Value);
        Assert.Contains("contains(@value, 'Contacts')", locator.Value);
    }

    [Fact]
    public void ByLabelOrValue_Creates_Exact_Label_And_Value_XPath()
    {
        var locator = NativeIosLocator.ByLabelOrValue("Log in");

        Assert.Equal(LocatorStrategy.XPath, locator.Strategy);
        Assert.Contains("@label='Log in'", locator.Value);
        Assert.Contains("@value='Log in'", locator.Value);
    }

    [Fact]
    public void ToXPathLiteral_Handles_Single_And_Double_Quotes()
    {
        var literal = NativeIosByExtensions.ToXPathLiteral("Bob's \"Contacts\"");

        Assert.Equal("concat('Bob', \"'\", 's \"Contacts\"')", literal);
    }

    [Fact]
    public void ByIdentifierEndsWith_Matches_The_Name_Suffix()
    {
        var locator = NativeIosLocator.ByIdentifierEndsWith("contactName");

        Assert.Equal(LocatorStrategy.XPath, locator.Strategy);
        Assert.Contains("@name", locator.Value);
        Assert.Contains("contactName", locator.Value);
    }
}
