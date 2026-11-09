using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Text;

/// <summary>
/// A text field (<c>TextBox</c>): its text written through the Value pattern, in the background, and
/// confirmed. No keys are typed unless asked for (<c>SendKeys</c>).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TextBox<TScope> : FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public TextBox(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextBox(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Editable Text - Core Methods

    /// <summary>Replaces the text with <paramref name="text"/>, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(IWinFormsElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "SetText", () => element.SetValue(text),
            () => ShownText(element.Value), value => value == text,
            $"'{text}'", timeoutMs);
    }

    /// <summary>Enters <paramref name="text"/>: the same as <c>SetText</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(IWinFormsElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the field, and confirms it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(IWinFormsElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Clear", () => element.SetValue(string.Empty),
            () => ShownText(element.Value), value => string.IsNullOrEmpty(value),
            "an empty field", timeoutMs);
    }

    /// <summary>Adds <paramref name="text"/> after the current text, and confirms the field ends with it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void AppendCore(IWinFormsElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var expected = (ShownText(element.Value) ?? string.Empty) + text;
        ActAndConfirm(
            "Append", () => element.SetValue(expected),
            () => ShownText(element.Value), value => value == expected,
            $"'{expected}'", timeoutMs);
    }

    #endregion

    /// <summary>The text as the field shows it, from its Value; a control that reads back more (a line end) trims it.</summary>
    /// <param name="value">The Value pattern's text.</param>
    protected virtual string? ShownText(string? value) => value;

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>The field's placeholder: its help text, where the app sets one.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetPlaceholderCore(IWinFormsElement element)
    {
        return element.HelpText;
    }

    /// <summary>Whether the field cannot be typed in: its Value is read-only, or it is disabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsReadOnlyCore(IWinFormsElement? element)
    {
        return element == null ? null : element.IsReadOnly ?? !element.Enabled;
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
