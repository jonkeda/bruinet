using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Selection;

/// <summary>
/// A tab of a <c>TabControl</c>: clicked to select it (SelectionItem), and selected or not.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TabPage<TScope> : ClickableControlBase<TScope>, ITabControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public TabPage(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TabPage(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <summary>The tab's header text now, or empty when the tab is not there. Does not wait (R7).</summary>
    public string Title => TryFindElement()?.Name ?? string.Empty;

    /// <summary>Whether the tab is the one selected.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsSelectedCore(IWinFormsElement? element) => element?.Selected;
}
