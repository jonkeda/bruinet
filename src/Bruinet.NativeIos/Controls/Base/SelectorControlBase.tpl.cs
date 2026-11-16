using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls;

/// <summary>
/// Base class for native iOS controls that select one option: SelectByText, SelectByIndex,
/// GetSelectedText. The defaults drive a picker wheel (<c>UIPickerView</c>, XCUITest type
/// <c>PickerWheel</c>): XCUITest sets a wheel by its row's text and reports the chosen row as its
/// value. A control built otherwise (a segmented control, a tab bar) overrides the Cores.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class SelectorControlBase<TScope> : ClickableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    // A wheel this long is not a list a test reads to its end.
    private const int MaxWheelRows = 200;

    protected SelectorControlBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected SelectorControlBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    #region Selection - Core Methods

    /// <summary>Turns the wheel to the row whose text is <paramref name="text"/>, and confirms it is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The row's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByTextCore(INativeIosElement element, string? text, int? timeoutMs = null)
    {
        if (text == null || GetSelectedTextCore(element) == text)
            return;

        ActAndConfirm(
            "SelectByText", () => element.Adjust(text),
            () => GetSelectedTextCore(element), selected => selected == text,
            $"'{text}' selected", timeoutMs);
    }

    /// <summary>Selects the row whose text is <paramref name="value"/>: an iOS row has no value of its own.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="value">The row's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByValueCore(INativeIosElement element, string? value, int? timeoutMs = null)
    {
        SelectByTextCore(element, value, timeoutMs);
    }

    /// <summary>Selects the row at <paramref name="index"/>, reading the rows first, and confirms its text is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="index">The row's position (0-based); null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByIndexCore(INativeIosElement element, int? index, int? timeoutMs = null)
    {
        if (index is not { } position)
            return;

        var rows = GetItemTextsCore(element) ?? [];
        var text = rows.ElementAtOrDefault(position)
            ?? throw new ArgumentOutOfRangeException(nameof(index), $"Option index {position} is out of range (0-{rows.Count - 1}). Locator: {Locator}");
        SelectByTextCore(element, text, timeoutMs);
    }

    #endregion

    #region Selection State - Core Methods

    /// <summary>The selected row's text: the wheel's value.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetSelectedTextCore(INativeIosElement? element)
        => element?.Value;

    /// <summary>The selected row's text, as its value.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetSelectedValueCore(INativeIosElement? element)
        => GetSelectedTextCore(element);

    /// <summary>The selected row's position (0-based); null when it is not among the rows.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual int? GetSelectedIndexCore(INativeIosElement? element)
    {
        if (element == null || GetSelectedTextCore(element) is not { } selected)
            return null;

        var index = GetItemTextsCore(element)?.ToList().IndexOf(selected) ?? -1;
        return index >= 0 ? index : null;
    }

    /// <summary>
    /// The rows' texts, in order. XCUITest shows only the chosen row, so this turns the wheel to its
    /// first row, reads each row to the last, and turns it back to the row it was on.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(INativeIosElement? element)
    {
        if (element == null)
            return null;

        var start = element.Value;
        var current = start;
        for (var step = 0; step < MaxWheelRows; step++)
        {
            element.StepWheel(forward: false);
            var previous = element.Value;
            if (previous == current)
            {
                break;
            }

            current = previous;
        }

        var rows = new List<string>();
        if (current != null)
        {
            rows.Add(current);
        }

        for (var step = 0; step < MaxWheelRows; step++)
        {
            element.StepWheel(forward: true);
            var next = element.Value;
            if (next == null || next == current)
            {
                break;
            }

            rows.Add(next);
            current = next;
        }

        if (start != null && start != current)
        {
            element.Adjust(start);
        }

        return rows;
    }

    /// <summary>How many rows there are.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual int? GetItemCountCore(INativeIosElement? element)
        => GetItemTextsCore(element)?.Count;

    #endregion
}
