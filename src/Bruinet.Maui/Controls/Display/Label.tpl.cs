namespace Bruinet.Maui.Controls.Display;

using Bruinet.Maui.Interfaces;

/// <summary>
/// MAUI Label control for read-only text display.
/// Uses generated GetText(), AssertText(), and AssertTextContains() members.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Label<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new label control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the label element.</param>
    public Label(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new label control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Label(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Text - Core Methods

    /// <summary>
    /// Gets text from the label element, with support for nested text structures.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The text content, or null if not found.</returns>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.StartsWith
        | Comparison.EndsWith | Comparison.Empty)]
    protected virtual string? GetTextCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // The element resolves its own text. On Windows that means reading through a MAUI
        // wrapper to the nested Edit; the control does not need to know that.
        return element.Text;
    }

    #endregion
}
