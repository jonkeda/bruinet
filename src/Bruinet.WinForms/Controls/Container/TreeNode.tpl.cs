using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Container;

/// <summary>
/// A tree view item: expanded and collapsed through ExpandCollapse, and clicked to select it.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TreeNode<TScope> : ExpandableControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public TreeNode(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TreeNode(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
