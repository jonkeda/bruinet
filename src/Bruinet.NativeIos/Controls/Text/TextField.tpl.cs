using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Text;

/// <summary>
/// A text field (<c>UITextField</c>, XCUITest type <c>TextField</c>). Its text never reads as its
/// placeholder. A secure field shows dots, so its text is confirmed by length.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TextField<TScope> : FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    public TextField(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextField(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    #region Editable Text - Core Methods

    /// <summary>Replaces the text with <paramref name="text"/>, and confirms it took.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(INativeIosElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        ActAndConfirm(
            "SetText",
            () =>
            {
                element.Clear();
                element.SendKeys(text);
            },
            () => element.Text, value => Shows(element, value, text),
            $"'{Masked(element, text)}'", timeoutMs);
    }

    /// <summary>Enters <paramref name="text"/>: the same as <c>SetText</c>.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(INativeIosElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the field, and confirms it is empty.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(INativeIosElement element, int? timeoutMs = null)
    {
        ActAndConfirm(
            "Clear", element.Clear,
            () => element.Text, value => string.IsNullOrEmpty(value),
            "an empty field", timeoutMs);
    }

    /// <summary>Types <paramref name="text"/> after the current text, and confirms the field ends with it.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void AppendCore(INativeIosElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var before = element.Text ?? string.Empty;
        ActAndConfirm(
            "Append", () => element.SendKeys(text),
            () => element.Text, value => Shows(element, value, before + text),
            $"a field ending in '{Masked(element, text)}'", timeoutMs);
    }

    /// <summary>Presses Return in the field: the keyboard's action (search, done, next).</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SubmitCore(INativeIosElement element, int? timeoutMs = null)
    {
        element.SendKeys("\n");
    }

    #endregion

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>The field's placeholder.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetPlaceholderCore(INativeIosElement element)
    {
        return element.Placeholder;
    }

    /// <summary>Whether the field cannot be typed in: XCUITest reports that as not enabled.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsReadOnlyCore(INativeIosElement? element)
    {
        return element == null ? null : !element.Enabled;
    }

    #endregion

    // A secure field reads as dots: its text matches when its length does.
    private static bool Shows(INativeIosElement element, string? value, string expected)
        => IsPassword(element) ? value?.Length == expected.Length : value == expected;

    private static string Masked(INativeIosElement element, string text)
        => IsPassword(element) ? new string('•', text.Length) : text;

    private static bool IsPassword(INativeIosElement element)
        => element.ElementType == "XCUIElementTypeSecureTextField";

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
