using Bruinet.Maui.Containers;
using Bruinet.Maui.Controls.Display;

namespace Bruinet.Maui.CommunityToolkit.Controls.Views;

/// <summary>
/// CommunityToolkit.Maui <c>AvatarView</c>: initials or an image in a bordered shape.
/// </summary>
/// <remarks>
/// <para>
/// Read from the automation tree alone, no bridge: on Windows the avatar is a group whose child
/// is a text element carrying the initials, or an image element when an image source is set.
/// </para>
/// <para>
/// The parts own the reads; this component forwards to them through shortcuts, so each call is
/// the part's own single unit of work.
/// </para>
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class AvatarView<TScope> : ComponentObjectBase<TScope, AvatarView<TScope>>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates an avatar view control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the avatar element.</param>
    public AvatarView(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates an avatar view control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID).</param>
    public AvatarView(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Parts

    private static readonly Locator InitialsLocator = Locator.ByControlType("text");
    private static readonly Locator ImageLocator = Locator.ByControlType("image");

    /// <summary>The initials, shown when no image is.</summary>
    public AvatarInitials<AvatarView<TScope>> Initials => new(this, InitialsLocator);

    /// <summary>The image, when an image source is set.</summary>
    public AvatarImage<AvatarView<TScope>> Image => new(this, ImageLocator);

    #endregion

    #region Shortcuts

    /// <summary>The initials the avatar shows; null while an image replaces them.</summary>
    [GenerateComparisons(Comparison.EqualTo | Comparison.Empty)]
    protected string? GetInitialsShortcut() => Initials.GetInitials();

    /// <summary>
    /// Whether the avatar shows an image rather than initials.
    /// </summary>
    protected bool? IsShowingImageShortcut() => Image.IsShown();

    #endregion
}
