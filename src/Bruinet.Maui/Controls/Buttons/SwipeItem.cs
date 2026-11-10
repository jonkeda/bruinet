namespace Bruinet.Maui.Controls.Buttons;

/// <summary>
/// A MAUI <c>SwipeItem</c>: an action a <c>SwipeView</c> uncovers when its content is swiped.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
/// <remarks>
/// <para>
/// Swipe the row open first: iOS creates the item's button only when the SwipeView opens.
/// </para>
/// <para>
/// iOS reports the open item's button hidden and disabled, because the SwipeView, not the button,
/// takes the tap. A click therefore does not wait for the item to be visible or enabled; waiting
/// would scroll, and scrolling closes the row.
/// </para>
/// </remarks>
public class SwipeItem<TScope> : Button<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>Creates a swipe item within the specified scope.</summary>
    /// <param name="scope">The scope, normally the list or row that holds the SwipeView.</param>
    /// <param name="locator">The locator for the swipe item.</param>
    public SwipeItem(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>Creates a swipe item found by its AutomationId as an accessibility id.</summary>
    /// <remarks>
    /// MAUI does not publish a SwipeItem's <c>AutomationId</c> as a view's: Android writes it as
    /// the button's content description, not its resource-id, so the item is found by
    /// accessibility id. iOS writes neither; the app maps it to the accessibility identifier.
    /// </remarks>
    /// <param name="scope">The scope, normally the list or row that holds the SwipeView.</param>
    /// <param name="automationId">The SwipeItem's AutomationId.</param>
    public SwipeItem(IMauiScope<TScope> scope, string automationId)
        : base(scope, Locator.ByAccessibilityId(automationId))
    {
    }

    /// <inheritdoc />
    protected override bool RequiresVisibilityForAction => false;

    /// <inheritdoc />
    protected override void EnsureClickableCore(IMauiElement element)
    {
    }
}
