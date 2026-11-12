using System.Globalization;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Core.Recording;
using Bruinet.Maui.Interfaces;
using Microsoft.Maui.DevFlow.Driver;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace Bruinet.Maui.DevFlow;

/// <summary>
/// One element of the app, by the id the agent gives it. Every read looks it up in the driver's
/// current tree, so it reads the app as it is now; one gone from the tree is stale, as on a device.
/// Actions go to the agent, which runs the control's own events, commands and bindings: a tap
/// invokes, a fill sets the text. Pixels, hover and pointer paths are not the agent's to give, and
/// throw <see cref="RouteUnavailableException"/>.
/// </summary>
/// <remarks>The app element (no id) stands for the whole app: lookups over every window, the flyout, dialogs.</remarks>
internal sealed class DevFlowMauiElement : IMauiElement
{
    private readonly DevFlowMauiDriver _driver;
    private readonly string? _id;
    private readonly StepperPart _part;
    private readonly string? _rowKey;

    internal DevFlowMauiElement(DevFlowMauiDriver driver, string? id)
        : this(driver, id, StepperPart.None, RowKeyOf(driver, id))
    {
    }

    private DevFlowMauiElement(DevFlowMauiDriver driver, string? id, StepperPart part, string? rowKey)
    {
        _driver = driver;
        _id = id;
        _part = part;
        _rowKey = rowKey;
    }

    /// <summary>
    /// What the list row around an element showed when the element was found: its labels' texts.
    /// The agent's ids belong to the platform's row containers, which a list recycles for other items
    /// as it scrolls; a UI Automation row belongs to its item. An element whose row now shows other
    /// texts is stale, as the item's element would be gone, and is found again.
    /// </summary>
    private static string? RowKeyOf(DevFlowMauiDriver driver, string? id)
        => id is null || driver.Snapshot().Find(id) is not { } node ? null : RowKey(node);

    private static string? RowKey(DevFlowNode node)
    {
        for (var walker = node; walker is not null; walker = walker.Parent)
        {
            if (walker.Parent is { } parent && DevFlowLocators.IsItemsView(parent))
                return string.Join("\u001f", walker.DescendantsAndSelf().Where(n => n.Type == "Label").Select(n => n.Text ?? string.Empty));
        }

        return null;
    }

    // In the tree, and still on the item it was found on.
    private DevFlowNode? Current()
    {
        var node = _driver.Snapshot(IsNative).Find(_id!);
        return node is not null && (_rowKey is null || RowKey(node) == _rowKey) ? node : null;
    }

    /// <summary>
    /// The two buttons a platform draws inside a Stepper, which Bruinet's Stepper presses. The agent's
    /// tree has no such children, so they stand in for them, as on the MauiHeadless head: each steps
    /// the value by Increment, and is disabled at its end of the range, as the platform's is.
    /// </summary>
    private enum StepperPart
    {
        None,
        Minus,
        Plus,
    }

    private bool IsApp => _id is null;

    /// <summary>This element in the current tree; stale when it has left it.</summary>
    private DevFlowNode Node
        => (_id is null ? throw new RouteUnavailableException("The app element has no state of its own.") : Current())
           ?? throw new StaleElementException(Locator.ById(_id), detail: "The element has left the app's tree, or its list row now shows another item.");

    private DevFlowNode? TryNode => _id is null ? null : Current();

    // A native element (a dialog's, on Windows) is only in a tree read with native nodes.
    private bool IsNative => _id?.StartsWith("native:", StringComparison.Ordinal) == true;

    private ElementInfo Info => Node.Info;

    #region State

    // Shown (in the tree, visible with its ancestors, not on a covered page) and inside every
    // scroller around it: a label scrolled out of view is off screen, as UI Automation reports it.
    public bool Visible => TryNode is { Shown: true } node && InView(node);

    private static bool InView(DevFlowNode node)
    {
        if (node.Info.WindowBounds is not { Width: > 0, Height: > 0 } own)
            return true;

        foreach (var ancestor in Ancestors(node))
        {
            if (IsScroller(ancestor)
                && ancestor.Info.WindowBounds is { Width: > 0, Height: > 0 } port
                && (own.X + own.Width <= port.X || own.X >= port.X + port.Width
                    || own.Y + own.Height <= port.Y || own.Y >= port.Y + port.Height))
                return false;
        }

        return true;
    }

    public bool Enabled => SelfEnabled(Node) && PartEnabled();

    private static bool SelfEnabled(DevFlowNode node) => node.Info.IsEnabled && Ancestors(node).All(n => n.Info.IsEnabled || n.Type == "Window");

    private bool PartEnabled() => _part switch
    {
        StepperPart.Minus => Number(Property("Value")) > Number(Property("Minimum")),
        StepperPart.Plus => Number(Property("Value")) < Number(Property("Maximum")),
        _ => true,
    };

