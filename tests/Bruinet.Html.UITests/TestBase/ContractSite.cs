using Bruinet.ControlContracts.Html;
using Bruinet.Html.Testing;

namespace Bruinet.Html.UITests.TestBase;

/// <summary>
/// The control contracts' site (tests/Bruinet.ControlContracts.Html.Site), started once for the run on a
/// free port and stopped when the run ends.
/// </summary>
internal static class ContractSite
{
    private static readonly Lazy<string> Url = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Where the site listens, without a trailing slash.</summary>
    public static string BaseUrl => Url.Value;

    private static string Start()
    {
        var host = WebAppHost.Start(new WebAppHostOptions { AppPath = WebAppHost.FindBuiltApp("tests/Bruinet.ControlContracts.Html.Site"), Name = "Control contracts site" });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => host.Dispose();
        return host.BaseUrl;
    }
}

/// <summary>Opens a contract page in Chromium, headless, a browser per test.</summary>
public sealed class ChromiumHead : IContractHead
{
    private IHtmlTestContext? _context;

    public async Task<IHtmlTestContext> OpenAsync(Type component, string path)
    {
        _context = await PlaywrightTestContext.CreateAsync(new HtmlTestContextOptions
        {
            BaseUrl = ContractSite.BaseUrl,
            Headless = true,
            BrowserType = "chromium",
        });
        _context.NavigateTo(ContractSite.BaseUrl + path);
        return _context;
    }

    public ValueTask DisposeAsync() => _context is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;
}
