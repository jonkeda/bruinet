namespace Bruinet.Maui.Controls.Base;

/// <summary>
/// Base class for MAUI controls with selection capability.
/// Implements ISelectorControlObject with SelectByText, SelectByIndex, GetSelectedText.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class SelectorControlBase<TScope> : FocusableControlBase<TScope>,
    ISelectorControlObject<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new selector control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the element.</param>
    public SelectorControlBase(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new selector control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public SelectorControlBase(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Selection - Core Methods

    /// <summary>
    /// Selects item by text on pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text to select. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void SelectByTextCore(IMauiElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        element.SelectByText(text);
    }

    /// <summary>
    /// Selects item by index on pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="index">The 0-based index to select. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void SelectByIndexCore(IMauiElement element, int? index, int? timeoutMs = null)
    {
        if (index == null)
            return;

        element.SelectIndex(index.Value);
    }

    /// <summary>
    /// Selects item by value on pre-found element.
    /// Override in derived classes for picker-specific implementation.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The value to select. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void SelectByValueCore(IMauiElement element, string? value, int? timeoutMs = null)
    {
        if (value == null)
            return;

        SelectByTextCore(element, value, timeoutMs);
    }

    #endregion

    #region Selection State - Core Methods

    /// <summary>
    /// Gets selected text from pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The selected text, or null if not available.</returns>
    protected virtual string? GetSelectedTextCore(IMauiElement? element)
        => element?.SelectedItemText;

    /// <summary>
    /// Gets selected index from pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The selected index, or null if not available.</returns>
    protected virtual int? GetSelectedIndexCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // No platform publishes a selected index. It is derived: find the selected text, then
        // its position among the item texts. Both halves are read from real elements.
        var selectedText = GetSelectedTextCore(element);
        if (!string.IsNullOrEmpty(selectedText))
        {
            var itemTexts = GetItemTextsCore(element);
            if (itemTexts != null)
            {
                for (int i = 0; i < itemTexts.Count; i++)
                {
                    if (itemTexts[i] == selectedText)
                        return i;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all item texts from pre-found element.
    /// Override in derived classes for picker-specific implementation.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>List of item texts, or null if not available.</returns>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(IMauiElement? element)
        => element?.ReadItemTexts();

    /// <summary>
    /// Gets item count from pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The item count, or null if not available.</returns>
    protected virtual int? GetItemCountCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // Counted, not asked: no platform publishes an item count, so the items are read.
        return element.ReadItemTexts()?.Count;
    }

    #endregion

}
