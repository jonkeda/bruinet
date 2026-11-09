using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for WPF controls that select one item: SelectByText, SelectByIndex, GetSelectedText.
/// An item is chosen with its SelectionItem pattern; a control whose items show only while it is
/// open (a combo box) is expanded first and collapsed after (ExpandCollapse). No pointer, no keys.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class SelectorControlBase<TScope> : ClickableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    protected SelectorControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected SelectorControlBase(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    /// <summary>The control's items inside it: its list items.</summary>
    protected virtual Locator ItemLocator => Locator.ByControlType("ListItem");

    /// <summary>Whether the items show only while the control is expanded (a combo box).</summary>
    protected virtual bool ExpandsToShowItems => false;

    /// <summary>An item's text: its UI Automation name (its content, or its item's <c>ToString()</c>).</summary>
    protected virtual string ItemText(IWpfElement item) => item.Name ?? item.Text ?? string.Empty;

    #region Selection - Core Methods

    /// <summary>Selects the item whose text is <paramref name="text"/>, and confirms it is selected.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The item's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByTextCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        if (text == null || GetSelectedTextCore(element) == text)
            return;

        ActAndConfirm(
            "SelectByText", () => Choose(element, items => items.FirstOrDefault(item => ItemText(item) == text), $"'{text}'"),
            () => GetSelectedTextCore(element), selected => selected == text,
            $"'{text}' selected", timeoutMs);
    }

    /// <summary>Selects the item whose text is <paramref name="value"/>: a WPF item shows its value as its text.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The item's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByValueCore(IWpfElement element, string? value, int? timeoutMs = null)
    {
        SelectByTextCore(element, value, timeoutMs);
    }

    /// <summary>Selects the item at <paramref name="index"/>, and confirms its text is selected.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="index">The item's position (0-based); null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByIndexCore(IWpfElement element, int? index, int? timeoutMs = null)
    {
        if (index is not { } position)
            return;

        string? chosen = null;
        ActAndConfirm(
            "SelectByIndex",
            () => Choose(element, items =>
            {
                var item = items.ElementAtOrDefault(position);
                chosen = item is null ? null : ItemText(item);
                return item;
            }, $"item {position}"),
            () => GetSelectedTextCore(element), selected => selected == chosen,
            $"item {position} selected", timeoutMs);
    }

    // One selection: open when the items need it, select once, close again. An item that is not
    // there fails, naming what was there: the selection is never tried again (R0).
    private void Choose(IWpfElement element, Func<IReadOnlyList<IWpfElement>, IWpfElement?> pick, string what)
    {
        var opened = Open(element);
        try
        {
            var items = element.FindElements(ItemLocator);
            if (pick(items) is not { } item)
            {
                throw new InvalidOperationException(
                    $"{Locator} has no {what}: its items are {string.Join(", ", items.Select(i => $"'{ItemText(i)}'"))}.");
            }

            item.Select();
        }
        finally
        {
            if (opened)
            {
                element.Collapse();
            }
        }
    }

    // Expands a control whose items show only while it is open; answers whether it did.
    private bool Open(IWpfElement element)
    {
        if (!ExpandsToShowItems || element.IsExpanded != false)
        {
            return false;
        }

        element.Expand();
        return true;
    }

    #endregion

    #region Selection State - Core Methods

    /// <summary>The selected item's text: the control's Value when it has one (a combo box), else its selected item's.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetSelectedTextCore(IWpfElement? element)
    {
        if (element == null)
            return null;

        if (element.Value is { } value)
            return value;

        return element.FindElements(ItemLocator).FirstOrDefault(item => item.Selected) is { } selected ? ItemText(selected) : null;
    }

    /// <summary>The selected item's text, as its value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetSelectedValueCore(IWpfElement? element)
        => GetSelectedTextCore(element);

    /// <summary>The selected item's position (0-based); null when it is not among the items.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetSelectedIndexCore(IWpfElement? element)
    {
        if (element == null || GetSelectedTextCore(element) is not { } selected)
            return null;

        var index = GetItemTextsCore(element)?.ToList().IndexOf(selected) ?? -1;
        return index >= 0 ? index : null;
    }

    /// <summary>The items' texts, in order; a combo box is opened to read them and closed again.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(IWpfElement? element)
    {
        if (element == null)
            return null;

        var opened = Open(element);
        try
        {
            return [.. element.FindElements(ItemLocator).Select(ItemText)];
        }
        finally
        {
            if (opened)
            {
                element.Collapse();
            }
        }
    }

    /// <summary>How many items there are.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetItemCountCore(IWpfElement? element)
        => GetItemTextsCore(element)?.Count;

    #endregion
}
