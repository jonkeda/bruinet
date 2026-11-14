using Bruinet.Html.Playwright;
using Microsoft.Playwright;

namespace Bruinet.Html.Tests;

/// <summary>How a Bruinet locator becomes a Playwright one: the accessible name, and quoting.</summary>
public class LocatorMappingTests
{
    private readonly Mock<ILocator> _root = new();

    [Fact]
    public void AccessibilityId_FindsByTheAccessibleName_Exactly()
    {
        LocatorExtensions.ToPlaywrightLocator(_root.Object, Locator.ByAccessibilityId("Close dialog"));

        _root.Verify(l => l.GetByLabel("Close dialog", It.Is<LocatorGetByLabelOptions?>(o => o != null && o.Exact == true)), Times.Once);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("it's", @"it\'s")]
    [InlineData(@"a\b", @"a\\b")]
    [InlineData(@"end\'", @"end\\\'")]
    public void EscapeForAttribute_EscapesBackslashesThenQuotes(string value, string escaped)
        => Assert.Equal(escaped, LocatorExtensions.EscapeForAttribute(value));

    [Fact]
    public void DataTestId_WithAQuoteAndABackslash_IsOneValidSelector()
    {
        LocatorExtensions.ToPlaywrightLocator(_root.Object, Locator.ByDataTestId(@"O'Brien\row"));

        _root.Verify(l => l.Locator(@"[data-testid='O\'Brien\\row']", It.IsAny<LocatorLocatorOptions?>()), Times.Once);
    }

    [Fact]
    public void A_child_of_a_control_is_looked_for_once_in_its_parent()
    {
        var page = new Mock<IPage>();
        var root = new Mock<ILocator>();
        var table = new Mock<ILocator>();
        page.Setup(p => p.Locator(":root", It.IsAny<PageLocatorOptions?>())).Returns(root.Object);
        root.Setup(l => l.Locator(".table", It.IsAny<LocatorLocatorOptions?>())).Returns(table.Object);
        table.Setup(l => l.Locator(It.IsAny<string>(), It.IsAny<LocatorLocatorOptions?>())).Returns(Mock.Of<ILocator>());

        LocatorExtensions.ToPlaywrightLocator(page.Object, null, new Locator(LocatorStrategy.Css, "tr.filter", Locator.ByCss(".table")));

        root.Verify(l => l.Locator(".table", It.IsAny<LocatorLocatorOptions?>()), Times.Once);
        table.Verify(l => l.Locator(".table", It.IsAny<LocatorLocatorOptions?>()), Times.Never);
        table.Verify(l => l.Locator("tr.filter", It.IsAny<LocatorLocatorOptions?>()), Times.Once);
    }
}
