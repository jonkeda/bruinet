using Bruinet.Core.Locators;
using Bruinet.Html.Controls;

namespace Bruinet.Html.Tests;

/// <summary>A control's string is CSS when it reads as CSS, an automation id otherwise (Bouw7 friction #24).</summary>
public sealed class StringLocatorTests
{
    private sealed class AnyPage(IHtmlTestContext context) : PageObjectBase<AnyPage>(context);

    private abstract class Probe : ViewBase<AnyPage>
    {
        private Probe() : base(null!, Locator.ByCss("x")) { }

        public static Locator Resolve(string selectorOrId) => ResolveLocator(selectorOrId);
    }

    [Theory]
    [InlineData("h1.page-header-title")]
    [InlineData("button.v-btn")]
    [InlineData("td.bold-column")]
    [InlineData(".v-btn")]
    [InlineData("[data-cy='save']")]
    [InlineData("form input")]
    public void A_selector_is_css(string value)
    {
        Assert.Equal(LocatorStrategy.Css, Probe.Resolve(value).Strategy);
    }

    [Theory]
    [InlineData("SaveButton")]
    [InlineData("user.name")]
    [InlineData("order-total")]
    public void Anything_else_is_an_automation_id(string value)
    {
        Assert.Equal(LocatorStrategy.AutomationId, Probe.Resolve(value).Strategy);
    }
}
