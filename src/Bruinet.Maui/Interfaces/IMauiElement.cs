using System.Drawing;
using Bruinet.Core;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;

namespace Bruinet.Maui.Interfaces;

/// <summary>
/// A UI element of the MAUI app under test: its state, location, gestures and child lookup.
/// </summary>
public interface IMauiElement
{
    #region State

    /// <summary>Whether the element is currently visible on screen.</summary>
    bool Visible { get; }

    /// <summary>Whether the element is enabled for interaction.</summary>
    bool Enabled { get; }

    /// <summary>Whether the element is selected (toggles, checkboxes, list items).</summary>
    bool Selected { get; }

    /// <summary>The visible text content of the element, or null if not available.</summary>
    string? Text { get; }

    /// <summary>The control type or tag name, or null if not available.</summary>
    string? TagName { get; }

    /// <summary>The top-left location of the element on screen.</summary>
    Point Location { get; }

    /// <summary>The size of the element.</summary>
    Size Size { get; }

    /// <summary>The bounding rectangle of the element.</summary>
    Rectangle Rect { get; }

    /// <summary>
    /// An attribute of the element, or null when it has none. Every head answers the same names, spelled as here
    /// (Windows and MauiHeadless also in any case; on Appium a lowercase name is the driver's own) (review 04,
    /// finding 10): <c>Name</c>, <c>AutomationId</c>, <c>ClassName</c> (the platform's
    /// class: <c>Button</c> headless, the WinUI class on Windows), <c>ControlType</c>, <c>Enabled</c> and
    /// <c>Visible</c> (<c>True</c> or <c>False</c>), <c>HelpText</c>, and the scroll values
    /// <c>scroll.verticalscrollpercent</c>, <c>scroll.horizontalscrollpercent</c>, <c>scroll.verticallyscrollable</c>
    /// and <c>scroll.horizontallyscrollable</c> where the head has a layout (not MauiHeadless). Another name is the
    /// head's own: MauiHeadless reads a bindable property by its name, Appium asks the driver, Windows answers null.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    string? GetAttribute(string name);

    #endregion

    #region Basic actions and gestures

    /// <summary>Performs a click/tap on the element.</summary>
    void Click();

