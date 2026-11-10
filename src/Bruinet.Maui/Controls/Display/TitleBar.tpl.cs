namespace Bruinet.Maui.Controls.Display;

using Bruinet.Maui.Interfaces;

/// <summary>
/// MAUI TitleBar control for customizing window title bar appearance and behavior.
/// TitleBar is used to customize the window decoration and is platform-specific.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TitleBar<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new TitleBar control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the TitleBar element.</param>
    public TitleBar(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new TitleBar control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public TitleBar(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Text - Core Methods

    /// <summary>
    /// Gets text from the title bar element, with support for nested text structures.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The text content, or null if not found.</returns>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains | Comparison.StartsWith
        | Comparison.EndsWith | Comparison.Empty)]
    protected virtual string? GetTextCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // The element resolves its own text, including through a nested structure where the
        // platform uses one.
        return element.Text;
    }

    #endregion
}
