using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeAndroid.Interfaces;

namespace Bruinet.NativeAndroid.Controls.Text;

/// <summary>
/// A text field (<c>android.widget.EditText</c>, <c>TextInputEditText</c>). Its text never reads as
/// its hint. A password field shows dots, so its text is confirmed by length.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class EditText<TScope> : FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : INativeAndroidScope<TScope>
{
    /// <summary>The Android key code for Enter.</summary>
    protected const int EnterKeyCode = 66;

    public EditText(INativeAndroidScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public EditText(INativeAndroidScope<TScope> scope, string resourceIdOrXPath)
        : base(scope, resourceIdOrXPath)
    {
    }

    #region Editable Text - Core Methods

    /// <summary>Replaces the text with <paramref name="text"/>, and confirms it took.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="text">The text; null skips.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SetTextCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
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
    protected virtual void EnterCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>Empties the field, and confirms it is empty.</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ClearCore(INativeAndroidElement element, int? timeoutMs = null)
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
    protected virtual void AppendCore(INativeAndroidElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        var before = element.Text ?? string.Empty;
        ActAndConfirm(
            "Append", () => element.SendKeys(text),
            () => element.Text, value => Shows(element, value, before + text),
            $"a field ending in '{Masked(element, text)}'", timeoutMs);
    }

    /// <summary>Presses Enter in the field: the keyboard's action (search, done, next).</summary>
    /// <param name="element">The pre-found view.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void SubmitCore(INativeAndroidElement element, int? timeoutMs = null)
    {
        Context.Driver.PressKeyCode(EnterKeyCode);
    }

    #endregion

    #region Placeholder and ReadOnly - Core Methods

    /// <summary>The field's hint.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual string? GetPlaceholderCore(INativeAndroidElement element)
    {
        return element.Hint;
    }

    /// <summary>Whether the field cannot be typed in: Android reports that as not enabled.</summary>
    /// <param name="element">The pre-found view.</param>
    protected virtual bool? IsReadOnlyCore(INativeAndroidElement? element)
    {
        return element == null ? null : !element.Enabled;
    }

    #endregion

    // A password field reads as dots: its text matches when its length does.
    private static bool Shows(INativeAndroidElement element, string? value, string expected)
        => IsPassword(element) ? value?.Length == expected.Length : value == expected;

    private static string Masked(INativeAndroidElement element, string text)
        => IsPassword(element) ? new string('•', text.Length) : text;

    private static bool IsPassword(INativeAndroidElement element)
        => string.Equals(element.GetAttribute("password"), "true", StringComparison.OrdinalIgnoreCase);

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
