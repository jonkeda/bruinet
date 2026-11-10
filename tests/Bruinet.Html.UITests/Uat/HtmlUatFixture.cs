namespace Bruinet.Html.UITests.Uat;

using Bruinet.Html.UITests.Uat.Pages;

using Bruinet.Core.Configuration;
using Bruinet.Html.Testing;
using Bruinet.Html.UITests.TestBase;

[TestModuleScan(typeof(CounterPage), NamespacePrefix = "Bruinet.Html.UITests.Uat.Pages")]
public sealed class HtmlUatFixture : IDisposable
{
    private readonly WebAppHost? _host;
    private readonly IHtmlTestContext _context;
    private readonly string _baseUrl;
    private bool _disposed;

    // The browser head starts the sample and a browser; the bUnit head renders its Routes (WebHeads).
    public HtmlUatFixture()
    {
        if (BlazorSampleBunit.IsHead)
        {
            _context = BlazorSampleBunit.Create();
            _baseUrl = BlazorSampleBunit.BaseUrl;
            Composition = TestComposition.ForFixture(this, services =>
                services.AddSingleton(_context));
            return;
        }

        var options = TestContextOptionsBase.Load<HtmlTestContextOptions>(HtmlTestFixtureBase.SettingsFile);
        _host = WebAppHost.Start(new WebAppHostOptions
        {
            AppPath = WebAppHost.FindBuiltApp("samples/Bruinet.Samples.Blazor.App"),
            Name = "Blazor sample",
        });
        _baseUrl = _host.BaseUrl;
        try
        {
            options.BaseUrl = _host.BaseUrl;
            _context = PlaywrightTestContext.CreateAsync(options).GetAwaiter().GetResult();
        }
        catch
        {
            _host.Dispose();
            throw;
        }

        Composition = TestComposition.ForFixture(this, services =>
            services.AddSingleton(_context));
    }

    public TestComposition Composition { get; }

    public void NavigateToCounter()
    {
        NavigateTo("/counter");
    }

    public void NavigateToFormControls()
    {
        NavigateTo("/form-controls");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            (_context as IAsyncDisposable)?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            _host?.Dispose();
        }
    }

    private void NavigateTo(string path)
    {
        _context.NavigateTo($"{_baseUrl}/{path.TrimStart('/')}");
    }
}
