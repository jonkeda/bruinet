namespace Bruinet.NativeIos.Interfaces;

// R9: NativeIos owns this contract and does not derive from Core's IElement<T>; lookups make
// one attempt and never wait. See AD-010.

/// <summary>
/// One element of the app under test, as XCUITest reports it. Every member reads or acts once; an
/// element that was removed throws <see cref="Bruinet.Core.Exceptions.StaleElementException"/>,
/// a session that ended throws <see cref="Bruinet.Core.Exceptions.AppUnavailableException"/>.
/// </summary>
public interface INativeIosElement
{
    #region Identity

    /// <summary>Which element this is: changes when the element is recreated or a cell is reused.</summary>
    string InstanceKey { get; }

    /// <summary>The element's accessibility identifier; null when it has none.</summary>
    string? Identifier { get; }

    /// <summary>The element's accessibility label: what VoiceOver reads. Null when it has none.</summary>
    string? Label { get; }

    /// <summary>The element's type as XCUITest reports it (<c>XCUIElementTypeButton</c>).</summary>
    string? ElementType { get; }

    #endregion

    #region State

    /// <summary>Whether the element is shown on the screen.</summary>
    bool Visible { get; }

    /// <summary>Whether the element is enabled.</summary>
    bool Enabled { get; }

    /// <summary>Whether the element is selected.</summary>
    bool Selected { get; }

    /// <summary>Whether the element has keyboard focus.</summary>
    bool Focused { get; }

    /// <summary>Whether the switch or toggle is on; null when the element cannot be switched.</summary>
    bool? Checked { get; }

    /// <summary>The element's text: its value, else its label. An empty text field answers empty, not its placeholder.</summary>
    string? Text { get; }

    /// <summary>The element's accessibility value (a switch's <c>1</c>, a slider's <c>50%</c>); null when it has none.</summary>
    string? Value { get; }

    /// <summary>The text field's placeholder; null when it has none.</summary>
    string? Placeholder { get; }

    /// <summary>The element's frame on the screen, in points.</summary>
    Rectangle Rect { get; }

    /// <summary>An XCUITest attribute (<c>name</c>, <c>label</c>, <c>value</c>, <c>type</c>, <c>hittable</c>, ...).</summary>
    string? GetAttribute(string name);

    #endregion

    #region Actions

    /// <summary>Taps the element.</summary>
    void Click();

    /// <summary>
    /// Taps a point of the element, as fractions of its width and height (0.5, 0.5 is its centre):
    /// for a control whose tap means where it lands, such as a slider's track.
    /// </summary>
    void TapAt(double x, double y);

    /// <summary>Taps the element twice.</summary>
    void DoubleTap();

    /// <summary>Presses the element and holds it.</summary>
    void LongPress(int durationMs = 1000);

    /// <summary>Types or sets text into the element.</summary>
    void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys);

    /// <summary>Empties the text field.</summary>
    void Clear();

    /// <summary>Swipes across the element.</summary>
    /// <param name="direction">The way the finger moves.</param>
    /// <param name="percent">How much of the element the swipe covers, 0 to 1.</param>
    void Swipe(GestureDirection direction, double percent = 0.75);

    /// <summary>Scrolls the element's content once.</summary>
    /// <param name="direction">The way the finger moves.</param>
    /// <param name="percent">How much of the element one scroll covers, 0 to 1.</param>
    /// <returns>Whether the content moved.</returns>
    bool Scroll(GestureDirection direction, double percent = 0.75);

    /// <summary>Scrolls the screen until the element is shown, within <paramref name="timeoutMs"/>.</summary>
    void ScrollIntoView(int timeoutMs);

    /// <summary>Sets a picker wheel or a slider to <paramref name="value"/> (XCUITest's adjust).</summary>
    void Adjust(string value);

    /// <summary>Turns a picker wheel one row: to the next row when <paramref name="forward"/>, else the previous one.</summary>
    void StepWheel(bool forward);

    #endregion

    #region Child lookup

    /// <summary>The first matching descendant now, or null. One attempt.</summary>
    INativeIosElement? TryFindElement(Locator locator);

    /// <summary>The matching descendants now. One attempt.</summary>
    IReadOnlyList<INativeIosElement> FindElements(Locator locator);

    #endregion
}
