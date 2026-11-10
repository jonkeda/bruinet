using Bruinet.Maui.Controls.Display;

namespace Bruinet.Maui.CommunityToolkit.Controls.Views;

/// <summary>
/// The initials an <see cref="AvatarView{TScope}"/> shows when it has no image.
/// </summary>
/// <remarks>
/// On Windows this is the avatar's text child. When an image source is set the image replaces it,
/// so "no initials" is an answer here, not a failure: <c>GetInitials</c> reads once and returns
/// null rather than waiting for text that will not come.
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class AvatarInitials<TScope> : Label<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates the initials part within the specified scope.
    /// </summary>
    /// <param name="scope">The scope providing element finding, normally the avatar.</param>
    /// <param name="locator">The locator for the initials text.</param>
    public AvatarInitials(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates the initials part within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope providing element finding, normally the avatar.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID).</param>
    public AvatarInitials(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>
    /// Reads the initials shown.
    /// </summary>
    /// <param name="element">The pre-found element, or null when there is none.</param>
    /// <returns>The initials, or null when none are shown.</returns>
    [AbsenceTolerant]
    [GenerateComparisons(Comparison.EqualTo | Comparison.Empty)]
    protected virtual string? GetInitialsCore(IMauiElement? element)
        => element?.Visible == true ? element.Text : null;

    #endregion
}
