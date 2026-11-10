using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;
using Microsoft.Playwright;
using static Bruinet.Html.Playwright.PlaywrightErrors;

namespace Bruinet.Html.Playwright;

/// <summary>
/// <see cref="IHtmlDriver"/> over one Playwright page, or one frame in it.
/// </summary>
/// <remarks>
/// The browser, its context and the page belong to <see cref="PlaywrightTestContext"/>, which
/// creates and closes them; this driver only talks to the page.
/// </remarks>
internal sealed class PlaywrightHtmlDriver : IHtmlDriver
{
    private readonly IPage _page;
    private readonly IFrame? _frame;

    /// <summary>Drives <paramref name="page"/>, or <paramref name="frame"/> in it when one is given.</summary>
    public PlaywrightHtmlDriver(IPage page, IFrame? frame = null)
    {
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _frame = frame;
    }

    /// <inheritdoc />
    public IHtmlElement PageRoot
        => new PlaywrightHtmlElement(_frame != null ? _frame.Locator("body") : _page.Locator("body"));

    /// <inheritdoc />
    public string CurrentUrl => _page.Url;

    /// <inheritdoc />
    public string PageTitle => Live(() => _page.TitleAsync().GetAwaiter().GetResult());

    /// <inheritdoc />
    public bool IsIdle()
    {
        try
        {
            Live(() => _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10 }).GetAwaiter().GetResult());
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Waits in the page (or its frame), at most <paramref name="timeoutMs"/>, until the document has loaded and
    /// <paramref name="busyAttribute"/> says ready as <paramref name="signal"/> reads it, checking every 10 ms; then probes
    /// once. A wait that times out, or that a navigation ends, is not an error: the probe says what the page is now.
    /// </summary>
    public PageProbe ProbePageWhenReady(string busyAttribute, Bruinet.Html.Scopes.BusySignal signal, int timeoutMs)
    {
        const string ready = "([a, s]) => document.readyState === 'complete' && document.body !== null"
            + " && (s === 'ignore' || (s === 'required' ? document.body.getAttribute(a) === 'false' : document.body.getAttribute(a) !== 'true'))";
        var argument = new object[] { busyAttribute, signal.ToString().ToLowerInvariant() };
        try
        {
            Live(() => (_frame != null
                    ? _frame.WaitForFunctionAsync(ready, argument, new() { Timeout = timeoutMs, PollingInterval = 10 })
                    : _page.WaitForFunctionAsync(ready, argument, new() { Timeout = timeoutMs, PollingInterval = 10 }))
                .GetAwaiter().GetResult());
        }
        catch (TimeoutException)
        {
            // Still not ready: the probe says how.
        }
        catch (Bruinet.Core.Exceptions.StaleElementException)
        {
            return PageProbe.Navigating;
        }

        return ProbePage(busyAttribute);
    }

    /// <summary>
    /// The document's load state and the body's <paramref name="busyAttribute"/>, from one
    /// evaluation in the page (or its frame). A document a navigation is replacing answers
    /// <see cref="PageProbe.Navigating"/>.
    /// </summary>
    public PageProbe ProbePage(string busyAttribute)
    {
        const string script = "a => JSON.stringify({ ready: document.readyState, body: document.body !== null, busy: document.body?.getAttribute(a) ?? null })";
        try
        {
            var json = Live(() => (_frame != null
                    ? _frame.EvaluateAsync<string>(script, busyAttribute)
                    : _page.EvaluateAsync<string>(script, busyAttribute))
                .GetAwaiter().GetResult());
            using var answer = System.Text.Json.JsonDocument.Parse(json);
            var root = answer.RootElement;
            var busy = root.GetProperty("busy");
            return new PageProbe(root.GetProperty("ready").GetString(), root.GetProperty("body").GetBoolean(),
                busy.ValueKind == System.Text.Json.JsonValueKind.Null ? null : busy.GetString());
        }
        catch (Bruinet.Core.Exceptions.StaleElementException)
        {
            return PageProbe.Navigating;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IHtmlElement> FindElements(Locator locator) => Live<IReadOnlyList<IHtmlElement>>(() =>
    {
        var playwrightLocator = LocatorExtensions.ToPlaywrightLocator(_page, _frame, locator);
        var count = playwrightLocator.CountAsync().GetAwaiter().GetResult();
        var elements = new List<IHtmlElement>(count);
        for (var i = 0; i < count; i++)
        {
            elements.Add(new PlaywrightHtmlElement(playwrightLocator.Nth(i)));
        }

        return elements;
    });

    /// <inheritdoc />
    public void GoTo(string url) => Live(() => _page.GotoAsync(url).GetAwaiter().GetResult());

    /// <inheritdoc />
    public void GoBack() => Live(() => _page.GoBackAsync().GetAwaiter().GetResult());

    /// <inheritdoc />
    public void GoForward() => Live(() => _page.GoForwardAsync().GetAwaiter().GetResult());

    /// <inheritdoc />
    public void Reload() => Live(() => _page.ReloadAsync().GetAwaiter().GetResult());

    /// <inheritdoc />
    public byte[] TakeScreenshot() => Live(() => _page.ScreenshotAsync().GetAwaiter().GetResult());

    /// <inheritdoc />
    /// <remarks>The whole page, not only the viewport, so a saved screen shows everything on it.</remarks>
    public void SaveScreenshot(string path)
        => Live(() => _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true }).GetAwaiter().GetResult());

    /// <inheritdoc />
    public string GetPageSource()
        => Live(() => (_frame != null ? _frame.ContentAsync() : _page.ContentAsync()).GetAwaiter().GetResult());

    /// <inheritdoc />
    public string GetAutomationTree()
        => Live(() => (_frame != null ? _frame.Locator("body") : _page.Locator("body")).AriaSnapshotAsync().GetAwaiter().GetResult());

    /// <summary>Nothing to release: the context owns the page.</summary>
    public void Dispose()
    {
    }
}
