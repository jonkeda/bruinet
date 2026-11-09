using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Selection;

/// <summary>
/// A list box: its items are always in the tree, each selected through its SelectionItem pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ListBox<TScope> : SelectorControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public ListBox(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ListBox(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
