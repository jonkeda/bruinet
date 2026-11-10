using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Controls;
using Bruinet.Html.Interfaces;

namespace Bruinet.Blazor.Controls;

/// <summary>
/// An &lt;img&gt; element.
/// </summary>
public partial class ImageControl<TScope> : ClickableControlBase<TScope>
    where TScope : IHtmlScope<TScope>
{
    public ImageControl(IHtmlScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public ImageControl(IHtmlScope<TScope> scope, string selectorOrId) : base(scope, selectorOrId) { }

    /// <summary>The image's <c>src</c>.</summary>
    /// <param name="element">The pre-found element.</param>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Contains)]
    protected virtual string? GetSourceCore(IHtmlElement element) => element.GetDomAttribute("src");

    /// <summary>The image's <c>alt</c> text.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual string? GetAltTextCore(IHtmlElement element) => element.GetDomAttribute("alt");

    /// <summary>Whether the image has loaded: complete, with a width.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsLoadedCore(IHtmlElement? element)
        => element?.Evaluate<bool>("img => img.complete && img.naturalWidth > 0");

    /// <summary>The image's own width, in pixels.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetNaturalWidthCore(IHtmlElement element) => element.Evaluate<int>("img => img.naturalWidth");

    /// <summary>The image's own height, in pixels.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual int? GetNaturalHeightCore(IHtmlElement element) => element.Evaluate<int>("img => img.naturalHeight");
}
