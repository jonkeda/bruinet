namespace Bruinet.Maui.Controls.Display;

/// <summary>
/// MAUI Image control for displaying images with source and dimension access.
/// Provides IsLoaded(), GetSource(), GetWidth(), GetHeight(), and image assertions.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Image<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new image control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the image element.</param>
    public Image(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new image control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public Image(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Source - Core Methods


    #endregion

    #region IsLoaded - Core Methods

    /// <summary>
    /// Whether the image has finished loading a bitmap.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>True if loaded, false otherwise, null if not found.</returns>
    protected virtual bool? IsLoadedCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // A null Source means the app does not answer here, so fall back to the size check.
        if (element.ReadState("Source") is { } source)
        {
            // Loaded means: a source to load, and not still loading it. Either alone is the
            // wrong question - an image with no source is never loading and never loaded.
            var hasSource = !string.IsNullOrEmpty(source);
            var loading = bool.TryParse(element.ReadState("IsLoading"), out var busy) && busy;

            return hasSource && !loading;
        }

        var size = element.Size;
        return size.Width > 0 && size.Height > 0;
    }

    /// <summary>
    /// Where the image's bitmap comes from, as the app declared it.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>The source, or null when it cannot be read.</returns>
    protected virtual string? GetSourceCore(IMauiElement? element)
        => element?.ReadState("Source");

    #endregion

    #region Dimensions - Core Methods

    /// <summary>
    /// Gets the rendered width of the image from the pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>Width in pixels, or null if element not found.</returns>
    protected virtual int? GetWidthCore(IMauiElement? element)
    {
        return element?.Size.Width;
    }

    /// <summary>
    /// Gets the rendered height of the image from the pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>Height in pixels, or null if element not found.</returns>
    protected virtual int? GetHeightCore(IMauiElement? element)
    {
        return element?.Size.Height;
    }

    #endregion

    #region Hand-written Convenience Members

    #endregion
}
