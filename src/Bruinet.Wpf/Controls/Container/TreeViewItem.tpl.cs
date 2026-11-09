using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Container;

/// <summary>
/// A tree view item: expanded and collapsed through ExpandCollapse, and clicked to select it.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TreeViewItem<TScope> : ExpandableControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public TreeViewItem(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TreeViewItem(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
