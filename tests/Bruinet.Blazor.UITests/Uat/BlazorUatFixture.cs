namespace Bruinet.Blazor.UITests.Uat;

using Bruinet.Core.Configuration;
using Bruinet.Blazor.UITests.TestBase;
using Bruinet.Html.Testing;

[TestModuleScan(typeof(CounterPage), NamespacePrefix = "Bruinet.Blazor.UITests.PageObjects")]
public sealed class BlazorUatFixture : IDisposable
{
    private readonly WebAppHost? _host;
    private readonly IHtmlTestContext _context;
    private readonly string _baseUrl;
    private bool _disposed;

    // The browser head starts the sample and a browser; the bUnit head renders its Routes (WebHeads).
    public BlazorUatFixture()
    {
        if (BlazorSampleBunit.IsHead)
        {
            _context = BlazorSampleBunit.Create();
            _baseUrl = BlazorSampleBunit.BaseUrl;
            Composition = TestComposition.ForFixture(this, services =>
                services.AddSingleton(_context));
            return;
        }

        _host = WebAppHost.Start(new WebAppHostOptions
        {
            AppPath = WebAppHost.FindBuiltApp("samples/Bruinet.Samples.Blazor.App"),
            Name = "Blazor sample",
        });
        _baseUrl = _host.BaseUrl;
        try
        {
            _context = BlazorTestContext.CreateAsync(new HtmlTestContextOptions
            {
                BaseUrl = _host.BaseUrl,
            }).GetAwaiter().GetResult();
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

    public void NavigateToLogin()
    {
        NavigateTo("/login");
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
