namespace Bruinet.Maui.Controls.Selection;

/// <summary>
/// MAUI Picker control for dropdown selection.
/// Inherits SelectByText, SelectByIndex, GetSelectedText from SelectorControlBase.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Picker<TScope> : Base.SelectorControlBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new picker control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the picker element.</param>
    public Picker(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new picker control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Picker(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Flyout - Core Methods

    // Opening the dropdown uses the ExpandCollapse pattern: MAUI has no public API for it.

    /// <summary>Opens the picker's dropdown.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void OpenFlyoutCore(IMauiElement element, int? timeoutMs = null)
        => element.OpenDropdown();

    /// <summary>Closes the picker's dropdown.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void CloseFlyoutCore(IMauiElement element, int? timeoutMs = null)
        => element.CloseDropdown();

    /// <summary>Whether the picker's dropdown is showing.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>Whether the dropdown is open.</returns>
    protected virtual bool? IsFlyoutOpenCore(IMauiElement? element)
        => element?.IsDropdownOpen ?? false;

    /// <summary>
    /// What the open dropdown is showing.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <returns>The realized item texts, or null where the platform has no dropdown to read.</returns>
    protected virtual IReadOnlyList<string>? GetDropdownItemTextsCore(IMauiElement? element)
        => element?.ReadDropdownItemTexts();

    #endregion

    #region Selection state - Core Methods

    // These reads are answered by the app, without opening the dropdown.

    /// <inheritdoc />
    protected override string? GetSelectedTextCore(IMauiElement? element)
    {
        if (element?.ReadState("SelectedItem") is not { } selected)
        {
            return base.GetSelectedTextCore(element);
        }

        return selected.Length == 0 ? null : selected;
    }

    /// <inheritdoc />
    protected override int? GetSelectedIndexCore(IMauiElement? element)
    {
        if (element?.ReadState("SelectedIndex") is not { } reported)
        {
            return base.GetSelectedIndexCore(element);
        }

        if (!int.TryParse(
                reported,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var index))
        {
            throw new BruinetException(
                $"The picker answered SelectedIndex with '{reported}', which is not a number. "
                + $"The two ends of the bridge disagree about the format. Locator: {Locator}");
        }

        // -1 is MAUI's "nothing is selected", and null is this API's. Neither is an error.
        return index < 0 ? null : index;
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string>? GetItemTextsCore(IMauiElement? element)
    {
        if (element?.ReadState("Items") is not { } reported)
        {
            return base.GetItemTextsCore(element);
        }

        var lines = reported.Split('\n');

        if (!int.TryParse(
                lines[0],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var count)
            || lines.Length - 1 != count)
        {
            throw new BruinetException(
                $"The picker reported {lines.Length - 1} item(s) after announcing '{lines[0]}'. "
                + "Either an item text contains a newline or the two ends of the bridge disagree "
                + $"about the format. Locator: {Locator}");
        }

        return lines.Skip(1).ToList();
    }

    /// <inheritdoc />
    protected override int? GetItemCountCore(IMauiElement? element)
    {
        if (element?.ReadState("ItemCount") is not { } reported)
        {
            return base.GetItemCountCore(element);
        }

        if (!int.TryParse(
                reported,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var count))
        {
            throw new BruinetException(
                $"The picker answered ItemCount with '{reported}', which is not a number. "
                + $"Locator: {Locator}");
        }

        return count;
    }

    #endregion

    #region Title - Core Methods

    /// <summary>
    /// Gets the picker title from the element.
    /// </summary>
    /// <param name="element">The picker element (may be null).</param>
    /// <returns>The title text.</returns>
    protected virtual string? GetTitleCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // The accessible name, which is what a rendered picker's Title becomes.
        return element.Name;
    }

    #endregion
}
