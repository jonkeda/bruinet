// docs/getting-started/quick-start-html.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;
using Xunit;

namespace MyApp.UITests;

public sealed class CounterTests : IAsyncLifetime
{
    private const string App = "https://localhost:5001";
    private IHtmlTestContext _context = null!;

    public async Task InitializeAsync()
        => _context = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { BaseUrl = App });

    public async Task DisposeAsync() => await ((IAsyncDisposable)_context).DisposeAsync();

    [Fact]
    public void Increment_CountsUp()
    {
        _context.NavigateTo($"{App}/counter");
        var page = new CounterPage(_context);

        page.Increment.Click();

        page.Count.AssertText("Current count: 1");
    }
}
#endregion
