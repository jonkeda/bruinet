using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Container;

public class TabContainerControl<TParent, TScope> : ContainerBase<TParent, TScope>
    where TParent : IHtmlScope<TParent>
    where TScope : IHtmlContainer<TParent, TScope>
{
    private readonly string _tabSelector;

    public TabContainerControl(IHtmlScope<TParent> parentScope, Locator locator, string tabSelector = "[role='tab']")
        : base(parentScope, locator)
    {
        _tabSelector = tabSelector;
    }

    public override TScope Self => (TScope)(object)this;

    public TScope SelectTab(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var tabs = ContainerRoot.FindElements(Locator.ByCss(_tabSelector));
        if (index >= tabs.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Tab index {index} is out of range.");
        }

        tabs[index].Click();
        return Self;
    }

    public TScope SelectTab(string text)
    {
        var tabs = ContainerRoot.FindElements(Locator.ByCss(_tabSelector));
        var tab = tabs.FirstOrDefault(t => string.Equals(t.Text?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tab == null)
        {
            throw new InvalidOperationException($"Tab with text '{text}' was not found.");
        }

        tab.Click();
        return Self;
    }

    public int TabCount => ContainerRoot.FindElements(Locator.ByCss(_tabSelector)).Count;
}
