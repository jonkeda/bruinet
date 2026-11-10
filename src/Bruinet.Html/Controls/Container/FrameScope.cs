using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Container;

/// <summary>
/// The document inside an <c>&lt;iframe&gt;</c>, as a scope (?A6, design 7.5): controls declared
/// on it are found in the frame, and its readiness is on their path. It stays in the same test
/// context, unlike the old <c>ForFrame</c>, which made a new one.
/// </summary>
/// <remarks>
/// The root is the frame document's <c>&lt;body&gt;</c>, reached through the iframe element in the
/// parent. Until the frame has loaded a body, the scope reports its root missing and a call on it
/// waits. Works for same-origin and cross-origin frames alike.
/// </remarks>
public class FrameScope<TParent> : ContainerBase<TParent, FrameScope<TParent>>
    where TParent : IHtmlScope<TParent>
{
    /// <summary>The frame found by <paramref name="frameLocator"/> in <paramref name="parent"/>.</summary>
    public FrameScope(IHtmlScope<TParent> parent, Locator frameLocator)
        : base(parent, frameLocator)
    {
    }

    public override FrameScope<TParent> Self => this;

    protected override IHtmlElement? TryFindRootElement()
        => ParentScope.TryFindElement(Locator)?.TryGetFrameDocument();
}
