using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Selection;

/// <summary>
/// A combo box. Its selected text is its Value. An item is selected by writing its text to that
/// Value, which WinForms takes as choosing the item (its <c>SelectedItem</c> changes, and a text
/// with no item is ignored, so the confirmation catches it); a combo box whose Value is read-only
/// is expanded, its item selected and collapsed again. Each WinForms combo box call through UI
/// Automation blocks about two seconds while it opens its list, so one write beats three calls.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ComboBox<TScope> : SelectorControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public ComboBox(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ComboBox(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <inheritdoc />
    protected override bool ExpandsToShowItems => true;

    /// <summary>Selects the item whose text is <paramref name="text"/> by writing it to the Value, and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The item's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SelectByTextCore(IWinFormsElement element, string? text, int? timeoutMs = null)
    {
        if (element.IsReadOnly != false)
        {
            base.SelectByTextCore(element, text, timeoutMs);
            return;
        }

        if (text == null || GetSelectedTextCore(element) == text)
            return;

        ActAndConfirm(
            "SelectByText", () => element.SetValue(text),
            () => GetSelectedTextCore(element), selected => selected == text,
            $"'{text}' selected (an item with that text)", timeoutMs);
    }
}
