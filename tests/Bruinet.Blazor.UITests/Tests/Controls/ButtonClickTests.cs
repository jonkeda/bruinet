using Bruinet.Blazor.UITests.PageObjects;
using Bruinet.Blazor.UITests.TestBase;

namespace Bruinet.Blazor.UITests.Tests.Controls;

public sealed class ButtonClickTests : BlazorSampleTestBase
{
    [Fact]
    public void Button_Click_TriggersAction()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.CountDisplay.AssertText("Current count: 0");
        page.IncrementButton.Click();
        page.CountDisplay.AssertText("Current count: 1");
    }

    [Fact]
    public void Button_MultipleClicks_AllRegister()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.ResetButton.Click();
        page.IncrementButton.Click();
        page.IncrementButton.Click();
        page.IncrementButton.Click();

        page.CountDisplay.AssertTextContains("3");
    }
}
