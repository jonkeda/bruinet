using System.Drawing;
using System.Globalization;
using Bruinet.Core.Diagnostics;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;

namespace Bruinet.FlaUI;

/// <summary>
/// An element of a desktop app read and driven through UI Automation's standard patterns:
/// what WPF's and WinForms' elements share. Every member is one attempt (R2): a read answers about
/// now, an action acts once. A removed element is reported stale and a closed app unavailable
/// (R5). Each stack's element derives from this and adds its own typed lookups, as its element
/// interface asks (AD-010: the element stays per stack).
/// </summary>
/// <remarks>
/// <para>
/// Actions use a pattern: Invoke, Toggle, SelectionItem, ExpandCollapse, Value, RangeValue,
/// ScrollItem, Scroll. None needs the app in front. Where a gesture has no pattern (a double
/// click, a right click, hover, a long press, typing into a field with no Value pattern), real
/// input goes through <see cref="PhysicalInput"/>, which <c>BRUINET_BACKGROUND_MODE</c> can refuse
/// (AD-005).
/// </para>
/// </remarks>
public abstract class UiaElementBase
{
    /// <summary>The element <paramref name="element"/> of <paramref name="app"/>.</summary>
    protected UiaElementBase(AutomationElement element, DesktopApp app)
    {
        Element = element ?? throw new ArgumentNullException(nameof(element));
        App = app ?? throw new ArgumentNullException(nameof(app));
    }

    /// <summary>The UI Automation element.</summary>
    public AutomationElement Element { get; }

    /// <summary>The app the element belongs to.</summary>
    protected DesktopApp App { get; }

    /// <summary>Runs a read or action, reporting a removed element as stale and a closed app as unavailable.</summary>
    protected T Live<T>(Func<T> touch) => UiaElements.Live(App, touch);

    /// <inheritdoc cref="Live{T}(Func{T})"/>
    protected void Live(Action touch) => UiaElements.Live(App, touch);

    #region Identity

    /// <summary>The runtime id: the same for the same element, new when the framework replaces it.</summary>
    public string InstanceKey => Live(() => UiaElements.InstanceKey(Element));

    /// <summary>Whether the element still hangs under the app's window.</summary>
    public bool IsAttached => Live(() => UiaElements.IsAttached(App, Element));

    /// <summary>The AutomationId (WPF's <c>AutomationProperties.AutomationId</c>, WinForms' control <c>Name</c>).</summary>
    public string? AutomationId => Live(() => Element.Properties.AutomationId.ValueOrDefault);

    /// <summary>The UI Automation name: a label's text, a button's caption.</summary>
    public string? Name => Live(() => Element.Properties.Name.ValueOrDefault);

    /// <summary>The framework's class name (<c>TextBox</c>, <c>WindowsForms10.EDIT...</c>).</summary>
    public string? ClassName => Live(() => Element.Properties.ClassName.ValueOrDefault);

    /// <summary>The control type (<c>Button</c>, <c>Edit</c>, <c>ComboBox</c>).</summary>
    public string? TagName => Live(() => Element.ControlType.ToString());

    #endregion

    #region State

    /// <summary>Whether the element is on screen now: UI Automation's <c>IsOffscreen</c> is false.</summary>
    public bool Visible => Live(() => !Element.Properties.IsOffscreen.ValueOrDefault);

    /// <summary>Whether the element is enabled.</summary>
    public bool Enabled => Live(() => Element.Properties.IsEnabled.ValueOrDefault);

    /// <summary>Whether the element has the keyboard focus.</summary>
    public bool Focused => Live(() => Element.Properties.HasKeyboardFocus.ValueOrDefault);

    /// <summary>Whether the element is selected (SelectionItem), or else toggled on (Toggle).</summary>
    public bool Selected => Live(() =>
    {
        if (Element.Patterns.SelectionItem.PatternOrDefault is { } selection)
            return selection.IsSelected.ValueOrDefault;
        return Element.Patterns.Toggle.PatternOrDefault is { } toggle && toggle.ToggleState.ValueOrDefault == ToggleState.On;
    });

