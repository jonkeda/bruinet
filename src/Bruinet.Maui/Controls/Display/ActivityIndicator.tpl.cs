namespace Bruinet.Maui.Controls.Display;

/// <summary>
/// MAUI ActivityIndicator control for displaying loading/busy state.
/// Provides IsRunning(), WaitRunning(), and running state assertions.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ActivityIndicator<TScope> : Base.ViewBase<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>
    /// Creates a new activity indicator control within the specified scope.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locator">The locator for the activity indicator element.</param>
    public ActivityIndicator(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>
    /// Creates a new activity indicator control within the specified scope using a string locator value.
    /// </summary>
    /// <param name="scope">The scope (page or container) providing element finding.</param>
    /// <param name="locatorValue">The locator value (e.g., automation ID, name).</param>
    public ActivityIndicator(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region IsRunning - Core Methods

    /// <summary>
    /// Checks if activity indicator is running using pre-found element.
    /// </summary>
    /// <param name="element">The pre-found element (may be null).</param>
    /// <returns>True if running, false otherwise, null if not found.</returns>
    protected virtual bool? IsRunningCore(IMauiElement? element)
    {
        if (element == null)
            return null;

        // No platform publishes IsRunning: a spinner is a view, not a state machine. Being on
        // screen is the only observable form of the question, and MAUI hides the indicator when
        // it stops.
        return element.Visible;
    }

    #endregion
}
