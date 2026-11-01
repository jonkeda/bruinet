namespace Bruinet.Maui.Controls.Text;

/// <summary>
/// MAUI SearchBar control for search text input with search action support.
/// Inherits all text manipulation from Entry, adds search-specific methods.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class SearchBar<TScope> : Entry<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new search bar control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the search bar element.</param>
    public SearchBar(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new search bar control within the specified scope using a string locator value.
    /// Uses the scope's DefaultLocatorStrategy to create the locator.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public SearchBar(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Search-Specific Core Methods

    /// <summary>
    /// Core implementation of Search using pre-found element.
    /// Enters the search text and submits it.
    /// </summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="searchText">The text to search for. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void SetSearchCore(IMauiElement element, string? searchText, int? timeoutMs = null)
    {
        if (searchText == null)
            return;

        SetTextCore(element, searchText, timeoutMs);
        SubmitSearchCore(element, timeoutMs);
    }

    /// <summary>
    /// Core implementation for submitting search.
    /// Platform-specific: sends Enter key to trigger search action.
    /// </summary>
    /// <param name="element">The search bar element.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    protected virtual void SubmitSearchCore(IMauiElement element, int? timeoutMs = null)
    {
        element.Submit();
    }

    #endregion

    #region Text Override for Nested TextBox

    /// <summary>
    /// Gets the text using nested TextBox discovery for Windows SearchBar.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The text content, or null if not found.</returns>
    protected override string? GetTextCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        return base.GetTextCore(element);
    }

    #endregion

    #region Hand-written Convenience Members

    /// <summary>
    /// Enters text and submits the search.
    /// </summary>
    /// <param name="searchText">The text to search for. Null skips the operation.</param>
    /// <param name="timeoutMs">Optional timeout.</param>
    /// <returns>The containing scope for fluent chaining.</returns>
    public TScope Search(string? searchText, int? timeoutMs = null)
        => SetSearch(searchText, timeoutMs);

    #endregion
}
