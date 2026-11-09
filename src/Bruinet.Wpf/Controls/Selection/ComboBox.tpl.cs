using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Selection;

/// <summary>
/// A combo box: expanded to show its items (ExpandCollapse), an item selected through its
/// SelectionItem pattern, collapsed again. Its selected text is its Value.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ComboBox<TScope> : SelectorControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public ComboBox(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ComboBox(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <inheritdoc />
    protected override bool ExpandsToShowItems => true;
}
