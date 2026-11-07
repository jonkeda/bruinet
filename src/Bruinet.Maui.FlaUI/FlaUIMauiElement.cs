using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Recording;
using Bruinet.Core.Utilities;
using FlaUI.Core.Definitions;
using System.Drawing;
using System.Globalization;
using FlaUI.Core.Patterns;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.FlaUI.Bridge;
using Bruinet.Maui.Interfaces;
using Bruinet.Uia;

namespace Bruinet.Maui.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IMauiElement"/> for Windows platform.
/// Provides native Windows UI Automation support for MAUI desktop apps.
/// No physical input and no fallback to it: an action with no UI Automation pattern and no
/// bridge verb throws, naming the route the app would have to offer (AD-005).
/// </summary>
public sealed class FlaUIMauiElement : IMauiElement, IDetachable
{
    private readonly AutomationElement? _wrapped;
    private readonly FlaUIMauiDriver _driver;

    /// <summary>
    /// Creates a new FlaUIMauiElement wrapper.
    /// </summary>
    /// <param name="element">The FlaUI AutomationElement to wrap.</param>
    /// <param name="driver">The driver that owns this element.</param>
    /// <exception cref="ArgumentNullException">Thrown when element or driver is null.</exception>
    public FlaUIMauiElement(AutomationElement element, FlaUIMauiDriver driver)
    {
        _wrapped = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    /// <summary>Creates the app element, which stands for the application window.</summary>
    private FlaUIMauiElement(FlaUIMauiDriver driver)
    {
        _driver = driver;
    }

    /// <summary>The app element: <see cref="IMauiDriver.AppElement"/>.</summary>
    internal static FlaUIMauiElement ForApp(FlaUIMauiDriver driver) => new(driver);

    /// <summary>Whether this element stands for the app rather than one of its elements.</summary>
    private bool IsApp => _wrapped is null;

    /// <summary>
    /// The automation element: the one wrapped, or for the app element the driver's window.
    /// </summary>
    private AutomationElement _element => _wrapped ?? _driver.RootElement;

    #region Live: how a removed element is reported

    /// <summary>
    /// Runs a UI Automation read or action, reporting a removed element as
    /// <see cref="StaleElementException"/> and a closed app as <see cref="AppUnavailableException"/>.
    /// </summary>
    private T Live<T>(Func<T> touch)
    {
        try
        {
            return touch();
        }
        catch (Exception error) when (FlaUIErrors.IsElementGone(error))
        {
            if (_driver.AppHasExited)
            {
                throw new AppUnavailableException("the application process has exited.", error);
            }

            throw new StaleElementException(platformError: error);
        }
    }

    /// <inheritdoc cref="Live{T}(Func{T})"/>
    private void Live(Action touch) => Live(() =>
    {
        touch();
        return true;
    });

    /// <inheritdoc />
    /// <remarks>
    /// The UI Automation runtime id: the same for the same element, new when WinUI replaces it (a
    /// toolbar item replaced when its command's CanExecute changes goes <c>…4.30</c> to
    /// <c>…4.65</c>). Read live, so it doubles as the "is this element still there" check.
    /// </remarks>
    public string InstanceKey => Live(() => string.Join('.', _element.Properties.RuntimeId.Value));

    /// <inheritdoc />
    /// <remarks>
    /// Walks the control view up to the app's window. A page Shell navigated away from leaves the
    /// tree before its element stops answering: for about a second it reads as present, on
    /// screen and full size, and only its missing way up to the window gives it away.
    /// </remarks>
    public bool IsAttached => Live(() =>
    {
        var walker = _driver.Automation.TreeWalkerFactory.GetControlViewWalker();
        var window = _driver.RootElement;
        var node = _element;

        // Deep MAUI trees are tens of levels, not hundreds; the bound only stops a cycle.
        for (var depth = 0; depth < MaxAttachedDepth; depth++)
        {
            if (node.Equals(window))
            {
                return true;
            }

            if (walker.GetParent(node) is not { } parent)
            {
                return false;
            }

            node = parent;
        }

        return true;
    });

    private const int MaxAttachedDepth = 256;

    #endregion

    #region State Properties

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Whether the element is on screen <em>right now</em>. UIA's <c>IsOffscreen</c> is false
    /// when the element is inside the viewport, and true when it is scrolled away, clipped, or
    /// not rendered.
    /// </para>
    /// <para>
    /// Deliberately nothing more. A control needing anything else — a Switch whose wrapper
    /// reports zero bounds, say — overrides <c>IsVisibleCore</c>, because that is control
    /// knowledge and an element must not know what a MAUI view means.
    /// </para>
    /// <para>
    /// "Visible once the user scrolls to it" is a different question, and the control object
    /// answers it separately through <c>IsVisibleAfterScroll</c>.
    /// </para>
    /// </remarks>
    public bool Visible => Live(() =>
    {
        try
        {
            return !_element.IsOffscreen;
        }
        catch (global::FlaUI.Core.Exceptions.PropertyNotSupportedException error)
        {
            // Every live element supports IsOffscreen; a disconnected WinUI peer reports it unsupported.
            throw new StaleElementException(platformError: error);
        }
    });

    /// <inheritdoc />
    public bool Enabled => Live(() => _element.IsEnabled);

    /// <inheritdoc />
    public int? PositionInSet => ReadNearestSetValue(
        static element => element.Properties.PositionInSet.ValueOrDefault);

    /// <inheritdoc />
    public int? SizeOfSet => ReadNearestSetValue(
        static element => element.Properties.SizeOfSet.ValueOrDefault);

    private int? ReadNearestSetValue(Func<AutomationElement, int> read) => Live<int?>(() =>
    {
        for (var candidate = _element; candidate is not null; candidate = candidate.Parent)
        {
            try
            {
                var value = read(candidate);
                if (value > 0)
                {
                    return value;
                }
            }
            catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
            {
                // This ancestor does not publish the optional set property.
            }
        }

        return null;
    });

    /// <inheritdoc />
    /// <remarks>
    /// The SelectionItem pattern and nothing else. It used to fall back to the Toggle pattern, so
    /// a checked CheckBox reported itself selected; checked state is <see cref="Checked"/> (step 105a).
    /// </remarks>
    public bool Selected => Live(() =>
    {
        try
        {
            return _element.Patterns.SelectionItem.IsSupported
                   && _element.Patterns.SelectionItem.Pattern.IsSelected.Value;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    });

    /// <inheritdoc />
    public string? AutomationId => Live<string?>(() =>
    {
        try
        {
            return _element.Properties.AutomationId.ValueOrDefault;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    #region Text field state

    /// <summary>
    /// The element carrying the Value pattern: this element, or the Edit that MAUI nests
    /// inside it. The nesting is the same one <see cref="Text"/> reads through.
    /// </summary>
    private AutomationElement? ValuePatternElement
    {
        get
        {
            try
            {
                if (_element.Patterns.Value.IsSupported)
                    return _element;

                var nested = FindNestedTextBoxElement();
                return nested?.Patterns.Value.IsSupported == true ? nested : null;
            }
            catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public string? Value => Live<string?>(() =>
    {
        try
        {
            return ValuePatternElement?.Patterns.Value.Pattern.Value.Value;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    public bool? IsReadOnly => Live<bool?>(() =>
    {
        try
        {
            return ValuePatternElement?.Patterns.Value.Pattern.IsReadOnly.Value;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    #endregion

    /// <inheritdoc />
    public string? Hint => Live<string?>(() =>
    {
        try
        {
            return _element.Properties.HelpText.ValueOrDefault;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>UI Automation first, and the app itself only if UI Automation says no.</b> Windows
    /// keyboard focus is a property of the foreground thread: a window that is not in front has
    /// no focused control as far as <c>HasKeyboardFocus</c> is concerned, however the app sees
    /// it. So on an occluded or off-screen app - the whole point of stage B - a field that MAUI
    /// considers focused reports false here, and every assertion about focus would be wrong in
    /// the one configuration this work exists to support.
    /// </para>
    /// <para>
    /// Asking the app costs a walk of the bridge, so it is asked only on the false path. A true
    /// from UI Automation is already the answer, and it is the answer on nearly every read.
    /// </para>
    /// </remarks>
    public bool Focused => Live(() =>
    {
        try
        {
            if (_element.Properties.HasKeyboardFocus.ValueOrDefault)
            {
                return true;
            }
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            // Fall through to the app's own answer, which is the better one anyway.
        }

        return TryBridge(BruinetVerb.IsFocused, string.Empty, out var reported)
               && bool.TryParse(reported, out var focused)
               && focused;
    });

    /// <inheritdoc />
    public string? Name => Live<string?>(() =>
    {
        try
        {
            return _element.Properties.Name.ValueOrDefault;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    public string? Text => Live<string?>(() =>
    {
        try
        {
            // Try Value pattern first (for text inputs)
            if (_element.Patterns.Value.IsSupported)
            {
                return _element.Patterns.Value.Pattern.Value.Value;
            }
            // Try RangeValue pattern for sliders and other range controls
            if (_element.Patterns.RangeValue.IsSupported)
            {
                return _element.Patterns.RangeValue.Pattern.Value.Value.ToString(CultureInfo.InvariantCulture);
            }

            // MAUI wraps text controls on WinUI: the AutomationId sits on a wrapper whose
            // own Value pattern is absent, and the text lives in a nested Edit. Reading
            // through to it here means a caller just asks for Text - it does not need to
            // know that this platform nests, which is why there is no longer an
            // INestedTextElement capability for controls to probe.
            var nested = FindNestedTextBoxElement();
            if (nested != null)
            {
                var nestedText = nested.Patterns.Value.IsSupported
                    ? nested.Patterns.Value.Pattern.Value.Value
                    : nested.Properties.Name.ValueOrDefault;
                if (!string.IsNullOrEmpty(nestedText))
                    return nestedText;
            }

            // Fallback to Name property with safe access
            return _element.Properties.Name.ValueOrDefault;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    public string? TagName => Live(() => _element.ControlType.ToString());

    /// <inheritdoc />
    public Point Location => Live(() => new Point(_element.BoundingRectangle.X, _element.BoundingRectangle.Y));

    /// <inheritdoc />
    public Size Size => Live(() => new Size(_element.BoundingRectangle.Width, _element.BoundingRectangle.Height));

    /// <inheritdoc />
    /// <remarks>One read of the bounds, so a single call sees one element, not two.</remarks>
    public Rectangle Rect => Live(() =>
    {
        var bounds = _element.BoundingRectangle;
        return new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    });

    #endregion

    #region Actions

    // A recorded video's touch mark (AD-024): the target as it is, before the action runs, in
    // physical pixels as the video's frames are.
    private Interactions.Scope Report(InteractionKind kind, string? key = null)
        => Interactions.Report(kind, PhysicalRect, () => AutomationId, key: key);

    private Rectangle PhysicalRect() => Windowing.PhysicalPixels.Read(() => Rect);

    private void Marked(InteractionKind kind, Action action)
    {
        using var mark = Report(kind);
        action();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>The app's <c>Tap</c> verb, or nothing.</b> A click is not a UI Automation operation: a
    /// control is invoked, toggled or selected, and control objects ask for those by name. What is
    /// left for <c>Click</c> is an element whose app declares a tap - a view with a
    /// <c>TapGestureRecognizer</c> - and on Android and iOS, where a tap is the ordinary path.
    /// </para>
    /// <para>
    /// It used to be a real mouse click, taking the foreground first.
    /// </para>
    /// </remarks>
    public void Click()
    {
        using var mark = Report(InteractionKind.Tap);

        if (DeclaresGesture(MauiGesture.Tap))
        {
            PerformGesture(MauiGesture.Tap);
            return;
        }

        throw NoQuietRoute(
            "be clicked",
            "Ask the control object for the operation it means - Invoke, Toggle or Select - or "
            + "declare the Tap verb on the element in the app under test.");
    }

    #region Activation

    /// <inheritdoc />
    /// <remarks>
    /// The Invoke pattern, and nothing else. Where a control genuinely cannot be invoked - a
    /// MAUI <c>ToolbarItem</c> is the known case - the control names its own operation,
    /// <see cref="InvokeToolbarItem"/>; it is not this method's job to guess a substitute.
    /// </remarks>
    public void Invoke() => Marked(InteractionKind.Tap, () => Perform(
        nameof(Invoke), HasInvokePattern, RunInvokePattern, "InvokePattern"));

    /// <inheritdoc />
    public void Toggle() => Marked(InteractionKind.Tap, () => Perform(
        nameof(Toggle), HasTogglePattern, RunTogglePattern, "TogglePattern"));

    /// <inheritdoc />
    public void Select() => Marked(InteractionKind.Tap, () => Perform(
        nameof(Select), HasSelectionItemPattern, RunSelectionItemPattern, "SelectionItemPattern"));

    // The three pattern checks Perform chooses on. All private: they were SupportsInvoke and
    // SupportsSelect on IMauiElement until no control asked them, and HasTogglePattern never
    // needed to be public because no control ever had to choose whether to toggle.
    private bool HasInvokePattern => HasPattern(() => _element.Patterns.Invoke.IsSupported);

    private bool HasTogglePattern => HasPattern(() => _element.Patterns.Toggle.IsSupported);

    private bool HasSelectionItemPattern => HasPattern(() => _element.Patterns.SelectionItem.IsSupported);

    /// <inheritdoc />
    /// <remarks>
    /// The app raises the item by id through the <c>InvokeToolbarItem</c> verb, which the page on
    /// screen must declare. Never the Invoke pattern: a toolbar item's peer accepts it and does
    /// nothing.
    /// </remarks>
    public void InvokeToolbarItem(string automationId)
    {
        // Marked on the item itself; asked of the app element, the item's place is unknown.
        using var mark = Interactions.Report(
            InteractionKind.Tap,
            () => AutomationId == automationId ? PhysicalRect() : throw new InvalidOperationException("Not the item itself."),
            () => automationId);
        _driver.InvokeToolbarItem(automationId);
    }

    /// <summary>
    /// Runs one automation pattern, or explains which half of it was missing.
    /// </summary>
    private void Perform(string operation, bool supported, Func<bool> run, string pattern) => Live(() =>
    {
        var name = AutomationId ?? Name ?? "(unnamed)";

        if (!supported)
        {
            throw new NotSupportedException(
                $"'{name}' does not expose the UI Automation {pattern}, so it cannot be asked to "
                + $"{operation}. Either the control object names the wrong operation for this "
                + "element, or this is not the element that was intended.");
        }

        if (!run())
        {
            throw new InvalidOperationException(
                $"The UI Automation {pattern} on '{name}' was available but refused to {operation}.");
        }
    });

    #endregion

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Writes the text; never types it.</b> <see cref="TextInputMethod.SetValue"/> tries the
    /// Value pattern and then the app's <c>SetText</c> verb. <see cref="TextInputMethod.Paste"/>
    /// is the <c>SetText</c> verb, because the thing a paste arranges is the field's contents, and
    /// the clipboard it used to overwrite belongs to the person at the machine.
    /// </para>
    /// <para>
    /// <b><see cref="TextInputMethod.Keys"/> throws.</b> Typing raises <c>TextChanged</c> per
    /// character, applies <c>MaxLength</c> as it goes and lets a numeric keyboard refuse a letter,
    /// and none of that can be done without the real keyboard. A test of that behaviour belongs on
    /// the Android head; a test that only wants the text there should say
    /// <see cref="TextInputMethod.SetValue"/>.
    /// </para>
    /// </remarks>
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys) => Marked(InteractionKind.Type, () => Live(() =>
    {
        switch (method)
        {
            case TextInputMethod.Keys:
                throw NoQuietRoute(
                    "be typed into key by key",
                    "Use TextInputMethod.SetValue to put the text in the field. A test of "
                    + "per-keystroke behaviour belongs on the Android head.");

            case TextInputMethod.Paste:
                if (TryBridge(BruinetVerb.SetText, text, out _))
                    return;

                throw NoQuietRoute(
                    "be pasted into",
                    "Declare the SetText verb on the element in the app under test.");

            case TextInputMethod.SetValue:
                // A password field takes a Value write and keeps it from MAUI: its Entry.Text stays
                // as it was, so the write would pass and the app would have nothing. Only the
                // app's SetText reaches the Entry.
                if (IsPasswordField)
                {
                    if (TryBridge(BruinetVerb.SetText, text, out _))
                        return;

                    throw NoQuietRoute(
                        "have its text set",
                        "It is a password field: MAUI on Windows keeps its text apart from what UI "
                        + "Automation writes, so the app under test needs to declare SetText on it.");
                }

                // The Value pattern first, and the bridge second. Both are semantic, so the
                // ordering is not about avoiding physical input at all - it is that the pattern
                // is two cross-process calls against an element already in hand, where the
                // bridge is a raw walk of the window's children before it can even start. The
                // bridge earns its place on the elements the pattern refuses: a read-only
                // wrapper, or a control that publishes no Value pattern at all.
                if (TrySetTextValue(text))
                {
                    _driver.SettleAfterWrite();
                    return;
                }

                if (TryBridge(BruinetVerb.SetText, text, out _))
                    return;

                throw NoQuietRoute(
                    "have its text set",
                    "It exposes no writable UI Automation Value pattern and does not answer the "
                    + "SetText verb - either it is read-only, or the app under test needs to "
                    + "declare SetText on it.");
        }
    }));

    /// <inheritdoc />
    public void Clear() => Marked(InteractionKind.Clear, () => Live(() =>
    {
        if (TrySetTextValue(string.Empty))
        {
            _driver.SettleAfterWrite();
            return;
        }

        if (TryBridge(BruinetVerb.ClearText))
            return;

        throw NoQuietRoute(
            "be cleared",
            "It exposes no writable UI Automation Value pattern and does not answer the ClearText "
            + "verb - either it is read-only, or the app under test needs to declare ClearText on it.");
    }));

    /// <inheritdoc />
    /// <remarks>The app's <c>DoubleTap</c> verb; throws where the app does not declare it.</remarks>
    /// <exception cref="GestureUnavailableException">The app does not offer a double tap here.</exception>
    public void DoubleClick() => PerformGesture(MauiGesture.DoubleTap);

    /// <inheritdoc />
    /// <remarks>
    /// Always throws. A right-click means "show me the context menu", and there is no way to make
    /// a menu appear without the pointer. Reaching one of its <i>items</i> is
    /// <c>IMauiDriver.InvokeMenuItem</c>, which is nearly always what a test wanted.
    /// </remarks>
    public void RightClick()
        => throw NoQuietRoute(
            "be right-clicked",
            "To run a context-menu item, use IMauiDriver.InvokeMenuItem with the item's "
            + "AutomationId. A test that the menu appears needs a real pointer, which this driver "
            + "does not use.");

    /// <inheritdoc />
    /// <remarks>
    /// Always throws. Hovering is pointer position, and no verb stands in for it yet; add one when
    /// an app under test needs pointer-over behaviour tested.
    /// </remarks>
    public void Hover()
        => throw NoQuietRoute(
            "be hovered",
            "Hover is pointer position, and no Bruinet verb stands in for it.");

    /// <inheritdoc />
    /// <remarks>
    /// The bridge's <c>LongPress</c> verb. It has no duration: the app raises its own long-press
    /// handling, which is the thing a test of it wants. It used to fall back to holding the real
    /// mouse button down where the app did not declare the verb.
    /// </remarks>
    /// <exception cref="GestureUnavailableException">The app does not offer a long press here.</exception>
    public void LongPress(int durationMs = 1000) => PerformGesture(MauiGesture.LongPress);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The UIA Scroll pattern on this element, or on the nearest scrollable ancestor, which reports
    /// whether the content moved. It is not pointer input.
    /// </para>
    /// <para>
    /// Without the pattern, the bridge's swipe verb for the direction - a vertical step towards the
    /// end is a swipe up - which the app must declare, and which cannot report movement. An element
    /// too short to swipe answers <see cref="ScrollStep.NotMoved"/>, as the shared swipe helper did
    /// when this route lived in the control layer.
    /// </para>
    /// </remarks>
    /// <exception cref="Bridge.GestureUnavailableException">
    /// No Scroll pattern, and the app does not declare the swipe.
    /// </exception>
    public ScrollStep ScrollContent(int verticalSteps, int horizontalSteps = 0) => Live<ScrollStep>(() =>
    {
        var scroll = FindScrollPattern();
        if (scroll == null)
        {
            return SwipeContent(verticalSteps, horizontalSteps);
        }

        if (verticalSteps == 0 && horizontalSteps == 0)
            return ScrollStep.NotMoved;

        var before = (scroll.VerticalScrollPercent.ValueOrDefault, scroll.HorizontalScrollPercent.ValueOrDefault);

        // A list already at its end cannot step on: it is only nudged, without waiting out a step's move.
        if (horizontalSteps == 0 && NudgeAtTheEnd(scroll, verticalSteps))
            return ScrollStep.NotMoved;

        try
        {
            scroll.Scroll(ToAmount(horizontalSteps), ToAmount(verticalSteps));
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            // UIA refuses a scroll past the end with an error rather than a no-op: "did not move".
            // A removed element is not that - it is let through, and reported as stale.
            return ScrollStep.NotMoved;
        }

        // The scroll percent does not update synchronously: read immediately it reports the
        // pre-scroll value, making a successful scroll look like no progress.
        return WaitForScrollChange(scroll, before) ? ScrollStep.Moved : ScrollStep.NotMoved;
    });

    /// <summary>
    /// At a list's end, a small step back and forward again: MAUI's CollectionView on Windows decides
    /// <c>RemainingItemsThresholdReached</c> on each scroll from the panel's last visible index, which a
    /// jump to the end leaves stale (measured: offset at the end, last visible 10 of 20), so a paging
    /// list is never asked for its next page. The second scroll reads it fresh.
    /// </summary>
    /// <returns>True when the list was at its end (and nudged), false when it was not, or no list.</returns>
    private bool NudgeAtTheEnd(IScrollPattern scroll, int verticalSteps)
    {
        if (verticalSteps <= 0
            || _element.ControlType != ControlType.List
            || scroll.VerticalScrollPercent.ValueOrDefault < 99.9)
        {
            return false;
        }

        try
        {
            var atEnd = (scroll.VerticalScrollPercent.ValueOrDefault, scroll.HorizontalScrollPercent.ValueOrDefault);
            scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallDecrement);
            if (WaitForScrollChange(scroll, atEnd))
            {
                var back = (scroll.VerticalScrollPercent.ValueOrDefault, scroll.HorizontalScrollPercent.ValueOrDefault);
                scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                WaitForScrollChange(scroll, back);
            }
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            // A list that refuses the nudge is left where it is: the end it reported stands.
        }

        return true;
    }

    /// <summary>The height below which a swipe has no room to travel.</summary>
    private const int MinimumSwipeHeight = 40;

    /// <summary>
    /// Steps the content with the bridge's swipe verbs, for an element with no Scroll pattern.
    /// </summary>
    private ScrollStep SwipeContent(int verticalSteps, int horizontalSteps)
    {
        if (verticalSteps != 0)
        {
            if (Rect.Height <= MinimumSwipeHeight)
                return ScrollStep.NotMoved;

            // Content moves opposite to the finger: towards the end is a swipe up.
            var gesture = verticalSteps > 0 ? MauiGesture.SwipeUp : MauiGesture.SwipeDown;
            for (var step = 0; step < Math.Abs(verticalSteps); step++)
                PerformGesture(gesture);
        }

        if (horizontalSteps != 0)
        {
            var gesture = horizontalSteps > 0 ? MauiGesture.SwipeLeft : MauiGesture.SwipeRight;
            for (var step = 0; step < Math.Abs(horizontalSteps); step++)
                PerformGesture(gesture);
        }

        return verticalSteps == 0 && horizontalSteps == 0
            ? ScrollStep.NotMoved
            : ScrollStep.Unconfirmed;
    }

    /// <summary>
    /// Polls until either scroll percent moves away from where it was.
    /// </summary>
    /// <returns>True if it moved; false if it stayed put for the whole window.</returns>
    private static bool WaitForScrollChange(IScrollPattern scroll, (double Vertical, double Horizontal) before)
    {
        var budgetMs = Bruinet.Core.Calls.CallBudget.Within(500);
        const int intervalMs = 25;

        for (var waited = 0; waited < budgetMs; waited += intervalMs)
        {
            if (Math.Abs(scroll.VerticalScrollPercent.ValueOrDefault - before.Vertical) > 0.01
                || Math.Abs(scroll.HorizontalScrollPercent.ValueOrDefault - before.Horizontal) > 0.01)
                return true;

            Thread.Sleep(intervalMs);
        }

        return false;
    }

    /// <summary>
    /// Maps a step count onto the UIA scroll increments, which are coarse by design.
    /// </summary>
    private static ScrollAmount ToAmount(int steps) => steps switch
    {
        0 => ScrollAmount.NoAmount,
        > 0 => ScrollAmount.LargeIncrement,
        _ => ScrollAmount.LargeDecrement,
    };

    /// <summary>
    /// Returns this element's scroll pattern, or the nearest ancestor's.
    /// </summary>
    private IScrollPattern? FindScrollPattern()
    {
        try
        {
            for (var candidate = _element; candidate != null; candidate = candidate.Parent)
            {
                if (candidate.Patterns.Scroll.IsSupported)
                    return candidate.Patterns.Scroll.Pattern;
            }
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            // Ancestors that cannot be walked: no route. A removed element is let through.
        }

        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Two routes, both semantic, and no guessing.</b> First the element's own ScrollItem
    /// pattern, which a list item has. If that leaves it off screen - or there is none - the
    /// nearest ancestor whose app-side element declares the bridge's <c>ScrollTo</c> verb is asked
    /// to reveal this element by id, which is exactly what a <c>ScrollView</c> knows how to do.
    /// </para>
    /// <para>
    /// <b>What went is a loop</b> that scrolled the nearest ancestor to the top, then down a large
    /// step at a time until this element stopped reporting itself off screen, then down a small
    /// step at a time - sleeping 50 ms between each, and giving up when the percentage stopped
    /// changing. It is the "scroll a bit, poll a percentage, guess" pattern the <c>ScrollTo</c>
    /// verb replaced (step 105c). An element neither route can reveal stays where it is, and the
    /// caller's visibility check reports it.
    /// </para>
    /// </remarks>
    public void ScrollIntoView(int timeoutMs) => Live(() =>
    {
        try
        {
            if (_element.Patterns.ScrollItem.IsSupported)
            {
                _element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }

            if (!_element.IsOffscreen)
                return;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            // A ScrollItem that refuses is the same as none: try the container.
        }

        var id = AutomationId;
        if (string.IsNullOrEmpty(id))
            return;

        for (var ancestor = _element.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            var container = new FlaUIMauiElement(ancestor, _driver);
            if (!string.IsNullOrEmpty(container.AutomationId) && container.DeclaresScrollVerbs)
            {
                container.ScrollTo(id);
                return;
            }
        }
    });

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>The bridge's swipe verb for the direction</b> (step 105b), which is the dominant axis of
    /// the two points. It used to fall back to a real pointer drag where the app did not declare
    /// the verb; now it throws <see cref="GestureUnavailableException"/>.
    /// </para>
    /// <para>
    /// <b>No mouse wheel.</b> A mostly-vertical swipe used to become five wheel clicks at the
    /// element's centre, because a drag does not scroll a WinUI <c>ScrollViewer</c>. Scrolling has
    /// its own routes now - <see cref="ScrollTo"/>, <see cref="ScrollToIndex"/> and
    /// <see cref="ScrollContent"/> - and a swipe that is secretly a scroll hid which one ran.
    /// </para>
    /// </remarks>
    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500)
    {
        var deltaX = endX - startX;
        var deltaY = endY - startY;

        var direction = Math.Abs(deltaX) >= Math.Abs(deltaY)
            ? (deltaX < 0 ? MauiGesture.SwipeLeft : MauiGesture.SwipeRight)
            : (deltaY < 0 ? MauiGesture.SwipeUp : MauiGesture.SwipeDown);

        PerformGesture(direction);
    }

    #endregion

    #region Element Finding

    /// <inheritdoc />
    public IMauiElement? TryFindElement(Locator locator) => Live<IMauiElement?>(() =>
    {
        var condition = locator.ToCondition(_driver.ConditionFactory);
        var found = _element.FindFirstDescendant(condition);

        return found == null ? null : new FlaUIMauiElement(found, _driver);
    });

    /// <inheritdoc />
    public IReadOnlyList<IMauiElement> FindElements(Locator locator) => Live<IReadOnlyList<IMauiElement>>(() =>
    {
        var condition = locator.ToCondition(_driver.ConditionFactory);

        return _element.FindAllDescendants(condition)
            .Select(e => new FlaUIMauiElement(e, _driver))
            .ToList();
    });

    #endregion

    #region Attribute Access (IMauiElement)

    /// <inheritdoc />
    public string? GetAttribute(string attributeName) => Live<string?>(() =>
    {
        try
        {
            // Map common attribute names to FlaUI properties
            // Use safe property access to avoid PropertyNotSupportedException
            return attributeName.ToLowerInvariant() switch
            {
                "name" => _element.Properties.Name.ValueOrDefault,
                "automationid" => _element.Properties.AutomationId.ValueOrDefault,
                "classname" or "class" => _element.Properties.ClassName.ValueOrDefault,
                "controltype" => _element.ControlType.ToString(),
                "enabled" => _element.IsEnabled.ToString(),
                "visible" => (!_element.IsOffscreen).ToString(),
                "helptext" => _element.Properties.HelpText.ValueOrDefault,
                "scroll.verticalscrollpercent" => GetScrollPatternValue(p => p.VerticalScrollPercent.Value),
                "scroll.horizontalscrollpercent" => GetScrollPatternValue(p => p.HorizontalScrollPercent.Value),
                "scroll.verticallyscrollable" => GetScrollPatternBool(p => p.VerticallyScrollable.Value),
                "scroll.horizontallyscrollable" => GetScrollPatternBool(p => p.HorizontallyScrollable.Value),
                _ => null
            };
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <summary>
    /// Gets a numeric value from the Scroll pattern, or null if not supported.
    /// </summary>
    private string? GetScrollPatternValue(Func<global::FlaUI.Core.Patterns.IScrollPattern, double> accessor)
    {
        if (!_element.Patterns.Scroll.IsSupported)
            return null;
        var value = accessor(_element.Patterns.Scroll.Pattern);
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets a boolean value from the Scroll pattern, or null if not supported.
    /// </summary>
    private string? GetScrollPatternBool(Func<global::FlaUI.Core.Patterns.IScrollPattern, bool> accessor)
    {
        if (!_element.Patterns.Scroll.IsSupported)
            return null;
        return accessor(_element.Patterns.Scroll.Pattern).ToString();
    }

    /// <inheritdoc />
    public string? GetDomAttribute(string attributeName)
    {
        // FlaUI doesn't have DOM attributes - return null
        return null;
    }

    /// <inheritdoc />
    public string? GetDomProperty(string propertyName)
    {
        // FlaUI doesn't have DOM properties - return null
        return null;
    }

    /// <inheritdoc />
    public string? GetCssValue(string propertyName)
    {
        // FlaUI doesn't have CSS values - return null
        return null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The bridge raises the control's own completion command, which is what Enter would have
    /// caused. It refuses when the app handles completion with an event handler rather than a
    /// bound command - <c>Entry.SendCompleted</c> is internal in MAUI, so there is genuinely no
    /// public route - and then this throws. It used to press the real Enter key instead.
    /// </remarks>
    public void Submit()
    {
        using var mark = Report(InteractionKind.Key, "Enter");

        // Exchange, not Invoke, even though it carries no strings. Submit lives in the text
        // range and the provider answers the whole range on one path; splitting a range across
        // both methods is how a verb ends up reaching the app down a route that has never heard
        // of it, and being refused for a reason that has nothing to do with the element.
        if (TryBridge(BruinetVerb.Submit, string.Empty, out _, onlyRoute: true))
            return;

        throw NoQuietRoute(
            "be submitted",
            "It does not answer the Submit verb. The verb raises a bound ReturnCommand; an app that "
            + "handles Completed with an event handler has no public route MAUI lets the bridge "
            + "call, so bind a command instead.");
    }

    #endregion

    #region Internal

    /// <summary>
    /// Gets the underlying FlaUI AutomationElement for internal use.
    /// </summary>
    internal AutomationElement Element => _element;

    #region Focus

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>The bridge, and this is the difference the whole of stage B turns on.</b> Focus and the
    /// desktop foreground window are separate things that the physical path had to conflate: it
    /// called <c>SetForeground</c> because the global keystrokes that usually follow go wherever
    /// the foreground is. Asking for focus on its own needs none of that, so the bridge route
    /// leaves the machine with whoever is sitting at it. UI Automation's own <c>SetFocus</c> is not
    /// offered: WinUI keyboard focus belongs to the foreground window, so on an app kept behind
    /// the user's work it lands nowhere a test can rely on.
    /// </para>
    /// <para>
    /// Without the <c>Focus</c> verb, an element that declares <c>Tap</c> is tapped, which is what
    /// the control object used to do when it was told focus had no route of its own. For a date
    /// picker that opens its calendar.
    /// </para>
    /// </remarks>
    public void Focus()
    {
        var declaresFocus = BridgeDeclares(BruinetVerb.Focus);
        if (declaresFocus && TryBridge(BruinetVerb.Focus))
            return;

        if (!declaresFocus && DeclaresGesture(MauiGesture.Tap))
        {
            PerformGesture(MauiGesture.Tap);
            return;
        }

        throw NoQuietRoute(
            "be focused",
            "It does not answer the Focus verb. Declare Focus on the element in the app under test.");
    }

    #endregion

    /// <summary>
    /// The exception for an action this driver has no route for without physical input.
    /// </summary>
    /// <param name="operation">What was asked, phrased to follow "cannot", e.g. "be clicked".</param>
    /// <param name="instead">What to do instead.</param>
    /// <returns>The exception to throw.</returns>
    private NotSupportedException NoQuietRoute(string operation, string instead)
        => new(
            $"'{AutomationId ?? Name ?? "(unnamed)"}' cannot {operation} without real mouse or "
            + $"keyboard input, which Bruinet.Maui does not use on Windows. {instead}");

    #endregion

    #region Checked state

    /// <inheritdoc />
    /// <remarks>The Toggle pattern: <c>Switch</c> maps to ToggleSwitch, <c>CheckBox</c> to CheckBox.</remarks>
    public bool? Checked => Live<bool?>(() =>
    {
        try
        {
            return _element.Patterns.Toggle.IsSupported
                ? _element.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On
                : null;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    /// <remarks>
    /// UI Automation has no set-state call, but a state read and a toggle against the element in
    /// hand is as close as the platform comes, and the read makes it idempotent. The control
    /// verifies the outcome. Without the Toggle pattern, <see cref="Toggle"/> throws naming it.
    /// </remarks>
    public void SetChecked(bool isChecked) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (Checked == isChecked)
            return;

        Toggle();
    }));

    private bool RunTogglePattern()
    {
        try
        {
            if (!_element.Patterns.Toggle.IsSupported)
                return false;

            _element.Patterns.Toggle.Pattern.Toggle();
            return true;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    }

    #endregion

    #region Activation patterns

    private bool HasPattern(Func<bool> probe)
    {
        try
        {
            return probe();
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    }

    private bool RunInvokePattern()
    {
        try
        {
            if (!_element.Patterns.Invoke.IsSupported)
                return false;

            _element.Patterns.Invoke.Pattern.Invoke();
            return true;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    }

    private bool RunSelectionItemPattern()
    {
        try
        {
            if (!_element.Patterns.SelectionItem.IsSupported)
                return false;

            _element.Patterns.SelectionItem.Pattern.Select();
            return true;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    }

    #endregion

    #region Range

    /// <inheritdoc />
    public double? RangeValue => ReadRange(p => p.Value.Value);

    /// <inheritdoc />
    public double? RangeMinimum => ReadRange(p => p.Minimum.Value);

    /// <inheritdoc />
    public double? RangeMaximum => ReadRange(p => p.Maximum.Value);

    /// <inheritdoc />
    public double? RangeSmallChange => ReadRange(p => p.SmallChange.Value);

    /// <inheritdoc />
    /// <remarks>Clamped to the published bounds, as the platform would clamp a drag.</remarks>
    public void SetRangeValue(double value) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (!HasPattern(() => _element.Patterns.RangeValue.IsSupported))
        {
            throw new NotSupportedException(
                $"'{AutomationId ?? Name ?? "(unnamed)"}' does not expose the UI Automation "
                + "RangeValue pattern, so its value cannot be set.");
        }

        var pattern = _element.Patterns.RangeValue.Pattern;
        var clamped = Math.Clamp(value, pattern.Minimum.Value, pattern.Maximum.Value);
        pattern.SetValue(clamped);
    }));

    private double? ReadRange(Func<IRangeValuePattern, double> read) => Live<double?>(() =>
    {
        try
        {
            return _element.Patterns.RangeValue.IsSupported
                ? read(_element.Patterns.RangeValue.Pattern)
                : null;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    #endregion

    #region Dropdown

    /// <summary>
    /// Whether this element exposes the ExpandCollapse pattern. A WinUI <c>ComboBox</c> is the
    /// case this exists for.
    /// </summary>
    private bool HasDropdown => HasPattern(() => _element.Patterns.ExpandCollapse.IsSupported);

    /// <inheritdoc />
    public bool? IsDropdownOpen => Live<bool?>(() =>
        HasDropdown
            ? _element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value
                == ExpandCollapseState.Expanded
            : null);

    /// <inheritdoc />
    public void OpenDropdown() => Marked(InteractionKind.Tap, () => Live(() =>
    {
        RequireDropdown(nameof(OpenDropdown));

        _element.Patterns.ExpandCollapse.Pattern.Expand();

        if (!Bruinet.Core.Calls.Settle.Until(() => IsDropdownOpen == true, windowMs: 2000, pollingIntervalMs: 50))
        {
            throw new InvalidOperationException(
                $"The dropdown on '{AutomationId ?? Name ?? "(unnamed)"}' accepted Expand and did "
                + "not report itself open.");
        }
    }));

    /// <inheritdoc />
    /// <remarks>Lenient: an element with no dropdown is already in the asked-for state.</remarks>
    public void CloseDropdown() => Live(() =>
    {
        if (IsDropdownOpen == true)
        {
            _element.Patterns.ExpandCollapse.Pattern.Collapse();
        }
    });

    /// <summary>
    /// The live popup item elements, for a caller that is holding the dropdown open.
    /// </summary>
    private IReadOnlyList<IMauiElement> ReadDropdownItems()
    {
        RequireDropdown(nameof(ReadDropdownItems));

        AutomationElement[] items = [];

        Bruinet.Core.Calls.Settle.Until(() =>
        {
            items = _element.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            if (items.Length > 0)
                return true;

            items = _element.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            return items.Length > 0;
        }, windowMs: 2000, pollingIntervalMs: 50);

        return items.Select(item => (IMauiElement)new FlaUIMauiElement(item, _driver)).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// For a dropdown, the Selection pattern, which names the chosen item rather than the combo
    /// box's header. For anything else, <see cref="Text"/>: what the control shows as its choice.
    /// </remarks>
    public string? SelectedItemText => Live<string?>(() =>
    {
        if (!HasDropdown)
            return Text;

        try
        {
            if (!_element.Patterns.Selection.IsSupported)
                return null;

            var selection = _element.Patterns.Selection.Pattern.Selection.Value;
            return selection is { Length: > 0 } ? selection[0].Name : null;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    });

    /// <inheritdoc />
    /// <remarks>
    /// The same read as <see cref="ReadDropdownItemTexts"/> on Windows. They stay two members
    /// because they mean different things - "every item the selector holds" versus "what the
    /// popup is showing" - as <c>Picker</c> documents.
    /// </remarks>
    public IReadOnlyList<string>? ReadItemTexts() => ReadDropdownItemTexts();

    /// <inheritdoc />
    /// <remarks>
    /// Opened for the read and restored afterwards, so the items are live while their texts are
    /// read. Null for an element with no dropdown.
    /// </remarks>
    public IReadOnlyList<string>? ReadDropdownItemTexts() => Live<IReadOnlyList<string>?>(() =>
    {
        if (!HasDropdown)
            return null;

        return WithDropdownOpen(
            () => ReadDropdownItems().Select(item => item.Text ?? string.Empty).ToList());
    });

    /// <summary>Runs a read with the dropdown open, restoring the state it was found in.</summary>
    private T WithDropdownOpen<T>(Func<T> read)
    {
        var wasOpen = IsDropdownOpen == true;
        if (!wasOpen)
        {
            OpenDropdown();
        }

        try
        {
            return read();
        }
        finally
        {
            if (!wasOpen)
            {
                CloseDropdown();
            }
        }
    }

    /// <summary>
    /// Opens the dropdown, selects the item <paramref name="choose"/> picks, and leaves it closed.
    /// </summary>
    /// <returns>False when <paramref name="choose"/> found nothing; the dropdown is closed again.</returns>
    private bool SelectFromDropdown(Func<IReadOnlyList<IMauiElement>, IMauiElement?> choose)
    {
        OpenDropdown();

        var item = choose(ReadDropdownItems());
        if (item == null)
        {
            CloseDropdown();
            return false;
        }

        item.Select();

        // Choosing an item closes a combo box by itself; close it if this one did not.
        Bruinet.Core.Calls.Settle.Until(() => IsDropdownOpen != true, windowMs: 2000, pollingIntervalMs: 50);
        CloseDropdown();
        return true;
    }

    private void RequireDropdown(string operation)
    {
        if (!HasDropdown)
        {
            throw new NotSupportedException(
                $"'{AutomationId ?? Name ?? "(unnamed)"}' does not expose the UI Automation "
                + $"ExpandCollapse pattern, so it has no dropdown to {operation}.");
        }
    }

    #endregion

    #region Nested text resolution

    // MAUI wraps text controls on WinUI: the AutomationId sits on a wrapper whose own Value
    // pattern is absent, and the real text lives in a nested Edit. Resolving that is this
    // element's job - the Text property and TrySetTextValue below both go through it - so no
    // control needs to know this platform nests, and there is no INestedTextElement capability
    // for one to probe.

    private AutomationElement? FindNestedTextBoxElement()
    {
        try
        {
            return _element.FindFirstDescendant(cf =>
                cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.Edit));
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a value through the UIA Value pattern, reaching into a wrapper if it has to.
    /// </summary>
    // The field itself, or the TextBox a MAUI Entry renders inside it.
    private bool IsPasswordField => Live(() =>
        _element.Properties.IsPassword.ValueOrDefault
        || FindNestedTextBoxElement()?.Properties.IsPassword.ValueOrDefault == true);

    private bool TrySetTextValue(string text)
    {
        if (TryWriteValue(_element, text))
            return true;

        var nested = FindNestedTextBoxElement();
        return nested != null && TryWriteValue(nested, text);
    }

    /// <summary>
    /// Writes to one element, reporting whether its Value pattern accepted the write.
    /// </summary>
    private static bool TryWriteValue(AutomationElement target, string text)
    {
        try
        {
            if (!target.Patterns.Value.IsSupported)
                return false;

            var pattern = target.Patterns.Value.Pattern;
            if (pattern.IsReadOnly.Value)
                return false;

            pattern.SetValue(text);
            return true;
        }
        catch (Exception error) when (!FlaUIErrors.IsElementGone(error))
        {
            return false;
        }
    }


    #endregion

    #region Gestures (Bruinet UI Automation bridge)

    /// <summary>
    /// Whether the app under test declared this gesture on this element.
    /// </summary>
    private bool DeclaresGesture(MauiGesture gesture)
        => GestureRunner.Supports(_driver.RootElement, _driver.Automation, AutomationId, gesture, BridgeNear);

    /// <inheritdoc />
    /// <remarks>
    /// Nothing is cached between calls. A page can be navigated away from and back, and its
    /// bridge elements are republished each time with new runtime ids; an answer cached against
    /// this element would be about the previous incarnation.
    /// </remarks>
    /// <exception cref="GestureUnavailableException">
    /// The app publishes no bridge, this element was not declared, or the verb was refused.
    /// </exception>
    public void PerformGesture(MauiGesture gesture)
    {
        using var mark = MauiInteractions.Report(gesture, PhysicalRect, () => AutomationId);
        GestureRunner.Perform(
            _driver.RootElement, _driver.Automation, AutomationId, gesture, near: BridgeNear);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Appending is the one text operation that cannot be assembled out of a read and a write
    /// from this side: between the two calls the app is still running, and anything it does to
    /// the field lands in the middle. The bridge does both on one pass of the app's UI thread,
    /// which is why this is a verb of its own rather than sugar over <c>GetText</c> and
    /// <c>SetText</c>.
    /// </remarks>
    public void AppendText(string text)
    {
        using var mark = Report(InteractionKind.Type);

        if (!TryBridge(BruinetVerb.AppendText, text, out _, onlyRoute: true))
        {
            throw new BruinetException(
                $"'{AutomationId}' did not append the text. Either it does not declare AppendText, "
                + "or the app refused - a read-only or disabled field refuses the bridge as it "
                + "refuses a keyboard.");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The physical route for this is a Tab keystroke, which does not so much clear focus as
    /// move it to whatever happens to be next - a different operation with a visible side
    /// effect, and one that needs the app in front. The verb removes focus and does nothing
    /// else.
    /// </remarks>
    public void ClearFocus()
    {
        if (!TryBridge(BruinetVerb.Unfocus, string.Empty, out _, onlyRoute: true))
        {
            throw new BruinetException(
                $"'{AutomationId}' did not give up focus. Either it does not declare Unfocus, or "
                + "the app refused.");
        }
    }

    #region State the platform cannot be asked for

    /// <inheritdoc />
    /// <remarks>
    /// One bridge walk, not two. <see cref="BridgeVerbRunner.SendIfDeclared"/> resolves the
    /// target once and reports whether the verb was declared separately from whether it was
    /// answered, which is what lets null and the throw mean different things here. The pair of
    /// calls this replaced - a <c>SupportsStateReads</c> that walked the bridge, then a read that
    /// walked it again - could not tell them apart at all: a provider says
    /// <c>UIA_E_NOTSUPPORTED</c> both for a verb it never declared and for a name it does not
    /// handle.
    /// </remarks>
    public string? ReadState(string property)
    {
        var answer = BridgeVerbRunner.SendIfDeclared(
            _driver.RootElement, _driver.Automation, AutomationId, BruinetVerb.GetState, property,
            BridgeNear, out var declared);

        if (!declared)
        {
            return null;
        }

        if (!answer.Delivered)
        {
            throw new NotSupportedException(
                $"'{AutomationId}' declares GetState but has no case for '{property}'. The bridge "
                + "said: " + answer.Reason);
        }

        return answer.Value;
    }

    #endregion

    #region Scrolling

    /// <summary>Whether the app declares the scroll verbs on this element.</summary>
    private bool DeclaresScrollVerbs => BridgeDeclares(BruinetVerb.ScrollPosition);

    /// <inheritdoc />
    /// <remarks>
    /// <b>Returns when the viewport has stopped moving, not when the request was accepted.</b>
    /// MAUI's <c>ScrollToAsync</c> is asynchronous even with animation off - the app fires it and
    /// cannot await it, because the continuation needs the UI thread the verb is already holding.
    /// So the settling is done from this side, where waiting costs nothing.
    /// <para>
    /// Measured: without this, a scroll followed immediately by <c>ReadScrollPosition</c> returns
    /// the offset from before the scroll. It survived alone and failed under a full run, which is
    /// the shape that gets blamed on whatever else is going wrong at the time - in this case it
    /// spent months inside step 36's noise.
    /// </para>
    /// </remarks>
    public void ScrollTo(string automationId)
    {
        if (!TryBridge(BruinetVerb.ScrollTo, automationId, out _, onlyRoute: true))
        {
            throw new BruinetException(
                $"'{AutomationId}' could not scroll to '{automationId}'. Either the scroller does "
                + "not declare the ScrollTo verb, or nothing under it carries that AutomationId - "
                + "the verb searches the whole subtree, so a miss means the id is wrong or the "
                + "element is not in this scroller.");
        }

        WaitForTheViewportToSettle();
    }

    /// <summary>
    /// Blocks until two consecutive reads of the scroll offset agree.
    /// </summary>
    private void WaitForTheViewportToSettle()
    {
        if (!DeclaresScrollVerbs)
        {
            return;
        }

        var budgetMs = Bruinet.Core.Calls.CallBudget.Within(2_000);
        const int pollMs = 25;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var previous = ReadScrollPosition();

        while (clock.ElapsedMilliseconds < budgetMs)
        {
            Thread.Sleep(pollMs);

            var current = ReadScrollPosition();

            if (current.X == previous.X && current.Y == previous.Y)
            {
                return;
            }

            previous = current;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// One walk of the bridge decides the route and takes it. Where the app declares
    /// <c>ScrollToIndex</c> this jumps, settles as <see cref="ScrollToIndex"/> does, and reports
    /// <see cref="ScrollStep.Jumped"/>; otherwise it is a Scroll-pattern step. The pair this
    /// replaced - a <c>SupportsScrollToIndex</c> walk followed by a <c>ScrollToIndex</c> walk -
    /// asked the bridge the same question twice.
    /// </remarks>
    public ScrollStep ScrollTowards(int index)
    {
        var answer = BridgeVerbRunner.InvokeIfDeclared(
            _driver.RootElement, _driver.Automation, AutomationId,
            BruinetVerb.ScrollToIndex, index, 0, BridgeNear, out var declared);

        if (!declared)
        {
            return ScrollContent(1);
        }

        if (!answer.Delivered)
        {
            if (answer.HResult == HResults.BRUINET_E_DECLINED)
            {
                var itemCount = ReadState("ItemCount") ?? "unknown";
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"'{AutomationId}' declined index {index}; item count is {itemCount}.");
            }

            throw new BruinetException(
                $"'{AutomationId}' declares ScrollToIndex but could not scroll to index {index}. "
                + $"The bridge said: {answer.Reason}");
        }

        WaitForTheViewportToSettle();
        return ScrollStep.Jumped;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Settles like <see cref="ScrollTo(string)"/>, and for the same measured reason: MAUI's
    /// scroll is asynchronous whatever the animation setting says, so "the request was accepted"
    /// and "the rows have moved" are different moments. A collection has the further wrinkle that
    /// the row a caller is about to look for does not exist until the scroll lands.
    /// </remarks>
    public void ScrollToIndex(int index)
    {
        var answer = BridgeVerbRunner.Invoke(
            _driver.RootElement,
            _driver.Automation,
            AutomationId,
            BruinetVerb.ScrollToIndex,
            index,
            0,
            BridgeNear,
            onlyRoute: true);

        if (!answer.Delivered)
        {
            if (answer.HResult == HResults.BRUINET_E_DECLINED)
            {
                var itemCount = ReadState("ItemCount") ?? "unknown";
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"'{AutomationId}' declined index {index}; item count is {itemCount}.");
            }

            throw new BruinetException(
                $"'{AutomationId}' could not scroll to index {index}. Either it does not declare "
                + $"the ScrollToIndex verb or it is not a collection. The bridge said: {answer.Reason}");
        }

        WaitForTheViewportToSettle();
    }

    /// <inheritdoc />
    public ScrollPosition ReadScrollPosition()
    {
        if (!TryBridge(BruinetVerb.ScrollPosition, string.Empty, out var reported, onlyRoute: true))
        {
            throw new NotSupportedException(
                $"'{AutomationId}' does not answer ScrollPosition. Declare it with "
                + "uia:GestureAutomation.Verbs on the ScrollView in the app under test.");
        }

        var parts = reported.Split(',');
        if (parts.Length != 6)
        {
            throw new BruinetException(
                $"'{AutomationId}' answered ScrollPosition with '{reported}', which is not the "
                + "six comma-separated numbers the verb returns. The two ends of the bridge "
                + "disagree about the format.");
        }

        return new ScrollPosition(
            Number(parts[0]), Number(parts[1]), Number(parts[2]),
            Number(parts[3]), Number(parts[4]), Number(parts[5]));

        static double Number(string value)
            => double.Parse(value, CultureInfo.InvariantCulture);
    }

    #endregion

    #region Selection

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>One question, then one route.</b> The <c>SelectIndex</c> verb where the app declares it:
    /// nothing opens, and the app range-checks against its own item list, so an index past the end
    /// is refused by the only party that knows how many items there are. Otherwise the dropdown -
    /// open, select, close - which counts the items the popup rendered, a different number while a
    /// virtualized list is filling. With neither, a throw naming both.
    /// </para>
    /// <para>
    /// These were <c>SupportsSelectIndex</c> and <c>SupportsDropdown</c> questions in the control.
    /// </para>
    /// </remarks>
    public void SelectIndex(int index) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (BridgeDeclares(BruinetVerb.SelectIndex))
        {
            SelectIndexThroughTheApp(index);
            return;
        }

        if (HasDropdown)
        {
            if (!SelectFromDropdown(items => index >= 0 && index < items.Count ? items[index] : null))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"'{AutomationId}' has no item at index {index}.");
            }

            return;
        }

        throw NoSelectionRoute(BruinetVerb.SelectIndex);
    }));

    /// <inheritdoc />
    /// <remarks>Routes as <see cref="SelectIndex"/> does, with the <c>SelectByText</c> verb.</remarks>
    public void SelectByText(string text) => Marked(InteractionKind.Tap, () => Live(() =>
    {
        if (BridgeDeclares(BruinetVerb.SelectByText))
        {
            SelectByTextThroughTheApp(text);
            return;
        }

        if (HasDropdown)
        {
            if (!SelectFromDropdown(items => items.FirstOrDefault(i => i.Name == text || i.Text == text)))
            {
                throw new InvalidOperationException($"'{AutomationId}' has no item with text '{text}'.");
            }

            return;
        }

        throw NoSelectionRoute(BruinetVerb.SelectByText);
    }));

    private NotSupportedException NoSelectionRoute(BruinetVerb verb)
        => new(
            $"'{AutomationId ?? Name ?? "(unnamed)"}' cannot be selected from: the app does not "
            + $"declare the {verb} verb on it, and it exposes no UI Automation ExpandCollapse "
            + "pattern to open. Declare the verb with uia:GestureAutomation.Verbs in the app under test.");

    private void SelectIndexThroughTheApp(int index)
    {
        var outcome = BridgeVerbRunner.Invoke(
            _driver.RootElement, _driver.Automation, AutomationId, BruinetVerb.SelectIndex, index,
            near: BridgeNear, onlyRoute: true);

        if (outcome.Delivered)
        {
            return;
        }

        // Named per answer rather than as one list of suspects. The verb distinguishes four
        // things and they call for four different fixes, so collapsing them here would undo
        // the reason the app bothers to distinguish them.
        var reason = outcome.HResult switch
        {
            HResults.E_INVALIDARG => "the index is negative",

            HResults.UIA_E_ELEMENTNOTAVAILABLE =>
                "the index is past the end of its items, or its list has not filled yet",

            HResults.UIA_E_NOTSUPPORTED =>
                "it does not declare the SelectIndex verb, it is not a picker, or an earlier "
                + "item is equal to this one - MAUI cannot hold that selection, because it "
                + "resolves SelectedItem back to the first equal entry and the two never agree",

            _ => outcome.Reason,
        };

        throw new BruinetException($"'{AutomationId}' could not select index {index}: {reason}.");
    }

    /// <summary>Selects by text through the app's verb.</summary>
    private void SelectByTextThroughTheApp(string text)
    {
        if (!TryBridge(BruinetVerb.SelectByText, text, out var landed, onlyRoute: true))
        {
            throw new BruinetException(
                $"'{AutomationId}' could not select '{text}'. Either it does not declare the "
                + "SelectByText verb, or it is not a picker, or no item shows that text.");
        }

        if (landed != text)
        {
            throw new BruinetException(
                $"'{AutomationId}' was asked for '{text}' and now shows '{landed}'. The picker "
                + "declined the value, which a two-way bound SelectedItem can do.");
        }
    }

    #endregion

    #region Dates and times

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The app sets <c>DatePicker.Date</c> and reports what the control then holds, which is not
    /// always what was sent: a picker with a <c>MinimumDate</c> or <c>MaximumDate</c> clamps. A
    /// clamped write comes back as <c>S_FALSE</c> and is raised here rather than left for an
    /// assertion about something else to discover.
    /// </para>
    /// <para>
    /// Invariant format on the wire. The value is written by a test and read by an app that may
    /// be running under any culture, and a date meaning one thing at each end is how this fails
    /// in June and passes in July.
    /// </para>
    /// </remarks>
    public void SetDate(DateTime date)
    {
        using var mark = Report(InteractionKind.Type);

        var wanted = date.ToString(BridgeDateFormat, CultureInfo.InvariantCulture);

        if (!TryBridge(BruinetVerb.SetDate, wanted, out var landed, onlyRoute: true))
        {
            throw new NotSupportedException(
                $"'{AutomationId}' did not answer SetDate, and it is the only route. Declare it with "
                + "uia:GestureAutomation.Verbs on the DatePicker in the app under test.");
        }

        if (landed != wanted)
        {
            throw new BruinetException(
                $"'{AutomationId}' was set to {wanted} and now holds '{landed}'. A DatePicker "
                + "clamps to its MinimumDate and MaximumDate, which is the usual cause.");
        }
    }

    /// <inheritdoc />
    /// <remarks>See <see cref="SetDate"/>; the same reporting applies.</remarks>
    public void SetTime(TimeSpan time)
    {
        using var mark = Report(InteractionKind.Type);

        var wanted = time.ToString(BridgeTimeFormat, CultureInfo.InvariantCulture);

        if (!TryBridge(BruinetVerb.SetTime, wanted, out var landed, onlyRoute: true))
        {
            throw new NotSupportedException(
                $"'{AutomationId}' did not answer SetTime, and it is the only route. Declare it with "
                + "uia:GestureAutomation.Verbs on the TimePicker in the app under test.");
        }

        if (landed != wanted)
        {
            throw new BruinetException(
                $"'{AutomationId}' was set to {wanted} and now holds '{landed}'.");
        }
    }

    /// <summary>
    /// The wire format for a date, which both ends must agree on exactly.
    /// </summary>
    private const string BridgeDateFormat = "yyyy-MM-dd";

    /// <summary>The wire format for a time. See <see cref="BridgeDateFormat"/>.</summary>
    private const string BridgeTimeFormat = @"hh\:mm\:ss";

    #endregion

    #region The app

    /// <summary>How long the Shell chrome gets to appear when the app declares no flyout verbs.</summary>
    private const int ChromeFindTimeoutMs = 5000;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The app's <c>OpenFlyout</c> verb where it declares the flyout verbs: one property on the
    /// Shell, and no chrome to find.
    /// </para>
    /// <para>
    /// Otherwise the Shell's opener, found by name and invoked through its pattern. It sits in the
    /// window's title-bar strip, where a synthetic pointer click is intercepted before it reaches
    /// the button and the flyout simply never opens.
    /// </para>
    /// </remarks>
    public void OpenFlyout()
    {
        using var mark = Report(InteractionKind.Key, "Menu");

        RequireApp(nameof(OpenFlyout));

        if (_driver.SupportsFlyoutVerbs)
        {
            _driver.OpenFlyout();
            return;
        }

        _driver.FindChrome(Locator.ByName("Open Navigation"), ChromeFindTimeoutMs).Invoke();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The app's <c>CloseFlyout</c> verb where declared. Otherwise WinUI's light-dismiss layer,
    /// through its pattern: the layer covers the page, and a click aimed at it can land on whatever
    /// it is covering. Stage G step 32 measured that layer refusing Invoke, which is why the verb
    /// comes first.
    /// </remarks>
    public void CloseFlyout()
    {
        using var mark = Report(InteractionKind.Key, "Menu");

        RequireApp(nameof(CloseFlyout));

        if (_driver.SupportsFlyoutVerbs)
        {
            _driver.CloseFlyout();
            return;
        }

        _driver.FindChrome(Locator.ByAutomationId("LightDismiss"), ChromeFindTimeoutMs).Invoke();
    }

    /// <inheritdoc />
    public bool? IsFlyoutOpen
    {
        get
        {
            RequireApp(nameof(IsFlyoutOpen));
            return _driver.SupportsFlyoutVerbs ? _driver.IsFlyoutOpen() : null;
        }
    }

    /// <inheritdoc />
    public AlertContents? ReadAlert()
    {
        RequireApp(nameof(ReadAlert));
        return _driver.CurrentAlert();
    }

    /// <inheritdoc />
    public IMauiElement? TryFindActiveDialog()
    {
        RequireApp(nameof(TryFindActiveDialog));
        return _driver.TryFindActiveDialogRoot();
    }

    /// <inheritdoc />
    public void AnswerFileDialog(string path, int timeoutMs)
    {
        RequireApp(nameof(AnswerFileDialog));
        _driver.AnswerFileDialog(path, timeoutMs);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The bridge target for that id, found with the same raw walk every verb uses. Null when the
    /// app publishes no bridge or declares nothing with that id.
    /// </remarks>
    public IMauiElement? TryFindDeclared(string automationId)
    {
        RequireApp(nameof(TryFindDeclared));

        if (string.IsNullOrEmpty(automationId))
            return null;

        var target = BruinetBridgeLookup.Find(_driver.RootElement, _driver.Automation, automationId);
        return target is null ? null : new FlaUIDeclaredElement(automationId, target, _driver);
    }

    private void RequireApp(string operation)
    {
        if (!IsApp)
        {
            throw new NotSupportedException(
                $"{operation} is a question about the app, not about '{AutomationId ?? Name ?? "(unnamed)"}'. "
                + "Ask IMauiTestContext.AppElement.");
        }
    }

    #endregion

    private bool TryBridge(BruinetVerb verb) => TryBridge(verb, string.Empty, out _);

    /// <summary>Sends a verb that carries two numbers, and says whether the app performed it.</summary>
    /// <param name="verb">The verb to send.</param>
    /// <param name="arg1">First argument, meaning defined per verb.</param>
    /// <param name="arg2">Second argument, meaning defined per verb.</param>
    /// <returns>Whether the app performed it.</returns>
    private bool TryBridge(BruinetVerb verb, int arg1, int arg2)
        => BridgeVerbRunner.Invoke(
            _driver.RootElement, _driver.Automation, AutomationId, verb, arg1, arg2, BridgeNear).Delivered;

    /// <summary>This element's screen rectangle, to pick its target among ones sharing the id.</summary>
    private Rectangle BridgeNear() => _wrapped?.BoundingRectangle ?? Rectangle.Empty;

    /// <summary>
    /// Whether the app under test declares this verb on this element.
    /// </summary>
    /// <param name="verb">The verb.</param>
    /// <returns>Whether the bridge would carry it.</returns>
    private bool BridgeDeclares(BruinetVerb verb)
        => BridgeVerbRunner.Supports(_driver.RootElement, _driver.Automation, AutomationId, verb, BridgeNear);

    /// <summary>Sends a verb with an argument, and says whether the app performed it.</summary>
    /// <param name="verb">The verb to send.</param>
    /// <param name="argument">Its argument.</param>
    /// <param name="result">What the app returned.</param>
    /// <returns>Whether the app performed it.</returns>
    /// <param name="onlyRoute">Whether nothing else can do this; see <see cref="BridgeVerbRunner.Send"/>.</param>
    private bool TryBridge(BruinetVerb verb, string argument, out string result, bool onlyRoute = false)
    {
        var outcome = BridgeVerbRunner.Send(
            _driver.RootElement, _driver.Automation, AutomationId, verb, argument, BridgeNear, onlyRoute);

        result = outcome.Value;
        return outcome.Delivered;
    }

    #endregion
}
