using Bruinet.Core.Locators;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Container;

/// <summary>
/// A tab's content as a page object of its own: the container is the tab's panel, and its tab is
/// found in the parent. <see cref="Open"/> selects the tab when it is not selected, and returns once
/// the tab says it is selected and the panel is shown, so a test never acts on the panel of the tab
/// that was open before.
/// </summary>
/// <typeparam name="TParent">The page or container the tabs are on.</typeparam>
/// <typeparam name="TSelf">The tab page's own type.</typeparam>
public abstract class TabPageBase<TParent, TSelf> : ContainerBase<TParent, TSelf>
    where TParent : IHtmlScope<TParent>
    where TSelf : TabPageBase<TParent, TSelf>
{
    /// <summary>The tab page whose tab is found by <paramref name="tab"/> and whose content is found by <paramref name="panel"/>.</summary>
    /// <param name="parentScope">The page or container the tabs are on.</param>
    /// <param name="tab">The tab, the element that is clicked and says whether it is selected.</param>
    /// <param name="panel">The content shown while the tab is selected.</param>
    protected TabPageBase(IHtmlScope<TParent> parentScope, Locator tab, Locator panel)
        : base(parentScope, panel)
    {
        _tab = tab;
        Tab = new ButtonControl<TParent>(parentScope, tab);
    }

    private readonly Locator _tab;

    /// <inheritdoc />
    public override TSelf Self => (TSelf)this;

    /// <summary>The tab itself.</summary>
    public ButtonControl<TParent> Tab { get; }

    /// <summary>The tab's attribute that is <see cref="SelectedValue"/> while the tab is selected: <c>aria-selected</c> by default.</summary>
    protected virtual string SelectedAttribute => "aria-selected";

    /// <summary>The value of <see cref="SelectedAttribute"/> while the tab is selected: <c>true</c> by default.</summary>
    protected virtual string SelectedValue => "true";

    /// <summary>Whether the tab is selected now.</summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds, to find the tab.</param>
    public bool IsSelected(int? timeoutMs = null)
        => string.Equals(Tab.GetAttribute(SelectedAttribute, timeoutMs), SelectedValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>Selects the tab when it is not selected, and waits until it is selected and its content is shown.</summary>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The tab page, to act on its content.</returns>
    public TSelf Open(int? timeoutMs = null)
    {
        if (!IsSelected(timeoutMs))
        {
            Tab.Click(timeoutMs);
        }

        Tab.AssertAttribute(SelectedAttribute, SelectedValue, $"The tab {_tab} did not become selected.", timeoutMs);
        return AssertVisible(true, $"The content of the tab {_tab} is not shown ({Locator}).", timeoutMs);
    }
}