    public bool Selected => Info.IsSelected || Info.State.Selected || Checked == true;

    public string? Text => _part switch
    {
        StepperPart.Minus => "-",
        StepperPart.Plus => "+",
        _ => Info.Text ?? ShownTime(),
    };

    // A TimePicker shows its time, which the agent does not give as text: read from its Time, as 24-hour hours and minutes.
    private string? ShownTime()
        => Info.Type == "TimePicker" && TimeSpan.TryParse(Property("Time"), CultureInfo.InvariantCulture, out var time)
            ? time.ToString(@"hh\:mm", CultureInfo.InvariantCulture)
            : null;

    public string? TagName => IsApp ? "App" : _part == StepperPart.None ? Info.Type : "RepeatButton";

    public Rectangle Rect
        => Info.WindowBounds is { } bounds
            ? new Rectangle((int)Math.Round(bounds.X), (int)Math.Round(bounds.Y), (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height))
            : Rectangle.Empty;

    public Point Location => Rect.Location;

    public Size Size => Rect.Size;

    /// <remarks>The names every MAUI head answers (IMauiElement.GetAttribute), then any bindable property by its name.</remarks>
    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.ToLowerInvariant() switch
        {
            "classname" or "class" or "controltype" => TagName,
            "name" => Name,
            "automationid" => AutomationId,
            "enabled" => Enabled.ToString(),
            "visible" => Visible.ToString(),
            "helptext" => Property("SemanticProperties.Hint"),
            "scroll.verticalscrollpercent" or "scroll.horizontalscrollpercent"
                or "scroll.verticallyscrollable" or "scroll.horizontallyscrollable" => null,
            _ => Property(name),
        };
    }

    /// <summary>
    /// The state names Bruinet's controls read (as the Windows bridge answers them), then any property
    /// of the element by its name, dotted paths included (<c>Items.Count</c>).
    /// </summary>
    /// <remarks>A Picker's <c>Items</c> are not readable through the agent (it reads no indexers): null, so the control falls back.</remarks>
    public string? ReadState(string property)
    {
        ArgumentNullException.ThrowIfNull(property);
        var type = Info.Type;
        switch (property)
        {
            case "Items" when type == "Picker":
                return null;
            case "ItemCount" when type == "Picker":
                return Property("Items.Count");
            case "ItemCount" when type is "CollectionView" or "ListView" or "CarouselView":
                return Property("ItemsSource.Count") ?? "0";
            case "SelectedItem" when type == "Picker":
                // MAUI's "nothing chosen" is index -1; the bridge's is an empty text.
                return Property("SelectedIndex") == "-1" ? string.Empty : Property("SelectedItem") ?? string.Empty;
            case "Date" when type == "DatePicker":
                return ParseMoment(Property("Date"))?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            case "Time" when type == "TimePicker":
                return TimeSpan.TryParse(Property("Time"), CultureInfo.InvariantCulture, out var time)
                    ? time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                    : string.Empty;
            case "LineCount" when type == "DrawingView":
                return Property("Lines.Count") ?? "0";
            case "AutomationId":
                return AutomationId ?? string.Empty;
            case "IsVisible":
                return Visible.ToString();
            case "IsEnabled":
                return Enabled.ToString();
            case "IsFocused":
                return Focused.ToString();
        }

        return Property(property)
               ?? throw new NotSupportedException($"{TagName} #{AutomationId} has no property '{property}' the DevFlow agent can read.");
    }

    public bool Focused => !IsApp && Info.IsFocused;

    public string? Hint => Info.Type switch
    {
        "Entry" or "Editor" or "SearchBar" => Property("Placeholder"),
        "Picker" or "DatePicker" or "TimePicker" => Property("Title"),
        _ => null,
    };

    public bool? Checked => Info.Type switch
    {
        "Switch" => Bool(Property("IsToggled")),
        "CheckBox" or "RadioButton" => Bool(Property("IsChecked")),
        _ => null,
    };

    public string? Value => Info.Value;

    public bool? IsReadOnly => Info.Type is "Entry" or "Editor" or "SearchBar" ? Bool(Property("IsReadOnly")) : null;

    public double? RangeValue => IsRange ? Number(Property("Value")) : null;

    public double? RangeMinimum => IsRange ? Number(Property("Minimum")) : null;

    public double? RangeMaximum => IsRange ? Number(Property("Maximum")) : null;

    public double? RangeSmallChange => Info.Type == "Stepper" ? Number(Property("Increment")) : null;

    private bool IsRange => Info.Type is "Slider" or "Stepper" or "ProgressBar";

    public string? SelectedItemText => Info.Type == "Picker" ? Property("SelectedItem") : null;

    #endregion

    #region Basic actions

    public void Click() => Invoke();

    /// <summary>
    /// Types into a field: appended to its text, as keys typed at the end would be; with
    /// <see cref="TextInputMethod.SetValue"/> the text replaces it. Either way the agent sets the text.
    /// </summary>
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
    {
        ArgumentNullException.ThrowIfNull(text);
        var info = Ready("type into");
        var current = method == TextInputMethod.SetValue ? string.Empty : info.Text ?? string.Empty;
        using var mark = Report(InteractionKind.Type);
        _driver.Act(_driver.Agent.Fill(info, current + text), $"type into {Describe()}");
    }

    public void AppendText(string text) => SendKeys(text);

    public void Clear()
    {
        using var mark = Report(InteractionKind.Clear);
        _driver.Act(_driver.Agent.Clear(Ready("clear")), $"clear {Describe()}");
    }

    /// <summary>A double tap where the element takes one; else two taps, as a double click on a button is.</summary>
    public void DoubleClick()
    {
        using var mark = Report(InteractionKind.DoubleTap);
        var info = Ready("double-tap");
        if (_driver.Agent.Gesture(info, "doubletap"))
        {
            _driver.Changed();
            return;
        }

        _driver.Act(_driver.Agent.Tap(info), $"tap {Describe()}");
        _driver.Act(_driver.Agent.Tap(Ready("tap")), $"tap {Describe()} again");
    }

    public void RightClick() => throw NoRoute(nameof(RightClick));

    public void Hover() => throw NoRoute(nameof(Hover));

    public void LongPress(int durationMs = 1000)
    {
        using var mark = Report(InteractionKind.LongPress);
        _driver.Act(_driver.Agent.Gesture(Ready("long-press"), "longpress", durationMs: durationMs), $"long-press {Describe()}");
    }

    /// <summary>
    /// Brings the element into view through every scroller around it, inside out, as UI Automation's
    /// scroll-into-view does. For each scroller that cuts it off, innermost first, the agent is asked
    /// to show what that scroller holds on the way to the element (the element, or the inner scroller
    /// around it): the agent scrolls the nearest ScrollView to it. Where that moves nothing (the agent
    /// finds no ScrollView through some containers) the scroller is scrolled by the overlap; the agent
    /// applies an amount only to a ScrollView with none around it, which is why it is the fallback.
    /// </summary>
    public void ScrollIntoView(int timeoutMs)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var node = Node;
            if (Clipping(node) is not { } clip)
                return;

            var (scroller, dx, dy) = clip;
            var shown = Ancestors(node).TakeWhile(ancestor => !ReferenceEquals(ancestor, scroller)).LastOrDefault(IsScroller) ?? node;
            var before = node.Info.WindowBounds;
            if (_driver.Agent.Scroll(shown.Info, 0, 0))
            {
                _driver.Changed();
                var after = Node.Info.WindowBounds;
                if (after is not null && before is not null && (after.X != before.X || after.Y != before.Y))
                    continue;
            }

            _driver.Agent.Scroll(scroller.Info, dx, dy);
            _driver.Changed();
        }
    }

    // The innermost scroller that cuts the element off, and how far it must scroll to show it whole
    // (or its start, when it is larger than the scroller).
    private static (DevFlowNode Scroller, double Dx, double Dy)? Clipping(DevFlowNode node)
    {
        if (node.Info.WindowBounds is not { Width: > 0, Height: > 0 } own)
            return null;

        foreach (var scroller in Ancestors(node).Where(IsScroller))
        {
            if (scroller.Info.WindowBounds is not { Width: > 0, Height: > 0 } port)
                continue;

            var dy = Overlap(own.Y, own.Height, port.Y, port.Height);
            var dx = Overlap(own.X, own.Width, port.X, port.Width);
            if (dx != 0 || dy != 0)
                return (scroller, dx, dy);
        }

        return null;
    }

    // How far to scroll along one axis so [start, start + size) lies inside [portStart, portStart + portSize).
    private static double Overlap(double start, double size, double portStart, double portSize)
    {
        if (start < portStart)
            return start - portStart;
        var beyond = start + Math.Min(size, portSize) - (portStart + portSize);
        return beyond > 0 ? Math.Ceiling(beyond) : 0;
    }

    private static bool IsScroller(DevFlowNode node) => node.Type is "ScrollView" or "CollectionView" or "ListView" or "CarouselView";

    // The agent swipes by direction and distance, not along a path: the larger movement decides.
    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500)
    {
        var (dx, dy) = (endX - startX, endY - startY);
        var direction = Math.Abs(dx) >= Math.Abs(dy) ? (dx < 0 ? "left" : "right") : (dy < 0 ? "up" : "down");
        using var mark = Interactions.ReportSwipe(() => Rect, dx, dy, () => AutomationId);
        _driver.Act(
            _driver.Agent.Gesture(Ready("swipe"), "swipe", direction, Math.Max(Math.Abs(dx), Math.Abs(dy)), durationMs),
            $"swipe {direction} on {Describe()}");
    }

    public void Submit() => throw NoRoute(nameof(Submit));

    #endregion

    #region Child lookup

    public IMauiElement? TryFindElement(Locator locator) => FindElements(locator).FirstOrDefault();

    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (IsApp)
            return _driver.FindElements(locator);
        if (_part != StepperPart.None)
            return [];

        // A Stepper's buttons are drawn by the platform, not declared in XAML.
        if (TryNode is { Type: "Stepper" } && locator is { Strategy: LocatorStrategy.ClassName, Value: "RepeatButton" }
                or { Strategy: LocatorStrategy.ControlType, Value: "button" })
            return [new DevFlowMauiElement(_driver, _id, StepperPart.Minus, _rowKey), new DevFlowMauiElement(_driver, _id, StepperPart.Plus, _rowKey)];

        return TryNode is { } node ? _driver.Find(node, locator) : [];
    }

    #endregion

    #region Identity

    public string InstanceKey => (_id ?? "app") + (_part == StepperPart.None ? "" : "/" + _part);

    public string? AutomationId => IsApp ? null : TryNode?.AutomationId + (_part == StepperPart.None ? "" : _part.ToString());

    public string? Name => IsApp ? null : _part != StepperPart.None ? Text : TryNode is { } node ? DevFlowLocators.NameOf(node) : null;

    #endregion

    #region Activation

    public void Invoke()
    {
        if (_part != StepperPart.None)
        {
            StepStepper();
            return;
        }

        // The agent's tap finds no handler on an Expander; its header's tap turns it over.
        if (TryNode is { Type: "Expander" })
        {
            using var turn = Report(InteractionKind.Tap);
            var expanded = Bool(Property("IsExpanded")) ?? false;
            _driver.Act(_driver.Agent.SetProperty(Ready("expand"), "IsExpanded", expanded ? "false" : "true"), $"turn over {Describe()}");
            return;
        }

        using var mark = Report(InteractionKind.Tap);
        var info = Ready("invoke");
        if (!_driver.Agent.Tap(info))
        {
            _driver.Changed();

            // A list's row without a tap of its own is chosen by the list's selection, which the
            // agent cannot set: SelectedItem is an object, and it has no select action.
            if (Node.Parent is { } parent && DevFlowLocators.IsItemsView(parent))
                throw new RouteUnavailableException($"The DevFlow agent cannot select the row {Describe()}: it sets no list's SelectedItem and has no select action.");
            throw new BruinetException($"The DevFlow agent could not tap {Describe()}.");
        }

        _driver.Changed();
    }

    // As the platform's button steps it: by Increment, held within the range.
    private void StepStepper()
    {
        using var mark = Report(InteractionKind.Tap);
        var info = Ready($"press the {_part} button of");
        var value = Number(Property("Value")) ?? 0;
        var step = Number(Property("Increment")) ?? 1;
        var next = Math.Clamp(
            value + (_part == StepperPart.Plus ? step : -step),
            Number(Property("Minimum")) ?? double.MinValue,
            Number(Property("Maximum")) ?? double.MaxValue);
        _driver.Act(_driver.Agent.SetProperty(info, "Value", next.ToString("R", CultureInfo.InvariantCulture)), $"step {Describe()}");
    }

    public void Toggle()
    {
        if (Checked is null)
            throw new RouteUnavailableException($"{Describe()} cannot be toggled: it is no Switch or CheckBox.");
        Invoke();
    }

    public void Select() => Invoke();

    public void SetChecked(bool isChecked)
    {
        if (Checked is not { } current)
            throw new RouteUnavailableException($"{Describe()} has no checked state.");
        if (current != isChecked)
            Invoke();
    }

    public void InvokeToolbarItem(string automationId)
    {
        var item = _driver.FindElements(Locator.ByAutomationId(automationId))
                       .FirstOrDefault(element => element.TagName == "ToolbarItem")
                   ?? throw new BruinetException($"No toolbar item on the page carries that AutomationId: '{automationId}'.");
        item.Invoke();
    }

    public void Focus() => _driver.Act(_driver.Agent.Focus(Ready("focus")), $"focus {Describe()}");

    public void ClearFocus()
        => _driver.Act(_driver.Agent.SetProperty(Info, "IsFocused", "false"), $"take the focus from {Describe()}");

    #endregion

    #region Values

    public void SetRangeValue(double value) => Set("Value", value.ToString("R", CultureInfo.InvariantCulture));

    public void SelectIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        // A RatingView's stars: the star at an index is that rating, counted from one.
        if (Info.Type == "RatingView")
        {
            Set("Rating", (index + 1).ToString(CultureInfo.InvariantCulture));
            return;
        }

        Set("SelectedIndex", index.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Chooses an item by its text. The agent sets no object-typed property from a text and reads no
    /// item list, so only the item already chosen can be named; any other has no route here.
    /// </summary>
    public void SelectByText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Property("SelectedItem") == text)
            return;

        throw new RouteUnavailableException(
            $"The DevFlow agent cannot read {Describe()}'s item texts, so it cannot choose '{text}' by text. Select it by index.");
    }

    /// <summary>
    /// Sets the picker's date and reads back what it holds: a picker with a <c>MinimumDate</c> or
    /// <c>MaximumDate</c> clamps, and a clamped write is raised here, as the Windows head raises it.
    /// </summary>
    public void SetDate(DateTime date)
    {
        var wanted = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Set("Date", wanted);
        var landed = ParseMoment(Property("Date"))?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (landed != wanted)
        {
            throw new BruinetException(
                $"'{AutomationId}' was set to {wanted} and now holds '{landed}'. A DatePicker "
                + "clamps to its MinimumDate and MaximumDate, which is the usual cause.");
        }
    }

    /// <summary>Sets the picker's time and reads back what it holds. See <see cref="SetDate"/>.</summary>
    public void SetTime(TimeSpan time)
    {
        var wanted = time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        Set("Time", wanted);
        var read = Property("Time");
        var landed = TimeSpan.TryParse(read, CultureInfo.InvariantCulture, out var held)
            ? held.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : read;
        if (landed != wanted)
            throw new BruinetException($"'{AutomationId}' was set to {wanted} and now holds '{landed}'.");
    }

    // The agent formats a DateTime in the app's culture; the invariant and ISO forms are tried as well.
    private static DateTime? ParseMoment(string? text)
        => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant) ? invariant
            : DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var local) ? local
            : null;

    private void Set(string property, string value)
    {
        using var mark = Report(InteractionKind.Type);
        _driver.Act(_driver.Agent.SetProperty(Ready($"set {property} on"), property, value), $"set {property} of {Describe()} to '{value}'");
    }

    #endregion

    #region Gestures

    public void PerformGesture(MauiGesture gesture)
    {
        var (type, direction) = gesture switch
        {
            MauiGesture.Tap => ("tap", (string?)null),
            MauiGesture.DoubleTap => ("doubletap", null),
            MauiGesture.LongPress => ("longpress", null),
            MauiGesture.SwipeLeft => ("swipe", "left"),
            MauiGesture.SwipeRight => ("swipe", "right"),
            MauiGesture.SwipeUp => ("swipe", "up"),
            MauiGesture.SwipeDown => ("swipe", "down"),
            _ => throw NoRoute($"{nameof(PerformGesture)}({gesture})"),
        };
        using var mark = gesture switch
        {
            MauiGesture.SwipeLeft => Interactions.ReportSwipe(() => Rect, -1, 0, () => AutomationId),
            MauiGesture.SwipeRight => Interactions.ReportSwipe(() => Rect, 1, 0, () => AutomationId),
            MauiGesture.SwipeUp => Interactions.ReportSwipe(() => Rect, 0, -1, () => AutomationId),
            MauiGesture.SwipeDown => Interactions.ReportSwipe(() => Rect, 0, 1, () => AutomationId),
            MauiGesture.DoubleTap => Report(InteractionKind.DoubleTap),
            MauiGesture.LongPress => Report(InteractionKind.LongPress),
            _ => Report(InteractionKind.Tap),
        };
        var info = Ready(type);

        if (gesture == MauiGesture.Tap && info.Type == "Expander")
        {
            Invoke();
            return;
        }

        // A swipe across a CarouselView moves it one card, as the finger's does: the agent's swipe
        // looks for a SwipeGestureRecognizer only.
        if (info.Type == "CarouselView" && CarouselStep(gesture) is { } step)
        {
            var position = (int)(Number(Property("Position")) ?? 0);
            var count = (int)(Number(Property("ItemsSource.Count")) ?? int.MaxValue);
            var next = Math.Clamp(position + step, 0, Math.Max(0, count - 1));
            _driver.Act(_driver.Agent.SetProperty(info, "Position", next.ToString(CultureInfo.InvariantCulture)), $"swipe {Describe()}");
            return;
        }

        // A pull down over a RefreshView refreshes it, as the finger's pull does: the agent's swipe
        // looks for a SwipeGestureRecognizer only.
        if (gesture == MauiGesture.SwipeDown && RefreshViewAt(Node) is { } refresh)
        {
            if (!refresh.Info.IsEnabled)
                throw new ElementNotReadyException(Locator.ByAutomationId(refresh.AutomationId ?? refresh.Id), NotReadyReason.Disabled, "the RefreshView is disabled");
            _driver.Act(_driver.Agent.SetProperty(refresh.Info, "IsRefreshing", "true"), $"refresh {Describe()}");
            return;
        }

        _driver.Act(_driver.Agent.Gesture(info, type, direction), $"{type} {direction} on {Describe()}".Replace("  ", " ", StringComparison.Ordinal));
    }

    #endregion

    #region Scrolling

    public void ScrollTo(string automationId)
    {
        var target = TryNode is { } node
            ? DevFlowLocators.Match(node, Locator.ByAutomationId(automationId)).FirstOrDefault()
            : null;
        if (target is null)
            throw new BruinetException($"Nothing under {Describe()} carries the AutomationId '{automationId}'.");
        new DevFlowMauiElement(_driver, target.Id).ScrollIntoView(0);
    }

    public void ScrollToIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var list = ItemsViewNode() ?? throw new RouteUnavailableException($"{Describe()} holds no CollectionView or ListView to scroll to an item.");
        if (index >= ItemCountOf(list))
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Describe()} has {ItemCountOf(list)} items.");
        _driver.Act(_driver.Agent.Scroll(list.Info, 0, 0, index), $"scroll {Describe()} to item {index}");
    }

    /// <summary>
    /// On a list (or a wrapper around one), a step that puts the item at the top: the agent scrolls a
    /// CollectionView or ListView to an index, where an amount would go to a ScrollView around it.
    /// </summary>
    /// <remarks>
    /// Reported as a step, not a jump: the agent gives no row its index (no <c>PositionInSet</c>), so the
    /// collection's next index is the count of rows it has seen, and the item at the top brings the rows
    /// after it into view. A step that brings none is the end of the list, where a paging list loads more.
    /// </remarks>
    public ScrollStep ScrollTowards(int index)
    {
        if (ItemsViewNode() is not { } list)
            return ScrollContent(1);

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var count = ItemCountOf(list);
        if (index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{Describe()} has {count} items.");

        var rowsBefore = RowIds(list);
        _driver.Act(_driver.Agent.Scroll(list.Info, 0, 0, index, "Start"), $"scroll {Describe()} to item {index}");
        return _driver.Snapshot().Find(list.Id) is { } listAfter && !RowIds(listAfter).SequenceEqual(rowsBefore) ? ScrollStep.Moved : ScrollStep.NotMoved;
    }

    // The list this element is, or the first one it holds.
    private DevFlowNode? ItemsViewNode()
        => TryNode is { } node
            ? node.DescendantsAndSelf().FirstOrDefault(n => DevFlowLocators.IsItemsView(n) && n.Shown)
            : null;

    private int ItemCountOf(DevFlowNode list)
        => (int)(Number(_driver.Agent.GetProperty(list.Id, "ItemsSource.Count")) ?? int.MaxValue);

    /// <summary>A ScrollView's offset, viewport and content, from its own properties.</summary>
    public ScrollPosition ReadScrollPosition()
    {
        RequireScrollView();
        return new ScrollPosition(
            Number(Property("ScrollX")) ?? 0,
            Number(Property("ScrollY")) ?? 0,
            Number(Property("Width")) ?? 0,
            Number(Property("Height")) ?? 0,
            Number(Property("ContentSize.Width")) ?? 0,
            Number(Property("ContentSize.Height")) ?? 0);
    }

    public ScrollPercent? ReadScrollPercent()
    {
        if (Info.Type != "ScrollView")
            return null;
        var position = ReadScrollPosition();
        var range = position.ContentHeight - position.ViewportHeight;
        var vertical = range <= 0 ? 0 : Math.Clamp(position.Y / range * 100, 0, 100);
        var viewSize = position.ContentHeight <= 0 ? 100 : Math.Min(100, position.ViewportHeight / position.ContentHeight * 100);
        return new ScrollPercent(vertical, viewSize);
    }

    /// <summary>
    /// Scrolls a ScrollView to a vertical position in percent. A ScrollView with none around it is
    /// scrolled by the distance from where it is. One inside another cannot be: the agent spends an
    /// amount on the outermost ScrollView. It can show a child at the start, centre or end of its
    /// nearest ScrollView, so the inner one goes to the nearest of those positions to the target, on
    /// the side the target lies: the top and the end exactly, a position between them as near as the
    /// children allow.
    /// </summary>
    public void SetVerticalScrollPercent(double percent)
    {
        var position = ReadScrollPosition();
        var range = Math.Max(0, position.ContentHeight - position.ViewportHeight);
        var target = Math.Clamp(percent, 0, 100) / 100 * range;
        var delta = target - position.Y;
        if (Math.Abs(delta) < 0.5)
            return;

        var node = Node;
        if (!Ancestors(node).Any(ancestor => ancestor.Type == "ScrollView"))
        {
            _driver.Act(_driver.Agent.Scroll(Info, 0, delta), $"scroll {Describe()} to {percent:0.#} percent");
            return;
        }

        var reachable = ReachableOffsets(node, position.ViewportHeight, range);
        var onTheWay = reachable.Where(stop => Math.Abs(stop.Offset - position.Y) >= 0.5 && Math.Sign(stop.Offset - position.Y) == Math.Sign(delta)).ToList();
        if (onTheWay.Count == 0)
            return;

        var (child, placement, _) = onTheWay.MinBy(stop => Math.Abs(stop.Offset - target));
        _driver.Act(_driver.Agent.Scroll(child.Info, 0, 0, position: placement), $"scroll {Describe()} to {percent:0.#} percent");
    }

    // Where the ScrollView can be put by showing one of its children at its start, centre or end:
    // a child's offset in the content is where it is in the window, less where the content is.
    private static List<(DevFlowNode Child, string Placement, double Offset)> ReachableOffsets(
        DevFlowNode scroller, double viewport, double range)
    {
        var stops = new List<(DevFlowNode, string, double)>();
        if (scroller.Children.FirstOrDefault() is not { Info.WindowBounds: { } content })
            return stops;

        foreach (var child in scroller.DescendantsAndSelf().Skip(1))
        {
            if (child.Info.WindowBounds is not { Height: > 0 } bounds || IsScroller(child) && !ReferenceEquals(child.Parent, scroller))
                continue;

            var top = bounds.Y - content.Y;
            stops.Add((child, "Start", Math.Clamp(top, 0, range)));
            stops.Add((child, "Center", Math.Clamp(top + bounds.Height / 2 - viewport / 2, 0, range)));
            stops.Add((child, "End", Math.Clamp(top + bounds.Height - viewport, 0, range)));
        }

        return stops;
    }

    private void RequireScrollView()
    {
        if (Info.Type != "ScrollView")
            throw new RouteUnavailableException($"{Describe()} is no ScrollView: the DevFlow runner reads a scroll position from a ScrollView's own properties.");
    }

    public ScrollStep ScrollContent(int verticalSteps, int horizontalSteps = 0)
    {
        // A list goes back to its first item in one jump: an amount on a list inside a ScrollView
        // goes to the ScrollView. Moved when the realized rows changed.
        if ((verticalSteps < 0 || horizontalSteps < 0) && ItemsViewNode() is { } list)
        {
            var rowsBefore = RowIds(list);
            _driver.Agent.Scroll(list.Info, 0, 0, 0);
            _driver.Changed();
            return _driver.Snapshot().Find(list.Id) is { } listAfter && !RowIds(listAfter).SequenceEqual(rowsBefore) ? ScrollStep.Moved : ScrollStep.NotMoved;
        }

        var info = Info;
        var height = info.Bounds?.Height ?? 0;
        var width = info.Bounds?.Width ?? 0;
        var before = info.Children?.FirstOrDefault()?.Bounds;
        var done = _driver.Agent.Scroll(info, horizontalSteps * width * 0.8, verticalSteps * height * 0.8);
        _driver.Changed();
        if (!done)
            return ScrollStep.NotMoved;

        var after = TryNode?.Info.Children?.FirstOrDefault()?.Bounds;
        return before is null || after is null || before.X != after.X || before.Y != after.Y ? ScrollStep.Moved : ScrollStep.NotMoved;
    }

    #endregion

    #region The app

    public void OpenFlyout() => SetFlyout(true);

    public void CloseFlyout() => SetFlyout(false);

    public bool? IsFlyoutOpen => RequireApp() && ShellNode() is { } shell ? Bool(_driver.Agent.GetProperty(shell.Id, "FlyoutIsPresented")) : null;

    private void SetFlyout(bool open)
    {
        RequireApp();
        var shell = ShellNode() ?? throw new RouteUnavailableException("The app has no Shell, so no flyout.");
        _driver.Act(_driver.Agent.SetProperty(shell.Info, "FlyoutIsPresented", open ? "true" : "false"), (open ? "open" : "close") + " the flyout");
    }

    private DevFlowNode? ShellNode()
        => _driver.Snapshot().All.FirstOrDefault(node => node.Type.EndsWith("Shell", StringComparison.Ordinal)
                                                         || node.Info.FullType == "Microsoft.Maui.Controls.Shell");

    public IMauiElement? TryFindDeclared(string automationId)
    {
        RequireApp();
        return _driver.Snapshot().Windows
            .SelectMany(window => _driver.Find(window, Locator.ByAutomationId(automationId), includeHidden: true))
            .FirstOrDefault();
    }

    /// <summary>The dialog on screen (see <see cref="DevFlowTree.ActiveDialog"/>), or null when none is open.</summary>
    public IMauiElement? TryFindActiveDialog()
    {
        RequireApp();
        return _driver.Snapshot(dialogs: true).ActiveDialog() is { } dialog ? new DevFlowMauiElement(_driver, dialog.Id) : null;
    }

    /// <summary>
    /// What the dialog on screen asks, read from the dialog itself: its first text is the title, the
    /// next the message, and its buttons come in the order MAUI was given them. A one-button alert
    /// reports its button as <see cref="AlertContents.Cancel"/>, as MAUI passes it.
    /// </summary>
    public AlertContents? ReadAlert()
    {
        RequireApp();
        if (_driver.Snapshot(dialogs: true).ActiveDialog() is not { } dialog)
            return null;

        var texts = dialog.DescendantsAndSelf().Skip(1).Where(node => node.Type == "Text").Select(node => node.Text ?? string.Empty).ToList();
        var buttons = dialog.DescendantsAndSelf().Where(node => node.Type == "Button").Select(node => node.Text ?? string.Empty).ToList();
        var title = dialog.Text ?? texts.FirstOrDefault() ?? string.Empty;
        var message = texts.Skip(1).FirstOrDefault() ?? string.Empty;
        return buttons.Count switch
        {
            0 => new AlertContents(title, message, string.Empty, string.Empty),
            1 => new AlertContents(title, message, string.Empty, buttons[0]),
            _ => new AlertContents(title, message, buttons[0], buttons[^1]),
        };
    }

    private bool RequireApp()
        => IsApp ? true : throw new RouteUnavailableException($"{Describe()} is not the app element. Ask IMauiTestContext.AppElement.");

    #endregion

    #region DOM Access (Hybrid Apps)

    public string? GetDomAttribute(string attributeName) => null;

    public string? GetDomProperty(string propertyName) => null;

    public string? GetCssValue(string propertyName) => null;

    #endregion

    /// <summary>This element, checked as a device checks it before acting: on screen and enabled.</summary>
    private ElementInfo Ready(string action)
    {
        var node = Node;
        var locator = string.IsNullOrEmpty(node.AutomationId) ? Locator.ByClassName(node.Type) : Locator.ByAutomationId(node.AutomationId);
        if (!node.Shown)
            throw new ElementNotReadyException(locator, NotReadyReason.NotVisible, $"cannot {action} it");
        // A Stepper's end button is checked as the Stepper: pressed at the end it does nothing, as on the other heads.
        if (!(_part == StepperPart.None ? Enabled : SelfEnabled(node)))
            throw new ElementNotReadyException(locator, NotReadyReason.Disabled, $"cannot {action} it");
        return node.Info;
    }

    private static List<string> RowIds(DevFlowNode list) => list.Children.Select(row => row.Id).ToList();

    // Left (or up, on a vertical carousel) is the next card.
    private int? CarouselStep(MauiGesture gesture)
    {
        var vertical = Property("ItemsLayout.Orientation") == "Vertical";
        return (gesture, vertical) switch
        {
            (MauiGesture.SwipeLeft, false) or (MauiGesture.SwipeUp, true) => 1,
            (MauiGesture.SwipeRight, false) or (MauiGesture.SwipeDown, true) => -1,
            _ => null,
        };
    }

    // The RefreshView the element is, holds, or sits in.
    private static DevFlowNode? RefreshViewAt(DevFlowNode node)
        => node.DescendantsAndSelf().FirstOrDefault(n => n.Type == "RefreshView")
           ?? Ancestors(node).FirstOrDefault(n => n.Type == "RefreshView");

    private Interactions.Scope Report(InteractionKind kind) => Interactions.Report(kind, () => Rect, () => AutomationId);

    private string? Property(string name) => IsApp ? null : _driver.Agent.GetProperty(Node.Id, name);

    private static IEnumerable<DevFlowNode> Ancestors(DevFlowNode node)
    {
        for (var walker = node.Parent; walker is not null; walker = walker.Parent)
            yield return walker;
    }

    private static bool? Bool(string? value) => bool.TryParse(value, out var flag) ? flag : null;

    private static double? Number(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    private string Describe() => IsApp ? "the app" : TryNode is { } node ? $"{node.Type} #{node.AutomationId ?? node.Id}" : $"element {_id}";

    private RouteUnavailableException NoRoute(string member)
        => new($"'{member}' has no route on the DevFlow runner: its input is semantic, with no pointer or pixels. Run this test on FlaUI or Appium.");
}
