using System.Drawing;

namespace Bruinet.WinForms.Interfaces;

// WinForms owns its element contract and does not derive from a Core element (AD-010): lookups make one
// attempt and never wait (R2); a removed element is stale (R5).

/// <summary>
/// One element of the WinForms app under test, as UI Automation shows it. Reads answer about now;
/// actions act once, through UI Automation's patterns.
/// </summary>
public interface IWinFormsElement
{
    #region Identity

    /// <summary>The runtime id: the same for the same element, new when WinForms replaces it. Read live.</summary>
    string InstanceKey { get; }

    /// <summary>The element's AutomationId: the control's <c>Name</c>.</summary>
    string? AutomationId { get; }

    /// <summary>The UI Automation name: a text block's text, a button's caption.</summary>
    string? Name { get; }

    /// <summary>WinForms's class name (<c>TextBox</c>, <c>ComboBox</c>).</summary>
    string? ClassName { get; }

    /// <summary>The control type (<c>Button</c>, <c>Edit</c>).</summary>
    string? TagName { get; }

    #endregion

    #region State

    /// <summary>Whether the element is on screen now.</summary>
    bool Visible { get; }

    /// <summary>Whether the element is enabled.</summary>
    bool Enabled { get; }

    /// <summary>Whether the element has the keyboard focus.</summary>
    bool Focused { get; }

    /// <summary>Whether the element is selected, or toggled on.</summary>
    bool Selected { get; }

    /// <summary>The Toggle state; null indeterminate or without the Toggle pattern.</summary>
    bool? Checked { get; }

    /// <summary>The Value when the element has one, else its name.</summary>
    string? Text { get; }

    /// <summary>The Value pattern's value; null without it.</summary>
    string? Value { get; }

    /// <summary>Whether the Value is read-only; null without the Value pattern.</summary>
    bool? IsReadOnly { get; }

    /// <summary>The help text: a placeholder or tooltip, where the app sets one.</summary>
    string? HelpText { get; }

    /// <summary>The bounds on screen.</summary>
    Rectangle Rect { get; }

    /// <summary>A named property (<c>name</c>, <c>automationid</c>, <c>value</c>, <c>togglestate</c>, <c>expanded</c>, the scroll percentages); null for any other.</summary>
    string? GetAttribute(string name);

    #endregion

    #region Actions

    /// <summary>Activates the element through its first pattern (Invoke, Toggle, SelectionItem, ExpandCollapse).</summary>
    void Click();

    /// <summary>Flips the Toggle state once.</summary>
    void Toggle();

    /// <summary>Selects the element (SelectionItem).</summary>
    void Select();

    /// <summary>Writes the Value pattern.</summary>
    void SetValue(string value);

    /// <summary>Enters text: the Value pattern by default, the keyboard for <see cref="TextInputMethod.Keys"/>.</summary>
    void SendKeys(string text, TextInputMethod method = TextInputMethod.SetValue);

    /// <summary>Empties the element.</summary>
    void Clear();

    /// <summary>Gives the element the keyboard focus.</summary>
    void Focus();

    /// <summary>Focuses the element and presses one key (a key name such as <c>HOME</c>, or one character), with the keyboard (physical input).</summary>
    void PressKey(string key);

    /// <summary>Double-clicks with the pointer (physical input).</summary>
    void DoubleClick();

    /// <summary>Right-clicks with the pointer (physical input).</summary>
    void RightClick();

    /// <summary>Moves the pointer over the element (physical input).</summary>
    void Hover();

    /// <summary>Presses and holds with the pointer (physical input).</summary>
    void LongPress(int durationMs = 1000);

    /// <summary>Scrolls the element into view (ScrollItem); does nothing without the pattern.</summary>
    void ScrollIntoView(int timeoutMs);

    #endregion

    #region Range, expand, scroll

    /// <summary>The RangeValue pattern's value; null without it.</summary>
    double? RangeValue { get; }

    /// <summary>The RangeValue pattern's minimum; null without it.</summary>
    double? RangeMinimum { get; }

    /// <summary>The RangeValue pattern's maximum; null without it.</summary>
    double? RangeMaximum { get; }

    /// <summary>The RangeValue pattern's small change; null without it.</summary>
    double? RangeSmallChange { get; }

    /// <summary>Writes the RangeValue pattern.</summary>
    void SetRangeValue(double value);

    /// <summary>Whether the element is expanded; null without ExpandCollapse or for a leaf.</summary>
    bool? IsExpanded { get; }

    /// <summary>Expands the element.</summary>
    void Expand();

    /// <summary>Collapses the element.</summary>
    void Collapse();

    /// <summary>The vertical scroll position in percent; null when it does not scroll vertically.</summary>
    double? VerticalScrollPercent { get; }

    /// <summary>Scrolls one page down (or up); whether the content moved.</summary>
    bool ScrollPage(bool down);

    /// <summary>Sets the vertical scroll position in percent.</summary>
    void SetVerticalScrollPercent(double percent);

    #endregion

    #region Lookup

    /// <summary>The first matching descendant now, or null. One attempt.</summary>
    IWinFormsElement? TryFindElement(Locator locator);

    /// <summary>Every matching descendant now. One attempt.</summary>
    IReadOnlyList<IWinFormsElement> FindElements(Locator locator);

    #endregion
}
