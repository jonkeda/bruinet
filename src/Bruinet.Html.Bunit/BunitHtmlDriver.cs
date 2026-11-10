using AngleSharp.Dom;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bunit;

/// <summary>The rendered root as Bruinet's Html driver: lookups, navigation and the page's markup.</summary>
internal sealed class BunitHtmlDriver : IHtmlDriver
{
    private readonly BunitTestContext _context;

    internal BunitHtmlDriver(BunitTestContext context) => _context = context;

    // No <body> without App.razor: the root's first element stands for it.
    public IHtmlElement PageRoot => new BunitHtmlElement(_context, () => _context.Nodes().FirstOrDefault(), null);

    public string CurrentUrl => _context.CurrentUrl;

    public string PageTitle => _context.PageTitle;

    // Bruinet's poll asks; a render in bUnit ends before the event that caused it returns.
    public bool IsIdle() => true;

    public IReadOnlyList<IHtmlElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        var count = _context.Select(locator).Count;
        var elements = new List<IHtmlElement>(count);
        for (var i = 0; i < count; i++)
        {
            var index = i;
            elements.Add(new BunitHtmlElement(_context, () => _context.Select(locator).ElementAtOrDefault(index), locator));
        }

        return elements;
    }

    public void GoTo(string url) => _context.NavigateTo(url);

    public void GoBack() => _context.NavigateBack();

    public void GoForward() => _context.GoForward();

    public void Reload() => _context.Refresh();

    public byte[] TakeScreenshot() => _context.TakeScreenshot();

    public void SaveScreenshot(string path) => _context.SaveScreenshot(path);

    public string GetPageSource() => _context.Markup;

    // No accessibility tree without a browser: the markup is the nearest thing.
    public string GetAutomationTree() => _context.Markup;

    public void Dispose()
    {
    }
}
