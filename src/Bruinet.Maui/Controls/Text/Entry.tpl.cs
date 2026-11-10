using Bruinet.Core;

namespace Bruinet.Maui.Controls.Text;

/// <summary>
/// MAUI Entry control with text input capability and fluent method chaining.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Entry<TScope> : Base.FocusableControlBase<TScope>, IEditableTextControlObject<TScope>
    where TScope : IMauiScope<TScope>
{
    public Entry(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Entry(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Text - Core Methods

    /// <summary>
    /// Gets the text of the element using pre-found element.
    /// Override in derived classes for platform-specific text retrieval.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The element text, or null if element is null.</returns>
    [GenerateComparisons(Comparison.Equals | Comparison.Contains | Comparison.StartsWith
        | Comparison.EndsWith | Comparison.Empty)]
    protected virtual string? GetTextCore(IMauiElement? element)
    {
        return element?.Text;
    }

    #endregion

    #region Editable Text - Core Methods

    /// <summary>
    /// Core implementation of Enter using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text to enter.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    [SkipGeneration("Enter is hand-written below so a null text skips without resolving an element.")]
    protected virtual void EnterCore(IMauiElement element, string? text, int? timeoutMs = null)
    {
        SetTextCore(element, text, timeoutMs);
    }

    /// <summary>
    /// Core implementation of Clear using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected virtual void ClearCore(IMauiElement element, int? timeoutMs = null)
    {
        element.Clear();
    }

    /// <summary>
    /// Core implementation of SetText using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text to set.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected virtual void SetTextCore(IMauiElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        element.Clear();
        // SetValue writes through the platform's own value mechanism, which needs no commit
        // keystroke: no trailing Tab.
        element.SendKeys(text, TextInputMethod.SetValue);
    }

    /// <summary>
    /// Core implementation of Append using pre-found element.
    /// Appends text without clearing existing content.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="text">The text to append.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected virtual void AppendCore(IMauiElement element, string? text, int? timeoutMs = null)
    {
        if (text == null)
            return;

        element.AppendText(text);
    }

    /// <summary>
    /// Core implementation of Submit using pre-found element.
    /// Drives MAUI Entry.Completed command paths such as search boxes.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout for enabled check.</param>
    protected virtual void SubmitCore(IMauiElement element, int? timeoutMs = null)
    {
        element.Submit();
    }

    #endregion

    #region Placeholder - Core Methods

    /// <summary>
    /// Gets the placeholder text using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The placeholder text, or null if not found.</returns>
    protected virtual string? GetPlaceholderCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // The hint first; an empty Entry on Windows also reports its placeholder as its name.
        return element.Hint ?? element.Name;
    }

    #endregion

    #region ReadOnly - Core Methods

    /// <summary>
    /// Checks if element is read-only using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>True if read-only, false if editable, null if element not found.</returns>
    [SkipGeneration("Hand-written below: IsReadOnly keeps the null of a platform that publishes no editability.")]
    protected virtual bool? IsReadOnlyCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // Windows answers from the Value pattern; Android and iOS publish no editability and answer null.
        return element?.IsReadOnly;
    }

    /// <summary>Whether the entry is read-only; null when the platform cannot tell.</summary>
    public bool? IsReadOnly() => IsReadOnlyCore(TryFindElement());

    public bool WaitReadOnly(bool? expected = true, int? timeoutMs = null)
        => RunWaitWithElement(expected,
            element => IsReadOnlyCore(element) == expected!.Value,
            timeoutMs);

    public TScope AssertReadOnly(bool? expected = true, string? message = null, int? timeoutMs = null)
        => RunAssertWithElement(expected,
            IsReadOnlyCore, (actual, expected1) => actual == expected1,
            message ?? $"Expected ReadOnly to be '{expected}'. Locator: {Locator}", timeoutMs);

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Enters text, doing nothing when the text is null.
    /// </summary>
    /// <remarks>
    /// A null value returns the scope without resolving an element.
    /// </remarks>
    /// <param name="text">The text to enter, or null to skip.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    /// <returns>The containing scope, for chaining.</returns>
    public TScope Enter(string? text, int? timeoutMs = null)
    {
        return RunSetWithElement(text, element => EnterCore(element, text, timeoutMs), timeoutMs);
    }


    #endregion
}
