using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Selection;

/// <summary>
/// A list box: its items are always in the tree, each selected through its SelectionItem pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ListBox<TScope> : SelectorControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public ListBox(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ListBox(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
