// <scraped control="b7-navbar-button||hydrated.interactive.right-side|#shadow" from="2026-10-02-recording/008-department.nodes.jsonl#74" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

public partial class B7NavbarButton<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    private static readonly Locator TextPart = Locator.ByCss("div");

    public B7NavbarButton(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public B7NavbarButton(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    protected override string? GetTextCore(IHtmlElement element)
    {
        return element.TryFindElement(TextPart)?.Text?.Trim() ?? element.Text?.Trim();
    }
}
