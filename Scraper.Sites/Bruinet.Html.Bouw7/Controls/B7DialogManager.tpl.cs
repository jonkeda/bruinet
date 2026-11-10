// <scraped control="b7-dialog-manager||hydrated|#shadow" from="2026-10-02-recording/008-department.nodes.jsonl#502" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

public partial class B7DialogManager<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public B7DialogManager(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public B7DialogManager(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    protected override string? GetTextCore(IHtmlElement element)
    {
        return element.Text?.Trim();
    }
}
