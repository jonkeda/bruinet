using Bruinet.Core.Calls;
using Bruinet.Core.Configuration;
using Bruinet.Core.Locators;
using Bruinet.Core.Logging;
using Bruinet.Core.Scopes;
using Bruinet.Html.Context;
using Bruinet.Html.Interfaces;
using Bruinet.Html.Playwright;
using Microsoft.Playwright;

namespace Bruinet.Blazor.Context;

public sealed class BlazorTestContext : IHtmlTestContext, IHtmlPageProbe, IAsyncDisposable, Bruinet.Core.Recording.IRecordable
{
    private readonly PlaywrightTestContext _inner;

    private BlazorTestContext(PlaywrightTestContext inner) => _inner = inner;

    public static async Task<BlazorTestContext> CreateAsync(HtmlTestContextOptions options)
        => new(await PlaywrightTestContext.CreateAsync(options));

    public static BlazorTestContext ForPage(IPage page, HtmlTestContextOptions? options = null)
        => new(PlaywrightTestContext.ForPage(page, options));

    public IHtmlTestContext Context => this;
    public IHtmlDriver Driver => _inner.Driver;
    public string? Name => _inner.Name;
    public NearMissSettings NearMiss => _inner.NearMiss;
    public TimeoutSettings Timeouts => _inner.Timeouts;
    public ITestLogger Logger => _inner.Logger;
    public LocatorStrategy DefaultLocatorStrategy => _inner.DefaultLocatorStrategy;
    public IHtmlPage? Page => _inner.Page;
    public string CurrentUrl => _inner.CurrentUrl;
    public string PageTitle => _inner.PageTitle;

    public ScopeReadiness ProbeReadiness() => _inner.ProbeReadiness();

    // The page's probe in one trip, and its wait in the browser, as the Playwright context answers them.
    public PageProbe ProbePage(string busyAttribute) => _inner.ProbePage(busyAttribute);
    public PageProbe ProbePageWhenReady(string busyAttribute, Bruinet.Html.Scopes.BusySignal signal, int timeoutMs)
        => _inner.ProbePageWhenReady(busyAttribute, signal, timeoutMs);
    /// <inheritdoc />
    public int WaitFirst(int? timeoutMs, params Func<bool>[] outcomes) => new Bruinet.Html.Pages.AppRoot(this).WaitFirst(timeoutMs, outcomes);

    public bool WaitReady(int? timeoutMs = null) => _inner.WaitReady(timeoutMs);
    public IHtmlElement? TryFindElement(Locator locator) => _inner.TryFindElement(locator);
    public IHtmlElement FindElement(Locator locator) => _inner.FindElement(locator);
    public IReadOnlyList<IHtmlElement> FindElements(Locator locator) => _inner.FindElements(locator);

    public void NavigateTo(string destination) => _inner.NavigateTo(destination);
    public void NavigateBack() => _inner.NavigateBack();
    public void GoForward() => _inner.GoForward();
    public void Refresh() => _inner.Refresh();

    public byte[] TakeScreenshot() => _inner.TakeScreenshot();
    public void SaveScreenshot(string path) => _inner.SaveScreenshot(path);

    public void ResetAppState() => _inner.ResetAppState();

    internal IPage InternalPage => _inner.InternalPage;

    /// <summary>The browser page; null once it is closed.</summary>
    public Bruinet.Core.Recording.RecordingTarget? RecordingTarget => _inner.RecordingTarget;

    public void Dispose() => _inner.Dispose();

    public ValueTask DisposeAsync() => ((IAsyncDisposable)_inner).DisposeAsync();
    public bool IsIdle() => true;
}
