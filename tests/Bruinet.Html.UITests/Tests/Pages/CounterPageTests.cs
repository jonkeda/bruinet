using Bruinet.Html;
using Bruinet.Html.UITests.PageObjects;
using Bruinet.Html.UITests.TestBase;

namespace Bruinet.Html.UITests.Tests.Pages;

public sealed class CounterPageTests : BlazorSampleTestBase
{
    [Fact]
    public void Counter_MultipleIncrements_DisplaysCorrectCount()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.IncrementButton.Click();
        page.IncrementButton.Click();
        page.IncrementButton.Click();

        page.CountDisplay.AssertText("Current count: 3");
    }

    [Fact]
    public void Counter_ResetAfterIncrements_DisplaysZero()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.IncrementButton.Click();
        page.IncrementButton.Click();
        page.ResetButton.Click();

        page.CountDisplay.AssertText("Current count: 0");
    }
}
