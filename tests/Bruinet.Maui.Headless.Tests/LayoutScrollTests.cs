using MauiHeadless;
using Bruinet.Core.Locators;

namespace Bruinet.Maui.Headless.Tests;

/// <summary>A layout holds all its rows at once: a search that read them all is at the end of the list.</summary>
public sealed class LayoutScrollTests : IDisposable
{
    private readonly MauiTestContext _ctx = new();
    private readonly MauiHeadlessDriver _driver;

    public LayoutScrollTests()
    {
        var rows = new VerticalStackLayout { AutomationId = "Rows" };
        rows.Add(new Label { AutomationId = "Row", Text = "Mileage journal" });
        rows.Add(new Label { AutomationId = "Row", Text = "Shortcuts" });
        _driver = new MauiHeadlessDriver(_ctx.Render(new ContentPage { AutomationId = "Page", Content = rows }));
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void Scrolling_a_layout_content_answers_not_moved()
        => Assert.Equal(ScrollStep.NotMoved, _driver.FindElements(Locator.ByAutomationId("Rows"))[0].ScrollContent(1));

    [Fact]
    public void Scrolling_a_layout_past_its_rows_answers_not_moved()
        => Assert.Equal(ScrollStep.NotMoved, _driver.FindElements(Locator.ByAutomationId("Rows"))[0].ScrollTowards(2));
}
