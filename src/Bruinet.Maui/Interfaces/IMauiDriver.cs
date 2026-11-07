using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;

namespace Bruinet.Maui.Interfaces;

/// <summary>
/// The driver for a MAUI app under test: lookup from the app root, platform detection, context
/// switching for hybrid apps, window management, navigation and gestures.
/// </summary>
public interface IMauiDriver : IDiagnosticDriver, IDisposable
{
    #region Lookup

    /// <summary>
    /// Finds every element in the app matching <paramref name="locator"/>, in one attempt.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <returns>The matches; empty when none match now.</returns>
    IReadOnlyList<IMauiElement> FindElements(Locator locator);

    /// <summary>
    /// Finds the first element in the app matching <paramref name="locator"/>, in one attempt:
    /// the same element as the first of <see cref="FindElements"/>.
    /// </summary>
    /// <remarks>
    /// A driver whose platform can stop at the first match overrides this. On Windows a page root
    /// costs 40 ms to collect app-wide and 7 ms to find first, and every navigation looks one up.
    /// </remarks>
    /// <param name="locator">The locator strategy and value.</param>
    /// <returns>The first match, or null when none match now.</returns>
    IMauiElement? TryFindElement(Locator locator)
    {
        var elements = FindElements(locator);
        return elements.Count > 0 ? elements[0] : null;
    }

    #endregion

    #region Session

    /// <summary>Closes the current window.</summary>
    void Close();

    /// <summary>Ends the session and closes the app.</summary>
    void Quit();

    /// <summary>Takes a screenshot of the app.</summary>
    byte[] GetScreenshot();

    #endregion

    #region Platform

    /// <summary>
    /// Gets the target platform (Windows, Android, iOS, macOS).
    /// </summary>
    MauiPlatform Platform { get; }

    #endregion

    #region The app

    /// <summary>
    /// The app itself, as an element: the application window on Windows, the hierarchy root on
    /// Android and iOS.
    /// </summary>
    IMauiElement AppElement { get; }

    /// <summary>
    /// The culture the app formats dates and times in, where it is not this process's; null where
    /// the app runs in this process's culture, as a Windows app on this machine does.
    /// </summary>
    /// <remarks>
    /// A device formats with its own locale, untouched by this machine's settings: the emulator
    /// shows "10/25/2026" while a Windows account with a custom short date reads "dd-MMM-yy", and a
    /// picker read with the machine's format found no date at all.
    /// </remarks>
    System.Globalization.CultureInfo? DeviceCulture => null;

    /// <summary>Where this platform draws MAUI Shell's tabs and flyout.</summary>
    /// <exception cref="PlatformNotSupportedException">Shell has not been mapped on this platform.</exception>
    ShellChromeLocators ShellChrome
        => throw new PlatformNotSupportedException(
            $"Shell chrome has not been mapped for {GetType().Name}. Dump the tree and map it in ShellChrome.");

    #endregion

    #region What the platform's tree is like

    // A control asks these by name instead of branching on the platform (AD-010): the driver knows
    // its platform, the control what it needs. Each default is the Windows (UI Automation) answer.

    /// <summary>
    /// Whether the tree leaves out what is off screen (UiAutomator2, XCUITest), so a miss is found
    /// by scrolling; Windows keeps an off-screen element in its tree, and a miss there is an answer.
    /// </summary>
    bool OmitsOffscreenElements => false;

    /// <summary>
    /// Whether a found element counts as visible, also under a dialog (UiAutomator2), so an action
    /// asks whether a dialog covers it.
    /// </summary>
    bool FoundMeansVisible => false;

    /// <summary>
    /// The XPath to an element's children: UiAutomator2 roots an element's XPath at the element,
    /// so its children are <c>./*/*</c> there.
    /// </summary>
    Locator ChildrenPath => Locator.ByXPath("./*");

    /// <summary>
    /// Where a dialog's buttons are by role, positive to negative; empty where they are read as the
    /// dialog's buttons by type.
    /// </summary>
    IReadOnlyList<Locator> AlertButtons => [];

    /// <summary>
    /// Whether a CommunityToolkit RatingView is drawn, publishing no value to select by index, so
    /// a star is tapped on screen (Android).
    /// </summary>
    bool DrawsRatingStars => false;

    /// <summary>
    /// Whether a list row is selected through the row that contains it (UI Automation's
    /// SelectionItem on Windows), where elsewhere the row's own element is tapped.
    /// </summary>
    bool ActivatesRowsThroughContainers => false;

    /// <summary>
    /// An IndicatorView's dots, each described "Item 2 of 5" and ", selected" on the current one
    /// (Android); null where they are not in the tree.
    /// </summary>
    Locator? IndicatorDots => null;

    /// <summary>
    /// The page control an IndicatorView is drawn as, whose value reads "page 2 of 5" (iOS), and
    /// its element type, for a view that is the control itself; null where there is none.
    /// </summary>
    (Locator Within, string Type)? PageControl => null;

    /// <summary>
    /// Whether a two-button alert lists its cancel action first (UIKit puts it on the left), so its
    /// buttons read negative to positive.
    /// </summary>
    bool ListsCancelFirstInTwoButtonAlerts => false;

