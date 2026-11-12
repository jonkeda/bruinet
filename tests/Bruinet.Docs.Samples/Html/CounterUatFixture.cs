// docs/getting-started/quick-start-uat.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:fixture
using Bruinet.Core.Composition;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.UITests;

// The pages the scenarios name, and how they are made: from this namespace, with the browser's context.
[TestModuleScan(typeof(CounterPage), NamespacePrefix = "MyApp.UITests")]
public sealed class CounterUatFixture : IDisposable
{
    private const string App = "https://localhost:5001";
    private readonly IHtmlTestContext _context;

    public CounterUatFixture()
    {
        _context = PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions { BaseUrl = App }).GetAwaiter().GetResult();
        Composition = TestComposition.ForFixture(this, services => services.AddSingleton(_context));
    }

    public TestComposition Composition { get; }

    // "Given I am on the Counter page"
    public void NavigateToCounter() => _context.NavigateTo($"{App}/counter");

    public void Dispose() => ((IAsyncDisposable)_context).DisposeAsync().AsTask().GetAwaiter().GetResult();
}
#endregion
