using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Text;

/// <summary>
/// HTML text input control. Wraps &lt;input type="text|email|password|search|tel|url"&gt;. Its text
/// is its value.
/// </summary>
public partial class TextInputControl<TScope> : FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    public TextInputControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextInputControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }

    #region Text - Core Methods

    /// <summary>An input's text is its value.</summary>
    /// <param name="element">The pre-found element.</param>
    protected override string? GetTextCore(IHtmlElement element)
    {
        return element.InputValue;
    }

    /// <summary>The input's value.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.Empty)]
    protected virtual string? GetValueCore(IHtmlElement element)
    {
        return element.InputValue;
    }

    #endregion

    #region Editable Text - Core Methods

    /// <summary>Replaces the value with <paramref name="text"/> (Playwright's Fill), and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        // A browser waits for an editable field and times out; bUnit would write. Both refuse at once (review 04).
        if (element.GetAttribute("readonly") is not null)
            throw new Bruinet.Core.Exceptions.ActionRefusedException($"'{Locator}' is read-only: it takes no text.");

        ActAndConfirm(
            "Fill", () => element.Fill(text),
            () => element.InputValue, value => value == text,
            $"'{text}'", timeoutMs);
    }

    /// <summary>Enters <paramref name="text"/>: the same as <c>SetText</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the value, and confirms it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(IHtmlElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Clear", element.Clear,
            () => element.InputValue, value => string.IsNullOrEmpty(value),
            "an empty value", timeoutMs);
    }

    /// <summary>Types <paramref name="text"/> after the current value, and confirms the value ends with it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void AppendCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "Append",
            () =>
            {
                element.Focus();
                element.SendKeys(text);
            },
            () => element.InputValue, value => value?.EndsWith(text, StringComparison.Ordinal) == true,
            $"a value ending in '{text}'", timeoutMs);
    }

    /// <summary>
    /// Types <paramref name="text"/> key by key (for inputs with keystroke handlers), and confirms
    /// the value contains it.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void TypeTextCore(IHtmlElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "SendKeys", () => element.SendKeys(text),
            () => element.InputValue, value => value?.Contains(text, StringComparison.Ordinal) == true,
            $"a value containing '{text}'", timeoutMs);
    }

    /// <summary>Submits the input's form.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SubmitCore(IHtmlElement element, int? timeoutMs = null)
    {
        element.Submit();
    }

    #endregion

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>The input's placeholder.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetPlaceholderCore(IHtmlElement element)
    {
        return element.GetAttribute("placeholder");
    }

    /// <summary>Whether the input is read-only.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsReadOnlyCore(IHtmlElement? element)
    {
        return element == null ? null : element.GetAttribute("readonly") != null;
    }

    #endregion

    #region Hand-written Convenience Members

    /// <summary>Enters text, doing nothing when the text is null.</summary>
    /// <param name="text">The text to enter, or null to skip.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope, for chaining.</returns>
    public TScope Enter(string? text, int? timeoutMs = null)
    {
        return RunSetWithElement(text, element => EnterCore(element, text, timeoutMs), timeoutMs);
    }

    #endregion
}
