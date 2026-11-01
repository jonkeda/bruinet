namespace Bruinet.Maui.Controls.Media;

/// <summary>
/// MAUI WebView control for displaying web content.
/// Provides methods for URL navigation and web content interaction.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class WebView<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a WebView control with locator.
    /// </summary>
    public WebView(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a WebView control with automation ID.
    /// </summary>
    public WebView(IMauiScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Gets the current URL of the WebView.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The current URL, or null if not available.</returns>
    [GenerateComparisons(Comparison.Equals | Comparison.Contains)]
    protected virtual string? GetUrlCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        return element.Text;
    }

    /// <summary>
    /// Gets the title of the currently loaded page.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The page title, or null if not available.</returns>
    [GenerateComparisons(Comparison.Equals | Comparison.Contains)]
    protected virtual string? GetPageTitleCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        var title = element.Name;
        if (!string.IsNullOrEmpty(title))
            return title;

        return element.Text;
    }



    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Asserts that the current URL contains the specified text, case-insensitively.
    /// </summary>
    /// <remarks>
    /// Kept alongside the generated <c>AssertUrlContains</c>-equivalent because URLs are
    /// compared case-insensitively here; the generated comparison is ordinal. Named
    /// <c>AssertUrlContainsIgnoreCase</c> so the two do not collide and the difference is
    /// visible at the call site.
    /// </remarks>
    /// <param name="expectedUrlPart">The expected URL part.</param>
    /// <param name="message">Optional assertion message.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope AssertUrlContainsIgnoreCase(string expectedUrlPart, string? message = null)
    {
        return RunAssertWithElement(expectedUrlPart,
            GetUrlCore,
            (actual, exp) => actual?.Contains(expectedUrlPart, StringComparison.OrdinalIgnoreCase) == true,
            message);
    }

    #endregion
}
