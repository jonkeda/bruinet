using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Controls.Selection;

/// <summary>
/// A selector whose options are its own children, all shown at once (a radio group, tabs, a bottom
/// navigation bar): selecting taps the option once and confirms it is the selected one.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ChoiceGroupBase<TScope> : SelectorControlBase<TScope>
    where TScope : INativeAndroidScope<TScope>
{
    private static readonly Locator FirstText = Locator.ByClassName("android.widget.TextView");

    protected ChoiceGroupBase(INativeAndroidScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ChoiceGroupBase(INativeAndroidScope<TScope> scope, string resourceIdOrXPath)
        : base(scope, resourceIdOrXPath)
    {
    }

    /// <summary>The options, inside the group's view.</summary>
    protected abstract Locator ChoicesLocator { get; }

    /// <summary>Whether <paramref name="choice"/> is the selected option.</summary>
    protected abstract bool IsChosen(INativeAndroidElement choice);

    /// <summary>An option's name: its text, else its content description, else its first text view's text.</summary>
    protected virtual string? Label(INativeAndroidElement choice)
        => Present(choice.Text) ?? Present(choice.ContentDescription) ?? Present(choice.TryFindElement(FirstText)?.Text);

    /// <summary>Taps the option named <paramref name="text"/> once, and confirms it is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The option's name; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SelectByTextCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
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
    protected override void SelectByIndexCore(INativeAndroidElement element, int? index, int? timeoutMs = null)
    {
        if (index is not { } position)
            return;

        var choices = element.FindElements(ChoicesLocator);
        var choice = choices.ElementAtOrDefault(position)
            ?? throw new ArgumentOutOfRangeException(nameof(index), $"Option index {position} is out of range (0-{choices.Count - 1}). Locator: {Locator}");
        Choose(element, choice, Label(choice), timeoutMs);
    }

    private void Choose(INativeAndroidElement group, INativeAndroidElement choice, string? label, int? timeoutMs)
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
    protected override string? GetSelectedTextCore(INativeAndroidElement? element)
        => element?.FindElements(ChoicesLocator).FirstOrDefault(IsChosen) is { } chosen ? Label(chosen) : null;

    /// <summary>The options' names, in order. Reading them changes nothing.</summary>
    /// <param name="element">The pre-found view.</param>
    protected override IReadOnlyList<string>? GetItemTextsCore(INativeAndroidElement? element)
        => element?.FindElements(ChoicesLocator).Select(choice => Label(choice) ?? string.Empty).ToList();

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