    /// <summary>
    /// Whether a list row's bounds are its full frame, also where the list's top edge cuts it off
    /// (iOS), so a first row starting at or below that edge means the list shows its top. Where a
    /// row's bounds are clipped to what is shown (Android), that cannot be told.
    /// </summary>
    bool ReportsFullRowFrames => false;

    #endregion

    #region Context Switching (Hybrid Apps)

    /// <summary>
    /// Gets or sets the current context (NATIVE_APP, WEBVIEW_*, etc.).
    /// </summary>
    string Context { get; set; }

    /// <summary>
    /// Gets all available contexts.
    /// </summary>
    IReadOnlyCollection<string> Contexts { get; }

    #endregion

    #region Window Management

    /// <summary>
    /// Gets the current window handle.
    /// </summary>
    string CurrentWindowHandle { get; }

    /// <summary>
    /// Gets all window handles.
    /// </summary>
    IReadOnlyCollection<string> WindowHandles { get; }

    #endregion

    #region Gestures

    /// <summary>
    /// Whether a gesture can be performed semantically on the element with this
    /// <c>AutomationId</c>.
    /// </summary>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target element.</param>
    /// <param name="gesture">The gesture to ask about.</param>
    /// <returns>Whether <see cref="PerformGesture(string, MauiGesture)"/> would work.</returns>
    bool SupportsGesture(string automationId, MauiGesture gesture) => false;

    /// <summary>Performs a gesture on the element with this <c>AutomationId</c>, or throws.</summary>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target element.</param>
    /// <param name="gesture">The gesture to perform.</param>
    /// <exception cref="NotSupportedException">
    /// This platform cannot perform the gesture on that element.
    /// </exception>
    void PerformGesture(string automationId, MauiGesture gesture)
        => PerformGesture(automationId, gesture, 0, 0);

    /// <summary>
    /// Performs a gesture that carries a magnitude, or throws.
    /// </summary>
    /// <param name="automationId">The MAUI <c>AutomationId</c> of the target element.</param>
    /// <param name="gesture">The gesture to perform.</param>
    /// <param name="arg1">First argument, meaning defined per gesture.</param>
    /// <param name="arg2">Second argument, meaning defined per gesture.</param>
    /// <exception cref="NotSupportedException">
    /// This platform cannot perform the gesture on that element.
    /// </exception>
    void PerformGesture(string automationId, MauiGesture gesture, int arg1, int arg2)
        => throw new RouteUnavailableException(
            $"Gestures are not implemented for {GetType().Name}. On Windows they are carried by "
            + "the Bruinet UI Automation bridge, which the app under test must opt into; on "
            + "Android and iOS they are synthetic touch input.");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the specified URL or destination.
    /// </summary>
    void NavigateTo(string destination);

    /// <summary>
    /// Goes back one page, or throws saying why it could not.
    /// </summary>
    /// <exception cref="Bruinet.Core.Exceptions.BruinetException">
    /// The app did not go back, with the specific reason.
    /// </exception>
    void NavigateBack();

    /// <summary>
    /// Whether the app is showing its first page, with nothing to go back to.
    /// </summary>
    /// <returns>Whether there is nothing to go back to.</returns>
    bool IsAtNavigationRoot() => NavigationDepth() <= 1;

    /// <summary>
    /// How many pages are on the app's navigation stack.
    /// </summary>
    /// <returns>The number of pages, where 1 means only the root.</returns>
    int NavigationDepth() => 1;

    /// <summary>
    /// Whether the app has finished the work it had queued.
    /// </summary>
    /// <param name="timeoutMs">How long to give the queue to drain.</param>
    /// <returns>Whether it drained within the budget.</returns>
    bool IsIdle(int timeoutMs = 2000) => true;

    /// <summary>
    /// Where the app currently is, in the terms its own navigation model uses.
    /// </summary>
    /// <returns>The route, or the top page's identity.</returns>
    string CurrentRoute() => string.Empty;

    /// <summary>
    /// Refreshes the current page/view.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Takes a screenshot of the current state.
    /// </summary>
    byte[] TakeScreenshot();

    /// <summary>
    /// Resets the application state (terminates and relaunches).
    /// </summary>
    void ResetAppState();

    #endregion

    #region Script Execution

    /// <summary>
    /// Executes a script command (e.g., mobile gestures, platform-specific actions).
    /// </summary>
    /// <param name="script">The script name (e.g., "mobile: longClickGesture", "windows: click").</param>
    /// <param name="args">Arguments to pass to the script.</param>
    /// <returns>The script result, or null.</returns>
    object? ExecuteScript(string script, params object[] args);

    #endregion

    #region Dialogs

    /// <summary>
    /// Raises a menu item by its <c>AutomationId</c>, without opening any menu.
    /// </summary>
    /// <param name="automationId">The menu item's <c>AutomationId</c>.</param>
    /// <exception cref="Bruinet.Core.Exceptions.BruinetException">
    /// No such item, or the app offers no menu verbs.
    /// </exception>
    void InvokeMenuItem(string automationId)
        => throw new RouteUnavailableException(
            $"Menu items are not implemented for {GetType().Name}.");

    #endregion
}
