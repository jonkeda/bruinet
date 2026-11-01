using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Controls;

public sealed class ButtonControlTests : BlazorSampleTestBase
{
    [Fact]
    public void Button_Click_IncrementsCounter()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.CountDisplay.AssertText("Current count: 0");
        page.IncrementButton.Click();
        page.CountDisplay.AssertText("Current count: 1");
    }

    [Fact]
    public void Button_IsVisible_ReturnsTrueForVisibleButton()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.IncrementButton.IsVisible());
    }

    [Fact]
    public void Button_AssertEnabled_PassesForEnabledButton()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.IncrementButton.AssertEnabled(true);
    }
}
