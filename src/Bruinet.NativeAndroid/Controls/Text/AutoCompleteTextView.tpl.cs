using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Controls.Text;

/// <summary>
/// A text field that offers suggestions (<c>AutoCompleteTextView</c>, Material's exposed dropdown
/// menu). Android filters only for a focused field, so typing taps it first; a suggestion is chosen
/// by its text from the list it shows in a window of its own.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class AutoCompleteTextView<TScope> : EditText<TScope>
    where TScope : INativeAndroidScope<TScope>
{
    // The suggestions are a list in a popup window above the page.
    private static readonly Locator Suggestions = Locator.ByXPath("//android.widget.ListView/*");

    public AutoCompleteTextView(INativeAndroidScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public AutoCompleteTextView(INativeAndroidScope<TScope> scope, string resourceIdOrXPath)
        : base(scope, resourceIdOrXPath)
    {
    }

    /// <summary>Focuses the field, then sets its text, so it filters its suggestions.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void SetTextCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        FocusFirst(element);
        base.SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Focuses the field, then types after its text, so it filters its suggestions.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected override void AppendCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        FocusFirst(element);
        base.AppendCore(element, text, timeoutMs);
    }

    /// <summary>Taps the suggestion whose text is <paramref name="text"/>, and confirms the field took it.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The suggestion's text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ChooseSuggestionCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        // Not tapped yet: a list still filtering is "not ready", and the call asks again.
        var suggestion = Context.FindElements(Suggestions).FirstOrDefault(row => row.Text == text || row.FindElements(Locator.ByText(text)).Count > 0)
            ?? throw new ElementNotReadyException(Locator, NotReadyReason.Other, $"no suggestion '{text}' is shown");
        ActAndConfirm(
            "ChooseSuggestion", suggestion.Click,
            () => element.Text, value => value == text,
            $"'{text}'", timeoutMs);
    }

    private void FocusFirst(INativeAndroidElement element)
    {
        if (!element.Focused)
        {
            element.Click();
        }
    }
}
