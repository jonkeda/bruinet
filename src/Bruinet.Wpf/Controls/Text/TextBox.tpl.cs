using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Text;

/// <summary>
/// A text field (<c>TextBox</c>): its text written through the Value pattern, in the background, and
/// confirmed. No keys are typed unless asked for (<c>SendKeys</c>).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TextBox<TScope> : FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    public TextBox(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextBox(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Editable Text - Core Methods

    /// <summary>Replaces the text with <paramref name="text"/>, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "SetText", () => element.SetValue(text),
            () => element.Value, value => value == text,
            $"'{text}'", timeoutMs);
    }

    /// <summary>Enters <paramref name="text"/>: the same as <c>SetText</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the field, and confirms it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(IWpfElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Clear", () => element.SetValue(string.Empty),
            () => element.Value, value => string.IsNullOrEmpty(value),
            "an empty field", timeoutMs);
    }

    /// <summary>Adds <paramref name="text"/> after the current text, and confirms the field ends with it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void AppendCore(IWpfElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var expected = (element.Value ?? string.Empty) + text;
        ActAndConfirm(
            "Append", () => element.SetValue(expected),
            () => element.Value, value => value == expected,
            $"'{expected}'", timeoutMs);
    }

    #endregion

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>The field's placeholder: its help text, where the app sets one.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetPlaceholderCore(IWpfElement element)
    {
        return element.HelpText;
    }

    /// <summary>Whether the field cannot be typed in: its Value is read-only, or it is disabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsReadOnlyCore(IWpfElement? element)
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
