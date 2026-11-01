using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Controls;

/// <summary>
/// Base class for native Android controls that select one option: SelectByText, SelectByIndex,
/// GetSelectedText. The defaults drive a <c>Spinner</c>: a tap opens its popup list, a tap on an
/// option chooses it. A control built otherwise (a radio group, tabs) overrides the Cores.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class SelectorControlBase<TScope> : ClickableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : INativeAndroidScope<TScope>
{
    // A spinner's popup is a list in a window of its own, above the page.
    private static readonly Locator PopupList = Locator.ByXPath("//android.widget.ListView");

    private static readonly Locator SelectedTextView = Locator.ByClassName("android.widget.TextView");

    protected SelectorControlBase(INativeAndroidScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected SelectorControlBase(INativeAndroidScope<TScope> scope, string resourceIdOrXPath)
        : base(scope, resourceIdOrXPath)
    {
    }

    #region Selection - Core Methods

    /// <summary>Opens the popup, taps the option whose text is <paramref name="text"/>, and confirms it is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The option's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByTextCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        if (text == null || GetSelectedTextCore(element) == text)
            return;

        ActAndConfirm(
            "SelectByText", () => ChooseInPopup(element, options => options.FirstOrDefault(option => option.Text == text), $"'{text}'"),
            () => GetSelectedTextCore(AfterPopup(element)), selected => selected == text,
            $"'{text}' selected", timeoutMs);
    }

    /// <summary>Selects the option whose text is <paramref name="value"/>: an Android option has no value of its own.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="value">The option's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByValueCore(INativeAndroidElement element, string? value, int? timeoutMs = null)
    {
        SelectByTextCore(element, value, timeoutMs);
    }

    /// <summary>Opens the popup, taps the option at <paramref name="index"/> among those shown, and confirms its text is selected.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="index">The option's position (0-based); null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SelectByIndexCore(INativeAndroidElement element, int? index, int? timeoutMs = null)
    {
        if (index is not { } position)
            return;

        string? chosen = null;
        ActAndConfirm(
            "SelectByIndex",
            () => ChooseInPopup(element, options =>
            {
                var option = options.ElementAtOrDefault(position);
                chosen = option?.Text;
                return option;
            }, $"option {position}"),
            () => GetSelectedTextCore(AfterPopup(element)), selected => selected == chosen,
            $"option {position} selected", timeoutMs);
    }

    // One tap opens the popup and one tap chooses. An option that is not there closes the popup
    // and fails, naming what was there: the selection is never tried again (R0).
    private void ChooseInPopup(INativeAndroidElement element, Func<IReadOnlyList<INativeAndroidElement>, INativeAndroidElement?> pick, string what)
    {
        OpenPopup(element);
        var seen = new List<string>();

        // A scroll that reports "no further" still moved the list: read it once more after it.
        var more = true;
        while (Popup() is { } list)
        {
            var options = list.FindElements(OptionsLocator);
            seen.AddRange(options.Select(option => option.Text ?? string.Empty).Where(text => !seen.Contains(text)));
            if (pick(options) is { } option)
            {
                option.Click();
                return;
            }

            if (!more)
            {
                break;
            }

            more = list.Scroll(GestureDirection.Up);
        }

        if (Popup() != null)
        {
            Context.Driver.Back();
        }

        throw new InvalidOperationException($"{Locator} has no {what}: its options are {string.Join(", ", seen.Select(text => $"'{text}'"))}.");
    }

    private INativeAndroidElement? Popup() => Context.TryFindElement(PopupList);

    // Android re-issues a view's node when a window above it closes: the popup's closing left the
    // control's old node stale though the view is the same (measured, 2026-10-07). The effect is
    // read from the control found again by its locator; the selection is not repeated (R0).
    private INativeAndroidElement AfterPopup(INativeAndroidElement element) => TryFindElement() ?? element;

    // The popup animates in after the tap: confirm it opened. It tapped already, so a popup that
    // never opens fails the call; ElementNotReadyException would make the call tap again (R0).
    private void OpenPopup(INativeAndroidElement element)
    {
        element.Click();
        var opened = Confirm(Popup, popup => popup != null, CallRemainingMs);
        if (!opened.IsConfirmed)
        {
            throw new InvalidOperationException($"{Locator} was tapped and did not open its options. The tap ran once and is not repeated.");
        }
    }

    /// <summary>The options inside the popup list: its rows' text views.</summary>
    protected virtual Locator OptionsLocator => Locator.ByXPath("./*");

    #endregion

    #region Selection State - Core Methods

    /// <summary>The selected option's text: what the spinner shows.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetSelectedTextCore(INativeAndroidElement? element)
        => element == null ? null : element.TryFindElement(SelectedTextView)?.Text ?? element.Text;

    /// <summary>The selected option's text, as its value.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetSelectedValueCore(INativeAndroidElement? element)
        => GetSelectedTextCore(element);

    /// <summary>The selected option's position (0-based); null when it is not among the options.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual int? GetSelectedIndexCore(INativeAndroidElement? element)
    {
        if (element == null || GetSelectedTextCore(element) is not { } selected)
            return null;

        var index = GetItemTextsCore(element)?.ToList().IndexOf(selected) ?? -1;
        return index >= 0 ? index : null;
    }

    /// <summary>
    /// The options' texts, in order: opens the popup, reads it to its end, and closes it with Back.
    /// </summary>
    /// <param name="element">The pre-found view.</param>
    [GenerateComparisons(Comparison.SequenceEquals | Comparison.HasItem | Comparison.Count)]
    protected virtual IReadOnlyList<string>? GetItemTextsCore(INativeAndroidElement? element)
    {
        if (element == null)
            return null;

        OpenPopup(element);
        var texts = new List<string>();
        var more = true;
        while (Popup() is { } list)
        {
            texts.AddRange(list.FindElements(OptionsLocator).Select(option => option.Text ?? string.Empty).Where(text => !texts.Contains(text)));
            if (!more)
            {
                break;
            }

            more = list.Scroll(GestureDirection.Up);
        }

        if (Popup() != null)
        {
            Context.Driver.Back();
        }

        return texts;
    }

    /// <summary>How many options there are.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual int? GetItemCountCore(INativeAndroidElement? element)
        => GetItemTextsCore(element)?.Count;

    #endregion
}
