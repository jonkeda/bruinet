namespace Bruinet.Samples.Todo.UITests.Controls;

/// <summary>
/// The Next button of a <see cref="TodoStatus{TScope}"/>.
/// </summary>
/// <remarks>
/// A read-only status control (every list row) has no Next button at all, and that is an answer,
/// not a failure: <c>IsShown</c> is false for a missing button. Plain <c>IsVisible</c> reads null
/// for a missing element, and <c>Exists</c> may scroll the list and find another row's button.
/// </remarks>
/// <typeparam name="TScope">The containing scope type, normally the status control.</typeparam>
public partial class TodoStatusNextButton<TScope> : Button<TScope>
    where TScope : IMauiScope<TScope>
{
    /// <summary>Creates the Next button part within the specified scope.</summary>
    /// <param name="scope">The status control.</param>
    /// <param name="locator">The locator for the button.</param>
    public TodoStatusNextButton(IMauiScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    /// <summary>Creates the Next button part within the specified scope.</summary>
    /// <param name="scope">The status control.</param>
    /// <param name="locatorValue">The button's AutomationId.</param>
    public TodoStatusNextButton(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    #region Core Methods (Element-Aware, No Logging)

    /// <summary>Reads whether the button is shown.</summary>
    /// <param name="element">The pre-found button, or null when there is none.</param>
    /// <returns>True when shown; false when hidden or absent.</returns>
    [AbsenceTolerant]
    protected virtual bool? IsShownCore(IMauiElement? element) => element?.Visible == true;

    #endregion
}
