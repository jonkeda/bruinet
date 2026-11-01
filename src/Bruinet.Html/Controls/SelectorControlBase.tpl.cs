using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls;

/// <summary>
/// Base class for Html controls that select one option: SelectByText, SelectByValue,
/// SelectByIndex, GetSelectedText. The defaults read and drive a &lt;select&gt;; a control built
/// on other markup (a radio group) overrides them.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class SelectorControlBase<TScope> : FocusableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    protected SelectorControlBase(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected SelectorControlBase(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    private static readonly Locator Options = Locator.ByCss("option");

    #region Selection - Core Methods

    /// <summary>Selects the option whose text is <paramref name="text"/>, and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The option's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByTextCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var option = element.FindElements(Options)
            .FirstOrDefault(candidate => string.Equals(candidate.Text?.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Select option with text '{text}' was not found. Locator: {Locator}");

        var value = option.GetAttribute("value");
        SelectValue(element, string.IsNullOrEmpty(value) ? text : value, timeoutMs);
    }

    /// <summary>Selects the option whose value is <paramref name="value"/>, and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="value">The option's value; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByValueCore(IHtmlElement element, string? value, int? timeoutMs = null)
    {
        if (value == null)
            return;

        SelectValue(element, value, timeoutMs);
    }

    /// <summary>Selects the option at <paramref name="index"/> (0-based), and confirms it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="index">The option's position; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByIndexCore(IHtmlElement element, int? index, int? timeoutMs = null)
    {
        if (index == null)
            return;

        var options = element.FindElements(Options);
        if (index < 0 || index >= options.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Option index {index} is out of range (0-{options.Count - 1}). Locator: {Locator}");
        }

        var option = options[index.Value];
        var value = option.GetAttribute("value");
        SelectValue(element, string.IsNullOrEmpty(value) ? option.Text ?? string.Empty : value, timeoutMs);
    }

    private void SelectValue(IHtmlElement element, string value, int? timeoutMs)
        => ActAndConfirm(
            "SelectOption", () => element.SelectOption(value),
            () => element.InputValue, selected => selected == value,
            $"'{value}' selected", timeoutMs);

    #endregion

    #region Selection State - Core Methods

    /// <summary>The selected option's value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetSelectedValueCore(IHtmlElement? element)
        => element?.InputValue;

    /// <summary>The selected option's text.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetSelectedTextCore(IHtmlElement? element)
        => element?.TryFindElement(Locator.ByCss("option:checked"))?.Text?.Trim();

    /// <summary>The selected option's position (0-based); null when nothing is selected.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetSelectedIndexCore(IHtmlElement? element)
    {
        if (element == null)
            return null;

        var selected = GetSelectedTextCore(element);
        if (selected == null)
            return null;

        var texts = GetItemTextsCore(element);
        var index = texts?.ToList().IndexOf(selected) ?? -1;
        return index >= 0 ? index : null;
    }

    /// <summary>The options' texts, in order.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(IHtmlElement? element)
        => element?.FindElements(Options).Select(option => option.Text?.Trim() ?? string.Empty).ToList();

    /// <summary>How many options there are.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetItemCountCore(IHtmlElement? element)
        => GetItemTextsCore(element)?.Count;

    #endregion
}
