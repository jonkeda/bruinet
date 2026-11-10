using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Text;

/// <summary>
/// An edit text: its text written in the game (no keys, no selection, so the game's "a field has
/// focus" check stays false) and confirmed.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class EditText<TScope> : ClickableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : IStrideScope<TScope>
{
    public EditText(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public EditText(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Editable Text - Core Methods

    /// <summary>Replaces the text with <paramref name="text"/>, and confirms it took.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(IStrideElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "SetText", () => element.SetText(text),
            () => element.Text, value => value == text,
            $"'{text}'", timeoutMs);
    }

    /// <summary>Enters <paramref name="text"/>: the same as <c>SetText</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(IStrideElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the field, and confirms it is empty.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(IStrideElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Clear", () => element.SetText(string.Empty),
            () => element.Text, value => string.IsNullOrEmpty(value),
            "an empty field", timeoutMs);
    }

    /// <summary>Adds <paramref name="text"/> after the current text, and confirms the field ends with it.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void AppendCore(IStrideElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var expected = (element.Text ?? string.Empty) + text;
        ActAndConfirm(
            "Append", () => element.SetText(expected),
            () => element.Text, value => value == expected,
            $"'{expected}'", timeoutMs);
    }

    #endregion

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>A Stride edit text has no placeholder the game reports: always null.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetPlaceholderCore(IStrideElement element) => null;

    /// <summary>Whether the field cannot be typed in: the game reports that as not enabled.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsReadOnlyCore(IStrideElement? element) => element == null ? null : !element.Enabled;

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
