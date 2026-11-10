// <scraped control="div||v-input__append-inner|div" from="2026-10-02-recording/008-department.nodes.jsonl#147" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

public partial class VInputAppendInner<TScope> : ViewBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public VInputAppendInner(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public VInputAppendInner(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    protected override string? GetTextCore(IHtmlElement element)
    {
        return element.Text?.Trim();
    }
}