    /// <summary>Sends text to the element using the specified input method.</summary>
    /// <param name="text">The text to enter.</param>
    /// <param name="method">How to enter the text (Keys, Paste, or SetValue).</param>
    void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys);

    /// <summary>Clears the element's value (for input fields).</summary>
    void Clear();

    /// <summary>Performs a double-click on the element.</summary>
    void DoubleClick();

    /// <summary>Performs a right-click (context click) on the element.</summary>
    void RightClick();

    /// <summary>Hovers the pointer over the element.</summary>
    void Hover();

    /// <summary>Performs a long-press/hold on the element.</summary>
    /// <param name="durationMs">Duration in milliseconds.</param>
    void LongPress(int durationMs = 1000);

    /// <summary>Scrolls the element into the visible viewport.</summary>
    /// <param name="timeoutMs">
    /// The most time the scroll may take. No default: a caller passes what is left of its call's
    /// budget, so a scroll never outlasts the call it is part of (see
    /// <c>.docs/contracts/call-model.md</c>, R2 and R3). A driver that scrolls in steps makes at
    /// least one step, even on zero.
    /// </param>
    void ScrollIntoView(int timeoutMs);

    /// <summary>Performs a swipe gesture from one point to another.</summary>
    void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500);

    #endregion

    #region Child lookup

    /// <summary>
    /// Finds the first descendant matching <paramref name="locator"/>, in one attempt.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <returns>The element, or null when none matches now.</returns>
    IMauiElement? TryFindElement(Locator locator);

    /// <summary>
    /// Finds every descendant matching <paramref name="locator"/>, in one attempt.
    /// </summary>
    /// <param name="locator">The locator strategy and value.</param>
    /// <returns>The matches; empty when none match now.</returns>
    IReadOnlyList<IMauiElement> FindElements(Locator locator);

    #endregion

    #region Identity

    /// <summary>
    /// Identifies this element instance: the same for the same UI element, new when the platform
    /// replaces it.
    /// </summary>
    string InstanceKey { get; }

    /// <summary>
    /// The element's automation id - the identifier the app author set.
    /// </summary>
    string? AutomationId { get; }

    /// <summary>
    /// The element's accessible name - what a screen reader would announce.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// Whether the element currently has keyboard focus.
    /// </summary>
    bool Focused { get; }

    /// <summary>
    /// One-based logical position in a set, inherited from the nearest ancestor that publishes it.
    /// </summary>
    int? PositionInSet => null;

    /// <summary>
    /// Logical size of the set, inherited from the nearest ancestor that publishes it.
    /// </summary>
    int? SizeOfSet => null;

    /// <summary>
    /// The element's hint text - what a field shows before it is filled in.
    /// </summary>
    string? Hint { get; }

    #endregion

    #region Activation

    /// <summary>
    /// Performs the control's primary action.
    /// </summary>
    /// <exception cref="NotSupportedException">This platform cannot invoke this element.</exception>
    void Invoke()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement Invoke. A control asked to perform its primary "
            + "action and this platform offers no route to it.");

    /// <summary>
    /// Flips a two- or three-state control.
    /// </summary>
    /// <exception cref="NotSupportedException">This platform cannot toggle this element.</exception>
    void Toggle()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement Toggle. A control asked to change its checked "
            + "state and this platform offers no route to it.");

    /// <summary>
    /// Chooses this element, as one of a group or as an item in a list.
    /// </summary>
    /// <exception cref="NotSupportedException">This platform cannot select this element.</exception>
    void Select()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement Select. A control asked to be chosen and this "
            + "platform offers no route to it.");

    /// <summary>
    /// Raises this element as a MAUI <c>ToolbarItem</c>. Performs or throws.
    /// </summary>
    /// <param name="automationId">The item's <c>AutomationId</c>, as the app's markup gives it.</param>
    /// <exception cref="NotSupportedException">This platform cannot raise a toolbar item.</exception>
    void InvokeToolbarItem(string automationId)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement InvokeToolbarItem.");

    #endregion

    #region Focus

    /// <summary>Gives this element focus. Performs or throws.</summary>
    /// <exception cref="NotSupportedException">This platform cannot focus this element.</exception>
    void Focus()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement Focus.");

    #endregion

    #region Checked state

    /// <summary>
    /// Whether a two-state control is on: a <c>Switch</c> or a <c>CheckBox</c>.
    /// </summary>
    bool? Checked => null;

    /// <summary>Sets the checked state. Performs or throws; a no-op when already there.</summary>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void SetChecked(bool isChecked)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SetChecked.");

    #endregion

    #region Text field state

    /// <summary>
    /// The value a field publishes, where the platform keeps it apart from what the field shows.
    /// Null when it does not.
    /// </summary>
    string? Value => null;

    /// <summary>Whether a text field refuses edits. Null when the platform does not say.</summary>
    bool? IsReadOnly => null;

    #endregion

    #region Range

    /// <summary>A range control's current value, in the platform's own units. Null when not published.</summary>
    double? RangeValue => null;

    /// <summary>The smallest value the range allows. Null when not published.</summary>
    double? RangeMinimum => null;

    /// <summary>The largest value the range allows. Null when not published.</summary>
    double? RangeMaximum => null;

    /// <summary>The range's small step. Null when not published.</summary>
    double? RangeSmallChange => null;

    /// <summary>Sets a range control's value. Performs or throws.</summary>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void SetRangeValue(double value)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SetRangeValue.");

    #endregion

    #region Dropdown

    /// <summary>Whether the dropdown is open. Null where this element has no dropdown.</summary>
    bool? IsDropdownOpen => null;

    /// <summary>Opens the dropdown and waits for it to report open. Performs or throws.</summary>
    /// <exception cref="NotSupportedException">This element has no dropdown.</exception>
    void OpenDropdown()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement OpenDropdown.");

    /// <summary>Closes the dropdown. A no-op when it is already closed, or when there is none.</summary>
    void CloseDropdown() { }

    /// <summary>
    /// The texts of the items the dropdown shows, opening it for the read and restoring it.
    /// Null where this element has no dropdown.
    /// </summary>
    IReadOnlyList<string>? ReadDropdownItemTexts() => null;

    /// <summary>The text of the selector's selected item, read without opening anything. Null when none.</summary>
    string? SelectedItemText => null;

    /// <summary>
    /// The texts of a selector's items, in order. Null where this platform cannot read them.
    /// </summary>
    IReadOnlyList<string>? ReadItemTexts() => null;

    #endregion

    #region Gestures

    /// <summary>
    /// Performs the gesture, or throws saying why it could not.
    /// </summary>
    /// <param name="gesture">The gesture to perform.</param>
    /// <exception cref="NotSupportedException">
    /// This platform cannot perform the gesture on this element.
    /// </exception>
    void PerformGesture(MauiGesture gesture)
        => throw new RouteUnavailableException(
            $"Gestures are not implemented for {GetType().Name}. On Windows they are carried by "
            + "the Bruinet UI Automation bridge, which the app under test must opt into; on "
            + "Android and iOS they are synthetic touch input.");

    #endregion

    #region Semantic text

    /// <summary>Appends text to a field. Performs or throws.</summary>
    /// <param name="text">The text to append.</param>
    void AppendText(string text)
        => throw new RouteUnavailableException("This platform has no semantic route for appending text.");

    /// <summary>Removes focus from the element. Performs or throws.</summary>
    void ClearFocus()
        => throw new RouteUnavailableException("This platform has no semantic route for clearing focus.");

    #endregion

    #region State the platform cannot be asked for

    /// <summary>
    /// Reads a named piece of app state. Null where the app does not answer state reads here.
    /// </summary>
    /// <param name="property">The property name, as the provider spells it.</param>
    /// <returns>The value as the app formatted it, or null where the app does not answer.</returns>
    /// <exception cref="NotSupportedException">
    /// The app answers state reads here but has no case for that name.
    /// </exception>
    string? ReadState(string property) => null;

    #endregion

    #region Scrolling

    /// <summary>
    /// Scrolls the content one viewport step and reports what is known about whether it moved.
    /// Performs or throws.
    /// </summary>
    /// <param name="verticalSteps">Positive towards the end, negative towards the start.</param>
    /// <param name="horizontalSteps">Positive towards the end, negative towards the start.</param>
    /// <returns>What is known about the movement.</returns>
    /// <exception cref="NotSupportedException">This element has no scroll route.</exception>
    ScrollStep ScrollContent(int verticalSteps, int horizontalSteps = 0)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement ScrollContent.");

    /// <summary>
    /// Finds a descendant by scrolling this element until the descendant enters the tree.
    /// </summary>
    /// <param name="locator">The locator for the descendant.</param>
    /// <returns>The element once it is on screen and still, or null when scrolling does not reach it.</returns>
    IMauiElement? TryFindByScrolling(Locator locator) => null;

    /// <summary>
    /// Moves this element clear of the screen edges when it rests there, for a caller that asked to
    /// see it. A row at the bottom edge can show a sliver whose children are not in the tree.
    /// </summary>
    void RevealClearOfEdges()
    {
    }

    /// <summary>
    /// Brings a named descendant into view, without animation.
    /// </summary>
    /// <param name="automationId">The descendant to reveal.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void ScrollTo(string automationId)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement ScrollTo.");

    /// <summary>
    /// Moves the content towards the item at an index, and reports how it moved.
    /// Performs or throws.
    /// </summary>
    /// <param name="index">The item index to move towards.</param>
    /// <returns>How the content moved.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The platform can jump and the index is past the end of the items. A platform that can only
    /// step cannot tell, and scrolls.
    /// </exception>
    ScrollStep ScrollTowards(int index) => ScrollContent(1);

    /// <summary>Brings the item at an index into view.</summary>
    /// <param name="index">The item index.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The index is past the end of the items.</exception>
    void ScrollToIndex(int index)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement ScrollToIndex.");

    /// <summary>
    /// Where this scroller is, and how much there is to scroll.
    /// </summary>
    /// <returns>The scroller's position.</returns>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    ScrollPosition ReadScrollPosition()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement ReadScrollPosition.");

    #endregion

    #region Selection

    /// <summary>
    /// Selects the item at a position, or throws saying why it could not.
    /// </summary>
    /// <param name="index">The zero-based item position.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No item at that position.</exception>
    void SelectIndex(int index)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SelectIndex.");

    /// <summary>Selects the item showing this text, or throws saying why it could not.</summary>
    /// <param name="text">The item text.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void SelectByText(string text)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SelectByText.");

    #endregion

    #region Dates and times

    /// <summary>
    /// Sets a date picker's date.
    /// </summary>
    /// <param name="date">The date to set.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void SetDate(DateTime date)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SetDate. A control asked for a date to be set "
            + "and this platform offers no route to it.");

    /// <summary>
    /// Sets a time picker's time.
    /// </summary>
    /// <param name="time">The time to set.</param>
    /// <exception cref="NotSupportedException">This platform offers no route.</exception>
    void SetTime(TimeSpan time)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement SetTime. A control asked for a time to be set "
            + "and this platform offers no route to it.");

    #endregion

    #region The app (IMauiTestContext.AppElement only)

    // App-level operations: the Shell's flyout, the alert on screen, the dialog, and targets the
    // platform's tree cannot show. Only IMauiTestContext.AppElement implements these; any other
    // element throws.

    /// <summary>Opens a Shell's flyout. Performs or throws; already open is success.</summary>
    /// <exception cref="NotSupportedException">Not the app element, or no route on this platform.</exception>
    void OpenFlyout()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement OpenFlyout. Ask IMauiTestContext.AppElement.");

    /// <summary>Closes a Shell's flyout without choosing anything. Performs or throws.</summary>
    /// <exception cref="NotSupportedException">Not the app element, or no route on this platform.</exception>
    void CloseFlyout()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement CloseFlyout. Ask IMauiTestContext.AppElement.");

    /// <summary>
    /// Whether a Shell's flyout is showing, as the app reports it. Null when the platform cannot
    /// say without counting the flyout's items.
    /// </summary>
    /// <exception cref="NotSupportedException">Not the app element.</exception>
    bool? IsFlyoutOpen
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement IsFlyoutOpen. Ask IMauiTestContext.AppElement.");

    /// <summary>
    /// What the alert on screen is asking, or null when none is open or the app does not report it.
    /// </summary>
    /// <exception cref="NotSupportedException">Not the app element.</exception>
    AlertContents? ReadAlert()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement ReadAlert. Ask IMauiTestContext.AppElement.");

    /// <summary>The root of the dialog on screen, or null when none is open.</summary>
    /// <exception cref="NotSupportedException">Not the app element.</exception>
    IMauiElement? TryFindActiveDialog()
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement TryFindActiveDialog. Ask IMauiTestContext.AppElement.");

    /// <summary>
    /// Answers the platform's open-file dialog, which the app showed through its file picker,
    /// with one file, as a user who typed the path and chose Open. Waits for the dialog to
    /// appear and to close.
    /// </summary>
    /// <param name="path">The full path of an existing file.</param>
    /// <param name="timeoutMs">How long to wait for the dialog to appear and to close.</param>
    /// <exception cref="RouteUnavailableException">
    /// Not the app element, or this platform has no route to answer the dialog.
    /// </exception>
    void AnswerFileDialog(string path, int timeoutMs)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement AnswerFileDialog. Ask IMauiTestContext.AppElement.");

    /// <summary>
    /// An element the app declared by <c>AutomationId</c>, whether or not the platform's tree
    /// shows it. Null when there is none.
    /// </summary>
    /// <param name="automationId">The <c>AutomationId</c> in the app's markup.</param>
    /// <exception cref="NotSupportedException">Not the app element.</exception>
    IMauiElement? TryFindDeclared(string automationId)
        => throw new RouteUnavailableException(
            $"{GetType().Name} does not implement TryFindDeclared. Ask IMauiTestContext.AppElement.");

    #endregion

    #region DOM Access (Hybrid Apps)

    /// <summary>
    /// Gets a DOM attribute value (for WebView content).
    /// </summary>
    /// <param name="attributeName">The name of the DOM attribute.</param>
    /// <returns>The attribute value, or null if not present or not applicable.</returns>
    string? GetDomAttribute(string attributeName);

    /// <summary>
    /// Gets a DOM property value (for WebView content).
    /// </summary>
    /// <param name="propertyName">The name of the DOM property.</param>
    /// <returns>The property value, or null if not present or not applicable.</returns>
    string? GetDomProperty(string propertyName);

    /// <summary>
    /// Gets a computed CSS value (for WebView content).
    /// </summary>
    /// <param name="propertyName">The name of the CSS property.</param>
    /// <returns>The CSS value, or null if not applicable.</returns>
    string? GetCssValue(string propertyName);

    #endregion

    #region Form Actions

    /// <summary>
    /// Submits a form (if the element is within a form).
    /// </summary>
    void Submit();

    #endregion
}
