using Bruinet.Blazor.UITests.PageObjects;
using Bruinet.Blazor.UITests.TestBase;

namespace Bruinet.Blazor.UITests.Tests.Controls;

public sealed class ControlStateTests : BlazorSampleTestBase
{
    [Fact]
    public void Control_IsExists_ReturnsTrueForExistingControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.IncrementButton.Exists());
    }

    [Fact]
    public void Control_WaitExists_WaitsForControlToExist()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.IncrementButton.WaitExists(true, 5000));
    }

    [Fact]
    public void Control_AssertExists_PassesForExistingControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.IncrementButton.AssertExists(true);
    }

    [Fact]
    public void Control_IsVisible_ReturnsTrueForVisibleControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.IncrementButton.IsVisible());
    }

    [Fact]
    public void Control_WaitVisible_WaitsForControlToBeVisible()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.CounterTitle.WaitVisible(true, 5000));
    }

    [Fact]
    public void Control_AssertVisible_PassesForVisibleControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.CounterTitle.AssertVisible(true);
    }

    [Fact]
    public void Control_IsEnabled_ReturnsTrueForEnabledControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.IncrementButton.IsEnabled());
    }

    [Fact]
    public void Control_WaitEnabled_WaitsForControlToBeEnabled()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        Assert.True(page.ResetButton.WaitEnabled(true, 5000));
    }

    [Fact]
    public void Control_AssertEnabled_PassesForEnabledControl()
    {
        NavigateToPage("/counter");
        var page = new CounterPage(Context);

        page.ResetButton.AssertEnabled(true);
    }
}