    /// <summary>The Toggle state: true on, false off, null indeterminate or when the element has no Toggle pattern.</summary>
    public bool? Checked => Live<bool?>(() => Element.Patterns.Toggle.PatternOrDefault is { } toggle
        ? toggle.ToggleState.ValueOrDefault switch
        {
            ToggleState.On => true,
            ToggleState.Off => false,
            _ => null,
        }
        : null);

    /// <summary>
    /// The element's text: its Value when it has the Value pattern (a text box, an editable combo
    /// box), else its name (a label, a button's caption).
    /// </summary>
    public string? Text => Live(() => Element.Patterns.Value.PatternOrDefault is { } value
        ? value.Value.ValueOrDefault
        : Element.Properties.Name.ValueOrDefault);

    /// <summary>The Value pattern's value; null when the element has no Value pattern.</summary>
    public string? Value => Live(() => Element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault);

    /// <summary>Whether the Value pattern is read-only; null when the element has no Value pattern.</summary>
    public bool? IsReadOnly => Live(() => Element.Patterns.Value.PatternOrDefault?.IsReadOnly.ValueOrDefault);

    /// <summary>The help text: a field's placeholder or tooltip, where the app sets it.</summary>
    public string? HelpText => Live(() => Element.Properties.HelpText.ValueOrDefault);

    /// <summary>The bounds, in one read.</summary>
    public Rectangle Rect => Live(() =>
    {
        var bounds = Element.BoundingRectangle;
        return new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    });

    /// <summary>The top-left corner on screen.</summary>
    public Point Location => Rect.Location;

    /// <summary>The size on screen.</summary>
    public Size Size => Rect.Size;

