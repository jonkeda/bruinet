namespace Bruinet.Stride.Interfaces;

// Stride owns its element contract (AD-010): an element is a handle on one UI element of the game,
// known by its identity (UIElement.Id). Every read asks the game once over the automation pipe;
// nothing waits (R2). An element the game no longer has is stale (R5).

/// <summary>
/// One UI element of the Stride game under test, read and driven through the in-game automation
/// server (<c>Bruinet.Stride.Automation</c>).
/// </summary>
public interface IStrideElement
{
    /// <summary>The element's identity: Stride's <c>UIElement.Id</c>, the same for the same element.</summary>
    string InstanceKey { get; }

    /// <summary>The element's <c>Name</c>, which a page object finds it by; null for an unnamed one.</summary>
    string? AutomationId { get; }

    /// <summary>The element's Stride type (<c>Button</c>, <c>EditText</c>, <c>Slider</c>).</summary>
    string? ControlType { get; }

    /// <summary>Whether the element and every parent of it is shown.</summary>
    bool Visible { get; }

    /// <summary>Whether the element and every parent of it is enabled.</summary>
    bool Enabled { get; }

    /// <summary>A toggle button's state: true checked, false unchecked; null for any other element.</summary>
    bool? Checked { get; }

    /// <summary>A text block's or edit text's text, or a button's caption; null for any other element.</summary>
    string? Text { get; }

    /// <summary>A slider's value; null for any other element.</summary>
    double? RangeValue { get; }

    /// <summary>A slider's minimum; null for any other element.</summary>
    double? RangeMinimum { get; }

    /// <summary>A slider's maximum; null for any other element.</summary>
    double? RangeMaximum { get; }

    /// <summary>The element's bounds in the game window's UI coordinates.</summary>
    Rectangle Rect { get; }

    /// <summary>A named value: <c>automationid</c>, <c>controltype</c>, <c>text</c>, <c>checked</c>, <c>value</c>; null for any other.</summary>
    string? GetAttribute(string name);

    /// <summary>Clicks a button (its Click event, raised in the game); one action.</summary>
    void Click();

    /// <summary>Flips a toggle button's state once.</summary>
    void Toggle();

    /// <summary>Writes an edit text's text.</summary>
    void SetText(string text);

    /// <summary>Sets a slider's value, clamped by the game to its range.</summary>
    void SetRangeValue(double value);

    /// <summary>Stride's UI keeps every element of a page in place: there is nothing to scroll. Does nothing.</summary>
    void ScrollIntoView(int timeoutMs);

    /// <summary>The first descendant now whose name the locator gives, or null. One attempt.</summary>
    IStrideElement? TryFindElement(Locator locator);

    /// <summary>Every descendant now whose name the locator gives. One attempt.</summary>
    IReadOnlyList<IStrideElement> FindElements(Locator locator);

    /// <summary>The element's children now (a panel's children, a control's content). One attempt.</summary>
    IReadOnlyList<IStrideElement> Children();
}
