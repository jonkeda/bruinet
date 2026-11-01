// <scraped control="button||v-btn|span" from="2026-10-02-recording/008-department.nodes.jsonl#489" />
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Bouw7.Controls;

/// <summary>
/// Vuetify button (v-btn), often icon-only. Click, and the rest of the clickable members, come
/// from <see cref="ClickableControlBase{TScope}"/>.
/// </summary>
public partial class VBtn<TScope> : ClickableControlBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public VBtn(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public VBtn(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    /// <summary>The button's accessible label: aria-label, else name, else its visible text.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetLabelCore(IHtmlElement? element)
    {
        if (element == null)
            return null;

        var label = element.GetAttribute("aria-label");
        if (!string.IsNullOrWhiteSpace(label))
            return label.Trim();

        var name = element.GetAttribute("name");
        if (!string.IsNullOrWhiteSpace(name))
            return name.Trim();

        return element.Text?.Trim();
    }
}