    /// <summary>
    /// A named property: <c>name</c>, <c>automationid</c>, <c>classname</c>, <c>controltype</c>,
    /// <c>enabled</c>, <c>visible</c>, <c>helptext</c>, <c>value</c>, <c>togglestate</c>,
    /// <c>expanded</c>, <c>scroll.verticalscrollpercent</c>, <c>scroll.horizontalscrollpercent</c>,
    /// <c>scroll.verticallyscrollable</c>, <c>scroll.horizontallyscrollable</c>; null for any other.
    /// </summary>
    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Live(() => name.ToLowerInvariant() switch
        {
            "name" => Element.Properties.Name.ValueOrDefault,
            "automationid" => Element.Properties.AutomationId.ValueOrDefault,
            "classname" or "class" => Element.Properties.ClassName.ValueOrDefault,
            "controltype" => Element.ControlType.ToString(),
            "enabled" => Element.Properties.IsEnabled.ValueOrDefault.ToString(),
            "visible" => (!Element.Properties.IsOffscreen.ValueOrDefault).ToString(),
            "helptext" => Element.Properties.HelpText.ValueOrDefault,
            "value" => Element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault,
            "togglestate" => Element.Patterns.Toggle.PatternOrDefault?.ToggleState.ValueOrDefault.ToString(),
            "expanded" => Element.Patterns.ExpandCollapse.PatternOrDefault is { } expand
                ? (expand.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Expanded).ToString()
                : null,
            "scroll.verticalscrollpercent" => Element.Patterns.Scroll.PatternOrDefault?.VerticalScrollPercent.ValueOrDefault.ToString(CultureInfo.InvariantCulture),
            "scroll.horizontalscrollpercent" => Element.Patterns.Scroll.PatternOrDefault?.HorizontalScrollPercent.ValueOrDefault.ToString(CultureInfo.InvariantCulture),
            "scroll.verticallyscrollable" => Element.Patterns.Scroll.PatternOrDefault?.VerticallyScrollable.ValueOrDefault.ToString(),
            "scroll.horizontallyscrollable" => Element.Patterns.Scroll.PatternOrDefault?.HorizontallyScrollable.ValueOrDefault.ToString(),
            _ => null,
        });
    }

    #endregion

    #region Actions

    /// <summary>
    /// Activates the element through the first pattern it has: Invoke (a button), Toggle (a check
    /// box), SelectionItem (a radio button, a list item, a tab), ExpandCollapse (a combo box, a
    /// tree item). One with none of them is clicked with the pointer, through
    /// <see cref="PhysicalInput"/>.
    /// </summary>
    public void Click() => Live(() =>
    {
        if (Element.Patterns.Invoke.PatternOrDefault is { } invoke)
        {
            invoke.Invoke();
            return;
        }

        if (Element.Patterns.Toggle.PatternOrDefault is { } toggle)
        {
            toggle.Toggle();
            return;
        }

        if (Element.Patterns.SelectionItem.PatternOrDefault is { } selection)
        {
            selection.Select();
            return;
        }

        if (Element.Patterns.ExpandCollapse.PatternOrDefault is { } expand)
        {
            if (expand.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Expanded)
                expand.Collapse();
            else
                expand.Expand();
            return;
        }

        PhysicalInput.Used($"{GetType().Name}.Click(pointer)", "an Invoke, Toggle, SelectionItem or ExpandCollapse pattern on the element");
        Element.Click();
    });

    /// <summary>Invokes the element (Invoke pattern).</summary>
    /// <exception cref="NotSupportedException">The element has no Invoke pattern.</exception>
    public void Invoke() => Live(() => Pattern(Element.Patterns.Invoke.PatternOrDefault, "Invoke").Invoke());

    /// <summary>Flips the element's Toggle state once (Toggle pattern).</summary>
    /// <exception cref="NotSupportedException">The element has no Toggle pattern.</exception>
    public void Toggle() => Live(() => Pattern(Element.Patterns.Toggle.PatternOrDefault, "Toggle").Toggle());

    /// <summary>Selects the element (SelectionItem pattern).</summary>
    /// <exception cref="NotSupportedException">The element has no SelectionItem pattern.</exception>
    public void Select() => Live(() => Pattern(Element.Patterns.SelectionItem.PatternOrDefault, "SelectionItem").Select());

    /// <summary>Writes <paramref name="value"/> through the Value pattern.</summary>
    /// <exception cref="NotSupportedException">The element has no Value pattern.</exception>
    public void SetValue(string value) => Live(() => Pattern(Element.Patterns.Value.PatternOrDefault, "Value").SetValue(value));

    /// <summary>
    /// Enters <paramref name="text"/>: <see cref="TextInputMethod.SetValue"/> (the default here)
    /// writes the Value pattern; <see cref="TextInputMethod.Keys"/> and
    /// <see cref="TextInputMethod.Paste"/> type with the keyboard, through
    /// <see cref="PhysicalInput"/>. An element with no Value pattern is typed into.
    /// </summary>
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.SetValue) => Live(() =>
    {
        ArgumentNullException.ThrowIfNull(text);
        if (method == TextInputMethod.SetValue && Element.Patterns.Value.PatternOrDefault is { } value)
        {
            value.SetValue(text);
            return;
        }

        PhysicalInput.Used($"{GetType().Name}.SendKeys({method})", "a Value pattern on the element");
        Element.Focus();
        Keyboard.Type(text);
    });

    /// <summary>
    /// Gives the element the focus and presses one key (<c>Home</c>, <c>Right</c>, <c>Tab</c>, a
    /// digit), through <see cref="PhysicalInput"/>: for a control whose value has no pattern to
    /// write it (a WinForms date picker).
    /// </summary>
    /// <param name="key">A <c>VirtualKeyShort</c> name (<c>HOME</c>, <c>RIGHT</c>) or a single character to type.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> names no key.</exception>
    public void PressKey(string key) => Live(() =>
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        PhysicalInput.Used($"{GetType().Name}.PressKey", "a pattern that writes the control's value");
        if (!Element.Properties.HasKeyboardFocus.ValueOrDefault)
            Element.Focus();

        try
        {
            if (key.Length == 1)
                Keyboard.Type(key[0]);
            else if (Enum.TryParse<global::FlaUI.Core.WindowsAPI.VirtualKeyShort>(key, ignoreCase: true, out var virtualKey))
                Keyboard.Type(virtualKey);
            else
                throw new ArgumentException($"'{key}' names no key.", nameof(key));
        }
        catch (System.ComponentModel.Win32Exception refused)
        {
            throw new InvalidOperationException(
                $"Windows refused the key '{key}' for {this} ({refused.Message}): synthetic input is refused while the "
                + "desktop is locked, or when the app runs elevated and the test does not.", refused);
        }
    });

    /// <summary>Empties the element through the Value pattern, or else selects all and deletes with the keyboard.</summary>
    public void Clear() => Live(() =>
    {
        if (Element.Patterns.Value.PatternOrDefault is { } value)
        {
            value.SetValue(string.Empty);
            return;
        }

        PhysicalInput.Used($"{GetType().Name}.Clear(keys)", "a Value pattern on the element");
        Element.Focus();
        Keyboard.TypeSimultaneously(global::FlaUI.Core.WindowsAPI.VirtualKeyShort.CONTROL, global::FlaUI.Core.WindowsAPI.VirtualKeyShort.KEY_A);
        Keyboard.Type(global::FlaUI.Core.WindowsAPI.VirtualKeyShort.DELETE);
    });

    /// <summary>Gives the element the keyboard focus (UI Automation's SetFocus).</summary>
    public void Focus() => Live(() => Element.Focus());

    /// <summary>Double-clicks with the pointer, through <see cref="PhysicalInput"/>: UI Automation has no pattern for it.</summary>
    public void DoubleClick() => Live(() =>
    {
        PhysicalInput.Used($"{GetType().Name}.DoubleClick", "a pattern for the double click's command");
        Element.DoubleClick();
    });

    /// <summary>Right-clicks with the pointer, through <see cref="PhysicalInput"/>: UI Automation has no pattern for it.</summary>
    public void RightClick() => Live(() =>
    {
        PhysicalInput.Used($"{GetType().Name}.RightClick", "a context menu reachable by pattern");
        Element.RightClick();
    });

    /// <summary>Moves the pointer over the element, through <see cref="PhysicalInput"/>.</summary>
    public void Hover() => Live(() =>
    {
        PhysicalInput.Used($"{GetType().Name}.Hover", "the hover's content reachable by pattern");
        Mouse.MoveTo(Center());
    });

    /// <summary>Presses and holds with the pointer, through <see cref="PhysicalInput"/>.</summary>
    public void LongPress(int durationMs = 1000) => Live(() =>
    {
        PhysicalInput.Used($"{GetType().Name}.LongPress", "a pattern for the long press's command");
        Mouse.Position = Center();
        Mouse.Down(MouseButton.Left);
        try
        {
            Thread.Sleep(Math.Max(0, durationMs));
        }
        finally
        {
            Mouse.Up(MouseButton.Left);
        }
    });

    /// <summary>
    /// Scrolls the element into view through its ScrollItem pattern; one without it is left as it
    /// is. Never waits: <paramref name="timeoutMs"/> is the call's remaining budget, unused here.
    /// </summary>
    public void ScrollIntoView(int timeoutMs) => Live(() => Element.Patterns.ScrollItem.PatternOrDefault?.ScrollIntoView());

    #endregion

    #region Range

    /// <summary>The RangeValue pattern's value; null without it.</summary>
    public double? RangeValue => Live(() => Element.Patterns.RangeValue.PatternOrDefault?.Value.ValueOrDefault);

    /// <summary>The RangeValue pattern's minimum; null without it.</summary>
    public double? RangeMinimum => Live(() => Element.Patterns.RangeValue.PatternOrDefault?.Minimum.ValueOrDefault);

    /// <summary>The RangeValue pattern's maximum; null without it.</summary>
    public double? RangeMaximum => Live(() => Element.Patterns.RangeValue.PatternOrDefault?.Maximum.ValueOrDefault);

    /// <summary>The RangeValue pattern's small change (one step); null without it.</summary>
    public double? RangeSmallChange => Live(() => Element.Patterns.RangeValue.PatternOrDefault?.SmallChange.ValueOrDefault);

    /// <summary>Whether the RangeValue pattern is read-only; null without it.</summary>
    public bool? RangeIsReadOnly => Live(() => Element.Patterns.RangeValue.PatternOrDefault?.IsReadOnly.ValueOrDefault);

    /// <summary>Writes <paramref name="value"/> through the RangeValue pattern.</summary>
    /// <exception cref="NotSupportedException">The element has no RangeValue pattern.</exception>
    public void SetRangeValue(double value) => Live(() => Pattern(Element.Patterns.RangeValue.PatternOrDefault, "RangeValue").SetValue(value));

    #endregion

    #region Expand and collapse

    /// <summary>Whether the element is expanded; null without the ExpandCollapse pattern or for a leaf.</summary>
    public bool? IsExpanded => Live<bool?>(() => Element.Patterns.ExpandCollapse.PatternOrDefault is { } expand
        ? expand.ExpandCollapseState.ValueOrDefault switch
        {
            ExpandCollapseState.Expanded => true,
            ExpandCollapseState.Collapsed or ExpandCollapseState.PartiallyExpanded => false,
            _ => null,
        }
        : null);

    /// <summary>Expands the element (ExpandCollapse pattern).</summary>
    /// <exception cref="NotSupportedException">The element has no ExpandCollapse pattern.</exception>
    public void Expand() => Live(() => Pattern(Element.Patterns.ExpandCollapse.PatternOrDefault, "ExpandCollapse").Expand());

    /// <summary>Collapses the element (ExpandCollapse pattern).</summary>
    /// <exception cref="NotSupportedException">The element has no ExpandCollapse pattern.</exception>
    public void Collapse() => Live(() => Pattern(Element.Patterns.ExpandCollapse.PatternOrDefault, "ExpandCollapse").Collapse());

    #endregion

    #region Scroll

    /// <summary>The vertical scroll position in percent (0 to 100); null without the Scroll pattern or when it does not scroll vertically.</summary>
    public double? VerticalScrollPercent => Live<double?>(() => Element.Patterns.Scroll.PatternOrDefault is { } scroll && scroll.VerticallyScrollable.ValueOrDefault
        ? scroll.VerticalScrollPercent.ValueOrDefault
        : null);

    /// <summary>
    /// Scrolls the content one large step (a page) down, or up when <paramref name="down"/> is
    /// false. Answers whether the content moved; false at the edge or without the Scroll pattern.
    /// </summary>
    public bool ScrollPage(bool down) => Live(() =>
    {
        if (Element.Patterns.Scroll.PatternOrDefault is not { } scroll || !scroll.VerticallyScrollable.ValueOrDefault)
            return false;

        var before = scroll.VerticalScrollPercent.ValueOrDefault;
        if ((down && before >= 100) || (!down && before <= 0))
            return false;

        scroll.Scroll(ScrollAmount.NoAmount, down ? ScrollAmount.LargeIncrement : ScrollAmount.LargeDecrement);
        return Math.Abs(scroll.VerticalScrollPercent.ValueOrDefault - before) > 0.001;
    });

    /// <summary>Sets the vertical scroll position in percent (0 to 100), leaving the horizontal one.</summary>
    /// <exception cref="NotSupportedException">The element has no Scroll pattern.</exception>
    public void SetVerticalScrollPercent(double percent) => Live(() =>
        Pattern(Element.Patterns.Scroll.PatternOrDefault, "Scroll").SetScrollPercent(-1, Math.Clamp(percent, 0, 100)));

    #endregion

    /// <summary>"Button 'LoginButton'", for messages.</summary>
    public override string ToString()
    {
        try
        {
            return $"{Element.ControlType} '{Element.Properties.AutomationId.ValueOrDefault ?? Element.Properties.Name.ValueOrDefault}'";
        }
        catch (Exception error) when (FlaUIErrors.IsElementGone(error))
        {
            return "(an element no longer in the tree)";
        }
    }

    private Point Center()
    {
        var bounds = Element.BoundingRectangle;
        return new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
    }

    private T Pattern<T>(T? pattern, string name)
        where T : class
        => pattern ?? throw new NotSupportedException(
            $"{this} does not expose the UI Automation {name} pattern, so it cannot be asked for that. "
            + "Either the control object names the wrong operation for this element, or the app has to expose the pattern.");
}
