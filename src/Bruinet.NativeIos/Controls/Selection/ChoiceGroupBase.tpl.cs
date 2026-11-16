using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Selection;

/// <summary>
/// A selector whose options are its own buttons, all shown at once (a segmented control, a tab
/// bar): selecting taps the option once and confirms it is the selected one.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ChoiceGroupBase<TScope> : SelectorControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    protected ChoiceGroupBase(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ChoiceGroupBase(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    /// <summary>The options, inside the group's view.</summary>
    protected abstract Locator ChoicesLocator { get; }

    /// <summary>Whether <paramref name="choice"/> is the selected option.</summary>
    protected abstract bool IsChosen(INativeIosElement choice);

    /// <summary>An option's name: its label, else its identifier.</summary>
    protected virtual string? Label(INativeIosElement choice)
        => Present(choice.Label) ?? Present(choice.Identifier);

    /// <summary>Taps the option named <paramref name="text"/> once, and confirms it is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The option's name; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SelectByTextCore(INativeIosElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var choices = element.FindElements(ChoicesLocator);
        var choice = choices.FirstOrDefault(candidate => Label(candidate) == text)
            ?? throw new InvalidOperationException($"{Locator} has no option '{text}': its options are {string.Join(", ", choices.Select(candidate => $"'{Label(candidate)}'"))}.");
        Choose(element, choice, text, timeoutMs);
    }

    /// <summary>Taps the option at <paramref name="index"/> once, and confirms it is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="index">The option's position (0-based); null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SelectByIndexCore(INativeIosElement element, int? index, int? timeoutMs = null)
    {
        if (index is not { } position)
            return;

        var choices = element.FindElements(ChoicesLocator);
        var choice = choices.ElementAtOrDefault(position)
            ?? throw new ArgumentOutOfRangeException(nameof(index), $"Option index {position} is out of range (0-{choices.Count - 1}). Locator: {Locator}");
        Choose(element, choice, Label(choice), timeoutMs);
    }

    private void Choose(INativeIosElement group, INativeIosElement choice, string? label, int? timeoutMs)
    {
        if (IsChosen(choice))
        {
            return;
        }

        ActAndConfirm(
            "Select", choice.Click,
            () => GetSelectedTextCore(group), selected => selected == label,
            $"'{label}' selected", timeoutMs);
    }

    /// <summary>The selected option's name; null when none is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    protected override string? GetSelectedTextCore(INativeIosElement? element)
        => element?.FindElements(ChoicesLocator).FirstOrDefault(IsChosen) is { } chosen ? Label(chosen) : null;

    /// <summary>The options' names, in order. Reading them changes nothing.</summary>
    /// <param name="element">The pre-found view.</param>
    protected override IReadOnlyList<string>? GetItemTextsCore(INativeIosElement? element)
        => element?.FindElements(ChoicesLocator).Select(choice => Label(choice) ?? string.Empty).ToList();

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
