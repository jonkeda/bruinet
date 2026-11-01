using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;
using System.Globalization;
using System.Runtime.CompilerServices;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Locators;
using Bruinet.Maui.Interfaces;
using Microsoft.Maui.Controls;
using MauiHeadless.Dialogs;
using MauiHeadless.Inspection;
using MauiHeadless.Interaction;
using MauiElement = Microsoft.Maui.Controls.Element;

namespace Bruinet.Maui.Headless;

/// <summary>
/// One MAUI element of a page rendered by MauiHeadless, as Bruinet's controls ask for it. State is read
/// from the element itself; actions go through MauiHeadless's interactions, so they run the control's own
/// events, commands and bindings and refuse what a user could not do, with the Bruinet exceptions
/// a device driver throws (<see cref="MauiHeadlessRefusals"/>).
/// </summary>
/// <remarks>
/// <para>
/// There is no layout: a visible element reports nominal 1x1 bounds at the origin, so Bruinet's
/// usable-bounds rule holds, and a hidden one reports none. Members a device alone can answer
/// (hover, pointer paths, pixels) throw <see cref="DeviceOnlyException"/>.
/// </para>
/// <para>
/// Where the app attaches a verb sink for Bruinet's Windows bridge, gestures and state reads go to
/// it first (<see cref="MauiHeadlessAppBridge"/>), as they do there.
/// </para>
/// </remarks>
public sealed class MauiHeadlessElement : IMauiElement
{
    private static readonly ConditionalWeakTable<MauiElement, string> Keys = new();
    private static readonly ConditionalWeakTable<Picker, StrongBox<bool>> OpenDropdowns = new();
    private static long s_nextKey;

    private readonly MauiHeadlessDriver _driver;
    private readonly bool _isApp;
    private readonly StepperPart _part;

    internal MauiHeadlessElement(MauiHeadlessDriver driver, MauiElement element, bool isApp = false)
        : this(driver, element, isApp, StepperPart.None)
    {
    }

    private MauiHeadlessElement(MauiHeadlessDriver driver, MauiElement element, bool isApp, StepperPart part)
    {
        _driver = driver;
        Element = element;
        _isApp = isApp;
        _part = part;
    }

    /// <summary>
    /// The two buttons a device draws inside a Stepper, which Bruinet's Stepper presses: published
    /// as its children, each stepping the value by Increment as the platform's button does.
    /// </summary>
    private enum StepperPart
    {
        None,
        Minus,
        Plus,
    }

    /// <summary>The MAUI element; for a Stepper's part, the Stepper.</summary>
    public MauiElement Element { get; }

    #region State

    // Visible only while still on screen: on the driver's window, on the page it shows. A device's
    // element of a page that has gone goes stale; headless the MAUI object lives on, and a container
    // that cached it (a Sync card, a list row) would read the old page's text without this.
    public bool Visible => AfterAppWork(() => IsOnScreen() && VisualTree.IsEffectivelyVisible(Element));

    public bool Enabled => AfterAppWork(() => VisualTree.IsEffectivelyEnabled(Element) && PartEnabled());

    public bool Selected => AfterAppWork(() => Element switch
    {
        RadioButton radio => radio.IsChecked,
        CheckBox box => box.IsChecked,
        ShellSection tab => tab.Parent is ShellItem item && ReferenceEquals(item.CurrentItem, tab) && item.Parent is Shell shell && ReferenceEquals(shell.CurrentItem, item),
        ShellItem flyoutItem => flyoutItem.Parent is Shell owner && ReferenceEquals(owner.CurrentItem, flyoutItem),
        _ => IsSelectedRow(),
    });

    public string? Text => _part switch
    {
        StepperPart.Minus => "-",
        StepperPart.Plus => "+",
        _ => AfterAppWork(() => VisualTree.AdapterFor(Element, _driver.TestContext.Adapters).GetText(Element)),
    };

    // Headless the app works only when asked; a device's app works between two reads. A state read
    // runs the queued work first (an answered request continued, a page closed), so a test polling
    // one element, as a cached page root's bounds are, sees the app move on. The read itself runs as
    // the UI thread would, with the app current, as an adapter's text may read its resources.
    private T AfterAppWork<T>(Func<T> read)
    {
        _driver.TestContext.Dispatcher.RunPending();
        return _driver.TestContext.Dispatcher.Run(read);
    }

    public string? TagName => _part == StepperPart.None ? Element.GetType().Name : "RepeatButton";

    public Point Location => Point.Empty;

    public Size Size => Rect.Size;

    public Rectangle Rect => Visible ? new Rectangle(0, 0, 1, 1) : Rectangle.Empty;

    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name is "ClassName" or "class")
            return TagName;
        if (name is "Name" or "name")
            return Name;

        return BindableValue(name);
    }

    public string? ReadState(string property)
    {
        ArgumentNullException.ThrowIfNull(property);

        // The bridge's own names first, then what the app's sink answers, as on Windows.
        return MauiHeadlessState.Read(Element, property)
               ?? MauiHeadlessAppBridge.TryExchange(Element, "GetState", property)
               ?? throw new NotSupportedException(
                   $"{TagName} #{AutomationId} has no state '{property}': neither Bruinet's state names nor an app sink answer it.");
    }

    #endregion

    #region Basic actions

    public void Click() => Invoke();

    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
        => Do(() =>
        {
            var input = Input("type into");
            input.SetText(method == TextInputMethod.SetValue ? text : (input.Text ?? string.Empty) + text);
        });

    public void Clear() => Do(() => Input("clear").ClearText());

    public void DoubleClick() => PerformGesture(MauiGesture.DoubleTap);

    public void RightClick() => throw new DeviceOnlyException(nameof(RightClick));

    public void Hover() => throw new DeviceOnlyException(nameof(Hover));

    public void LongPress(int durationMs = 1000) => PerformGesture(MauiGesture.LongPress);

    // Every element is in the tree and on "screen": there is nothing to scroll to.
    public void ScrollIntoView(int timeoutMs)
    {
    }

    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500)
        => throw new DeviceOnlyException(nameof(Swipe));

    #endregion

    #region Child lookup

    public IMauiElement? TryFindElement(Locator locator) => FindElements(locator).FirstOrDefault();

    public IReadOnlyList<IMauiElement> FindElements(Locator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (_part != StepperPart.None)
            return [];

        // A Stepper's buttons are drawn by the platform, not declared in XAML.
        if (Element is Stepper && locator is { Strategy: LocatorStrategy.ClassName, Value: "RepeatButton" }
                or { Strategy: LocatorStrategy.ControlType, Value: "button" })
            return [Part(StepperPart.Minus), Part(StepperPart.Plus)];

        return _driver.Find(Element, locator);
    }

    #endregion

    #region Identity

    public string InstanceKey
        => Keys.GetValue(Element, _ => Interlocked.Increment(ref s_nextKey).ToString(CultureInfo.InvariantCulture))
           + (_part == StepperPart.None ? "" : "/" + _part);

    public string? AutomationId => _part == StepperPart.None ? Element.AutomationId : Element.AutomationId + _part;

    public string? Name => _part == StepperPart.None ? NameOf(Element) : Text;

    public bool Focused => Element is VisualElement { IsFocused: true };

    public string? Hint => Element switch
    {
        InputView input => input.Placeholder,
        Picker picker => picker.Title,
        _ => null,
    };

    #endregion

    #region Activation

    public void Invoke() => Do(() =>
    {
        if (_part != StepperPart.None)
        {
            var stepper = (Stepper)Element;
            stepper.StepTo(stepper.Value + (_part == StepperPart.Plus ? stepper.Increment : -stepper.Increment));
            return;
        }

        switch (Element)
        {
            case Microsoft.Maui.Controls.Button button:
                button.Click();
                break;
            case ImageButton button:
                button.Click();
                break;
            case MenuItem item:
                item.Click();
                break;
            case NavigationBackButton back:
                back.Click();
                break;
            case Switch toggle:
                toggle.Toggle();
                break;
            case CheckBox box:
                box.Toggle();
                break;
            case RadioButton radio:
                radio.Select();
                break;
            case ShellSection tab:
                tab.Select();
                break;
            case ShellItem flyoutItem:
                flyoutItem.Select();
                break;
            case MauiElement row when TrySelectRow(row):
                break;
            case View view when MauiHeadlessAppBridge.Names(view, "Tap"):
                MauiHeadlessAppBridge.TryInvoke(view, "Tap", 0, 0);
                break;
            case View view:
                view.Tap();
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} has no primary action.");
        }
    });

    public void Toggle() => Do(() =>
    {
        switch (Element)
        {
            case Switch toggle:
                toggle.Toggle();
                break;
            case CheckBox box:
                box.Toggle();
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} cannot be toggled.");
        }
    });

    public void Select() => Invoke();

    public void InvokeToolbarItem(string automationId) => _driver.InvokeToolbarItem(automationId);

    #endregion

    #region Gestures

    public void PerformGesture(MauiGesture gesture) => PerformGesture(gesture, 0, 0);

    /// <summary>
    /// The gesture as the Windows bridge performs it: the app's sink when it names the verb, else
    /// the control's own answer (a tap through its recognizers; a swipe on a SwipeView, a
    /// CarouselView, a RefreshView or a SwipeGestureRecognizer).
    /// </summary>
    internal void PerformGesture(MauiGesture gesture, int arg1, int arg2) => Do(() =>
    {
        if (MauiHeadlessAppBridge.TryInvoke(Element, gesture.ToString(), arg1, arg2))
            return;

        var view = Element as View
            ?? throw new ElementNotInteractableException($"{TagName} #{AutomationId} takes no gestures.");
        switch (gesture)
        {
            case MauiGesture.Tap:
                view.Tap();
                break;
            case MauiGesture.DoubleTap:
                view.Tap(taps: 2);
                break;
            case MauiGesture.SwipeLeft:
                view.Swipe(SwipeDirection.Left);
                break;
            case MauiGesture.SwipeRight:
                view.Swipe(SwipeDirection.Right);
                break;
            case MauiGesture.SwipeUp:
                view.Swipe(SwipeDirection.Up);
                break;
            case MauiGesture.SwipeDown:
                view.Swipe(SwipeDirection.Down);
                break;
            case MauiGesture.LongPress:
                throw new ElementNotInteractableException(
                    $"Cannot long-press {TagName} #{AutomationId}: MAUI has no long-press gesture recognizer; attach a sink that names LongPress.");
            default:
                throw new ElementNotInteractableException(
                    $"Cannot {gesture} {TagName} #{AutomationId}: MAUI's Send{gesture} is internal; attach a sink that names {gesture}.");
        }
    });

    /// <summary>Whether <see cref="PerformGesture(MauiGesture, int, int)"/> has a route for <paramref name="gesture"/>.</summary>
    internal bool SupportsGesture(MauiGesture gesture)
    {
        if (MauiHeadlessAppBridge.Names(Element, gesture.ToString()))
            return true;
        if (Element is not View view)
            return false;

        return gesture switch
        {
            MauiGesture.Tap => view.GestureRecognizers.OfType<TapGestureRecognizer>().Any(r => r.NumberOfTapsRequired == 1),
            MauiGesture.DoubleTap => view.GestureRecognizers.OfType<TapGestureRecognizer>().Any(r => r.NumberOfTapsRequired == 2),
            MauiGesture.SwipeLeft or MauiGesture.SwipeRight or MauiGesture.SwipeUp or MauiGesture.SwipeDown
                => view is SwipeView or CarouselView
                   || (gesture == MauiGesture.SwipeDown && view is RefreshView or ContentView { Content: RefreshView })
                   || view.GestureRecognizers.OfType<SwipeGestureRecognizer>().Any(),
            _ => false,
        };
    }

    #endregion

    #region Checked, text field, range

    public bool? Checked => Element switch
    {
        Switch toggle => toggle.IsToggled,
        CheckBox box => box.IsChecked,
        RadioButton radio => radio.IsChecked,
        _ => null,
    };

    public void SetChecked(bool isChecked) => Do(() =>
    {
        switch (Element)
        {
            case Switch toggle:
                toggle.SetToggled(isChecked);
                break;
            case CheckBox box:
                box.SetChecked(isChecked);
                break;
            case RadioButton radio when isChecked:
                radio.Select();
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} has no checked state to set.");
        }
    });

    public bool? IsReadOnly => Element is InputView input ? input.IsReadOnly : null;

    public double? RangeValue => Element switch { Slider s => s.Value, Stepper s => s.Value, ProgressBar p => p.Progress, _ => null };

    public double? RangeMinimum => Element switch { Slider s => s.Minimum, Stepper s => s.Minimum, ProgressBar => 0, _ => null };

    public double? RangeMaximum => Element switch { Slider s => s.Maximum, Stepper s => s.Maximum, ProgressBar => 1, _ => null };

    public double? RangeSmallChange => Element is Stepper stepper ? stepper.Increment : null;

    public void SetRangeValue(double value) => Do(() =>
    {
        switch (Element)
        {
            case Slider slider:
                slider.SlideTo(value);
                break;
            case Stepper stepper:
                stepper.StepTo(value);
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} has no range to set.");
        }
    });

    public void AppendText(string text) => SendKeys(text, TextInputMethod.Keys);

    public void ClearFocus()
    {
        if (Element is VisualElement visual)
            visual.ClearFocus();
    }

    public void Focus()
        => Do(() => (Element as VisualElement ?? throw new ElementNotInteractableException($"{TagName} cannot take focus.")).SetFocus());

    public void Submit() => Do(() =>
    {
        switch (Element)
        {
            case Entry entry:
                entry.Complete();
                break;
            case SearchBar search:
                search.Search();
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} cannot be submitted.");
        }
    });

    #endregion

    #region Scrolling

    // Every row is realized and nothing is off screen, so a scroll has nowhere to go: it succeeds
    // at once where the target exists, and says so where it does not.

    public ScrollStep ScrollContent(int verticalSteps, int horizontalSteps = 0)
        => Element is ScrollView or ItemsView
            ? ScrollStep.NotMoved
            : throw new BruinetException($"{TagName} #{AutomationId} does not scroll.");

    // Scrolling reveals what is off screen, and nothing is: what a lookup missed, a scroll cannot
    // find. Answering with a lookup here made a container's miss an app-wide hit.
    public IMauiElement? TryFindByScrolling(Locator locator) => null;

    public void ScrollTo(string automationId)
    {
        if (_driver.Find(Element, Locator.ByAutomationId(automationId)).Count == 0)
            throw new BruinetException($"{TagName} #{AutomationId} holds no element with AutomationId '{automationId}' to scroll to.");
    }

    public ScrollStep ScrollTowards(int index)
    {
        ScrollToIndex(index);
        return ScrollStep.Jumped;
    }

    public void ScrollToIndex(int index)
    {
        var count = Element switch
        {
            ItemsView items => items.ItemsSource?.Cast<object>().Count() ?? 0,
            _ => throw new BruinetException($"{TagName} #{AutomationId} has no items to scroll to."),
        };
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"{TagName} #{AutomationId}: {index} is past the end, its item count is {count}.");
    }

    public ScrollPosition ReadScrollPosition() => throw new DeviceOnlyException(nameof(ReadScrollPosition));

    #endregion

    #region Selection, dropdowns, dates and times

    public string? SelectedItemText => Element is Picker ? Text : null;

    public IReadOnlyList<string>? ReadItemTexts() => Element switch
    {
        Picker picker => [.. picker.Items],
        ItemsView => _driver.Read(Element).Children.Select(FirstText).OfType<string>().ToList(),
        _ => null,
    };

    public bool? IsDropdownOpen => Element is Picker picker ? OpenDropdowns.GetValue(picker, _ => new StrongBox<bool>()).Value : null;

    public void OpenDropdown()
    {
        if (Element is not Picker picker)
            throw new BruinetException($"{TagName} #{AutomationId} has no dropdown.");
        Do(() => Interactions.Act(picker, "open", () => OpenDropdowns.GetValue(picker, _ => new StrongBox<bool>()).Value = true));
    }

    public void CloseDropdown()
    {
        if (Element is Picker picker)
            OpenDropdowns.GetValue(picker, _ => new StrongBox<bool>()).Value = false;
    }

    public IReadOnlyList<string>? ReadDropdownItemTexts() => Element is Picker picker ? [.. picker.Items] : null;

    public void SelectIndex(int index) => Do(() =>
    {
        switch (Element)
        {
            case Picker picker:
                picker.SelectIndex(index);
                break;
            case SelectableItemsView view:
                var items = view.ItemsSource?.Cast<object>().ToList() ?? [];
                if (index < 0 || index >= items.Count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"{TagName} #{AutomationId} has {items.Count} items.");
                view.Select(items[index]);
                break;
            case CarouselView carousel:
                Interactions.Act(carousel, "swipe", () => carousel.Position = index);
                break;
            case View view when MauiHeadlessAppBridge.Names(view, "SelectIndex"):
                MauiHeadlessAppBridge.TryInvoke(view, "SelectIndex", index, 0);
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} has no items to select.");
        }
    });

    public void SelectByText(string text) => Do(() =>
    {
        switch (Element)
        {
            case Picker picker:
                picker.Select(text);
                break;
            case SelectableItemsView view:
                var row = _driver.Read(view).Children.FirstOrDefault(child => FirstText(child) == text)
                    ?? throw new ElementNotInteractableException($"{TagName} #{AutomationId} shows no row '{text}'.");
                view.Select(row.BindingContext!);
                break;
            default:
                throw new ElementNotInteractableException($"{TagName} #{AutomationId} has no items to select.");
        }
    });

    public void SetDate(DateTime date)
        => Do(() => (Element as DatePicker ?? throw new ElementNotInteractableException($"{TagName} is no date picker.")).SetDate(date));

    public void SetTime(TimeSpan time)
        => Do(() => (Element as TimePicker ?? throw new ElementNotInteractableException($"{TagName} is no time picker.")).SetTime(time));

    #endregion

    #region The app

    public bool? IsFlyoutOpen
        => _isApp
            ? _driver.Window.Page switch { Shell shell => shell.FlyoutIsPresented, FlyoutPage flyout => flyout.IsPresented, _ => false }
            : throw new NotSupportedException("Ask IMauiTestContext.AppElement.");

    public void OpenFlyout() => SetFlyout(true);

    public void CloseFlyout() => SetFlyout(false);

    // The dialog on top of the window: what the app raised with DisplayAlertAsync, DisplayPromptAsync
    // or DisplayActionSheetAsync, as MauiHeadless shows it (TestDialog).
    public AlertContents? ReadAlert()
    {
        if (!_isApp)
            throw new NotSupportedException("Ask IMauiTestContext.AppElement.");

        _driver.TestContext.Dispatcher.RunPending();
        return DialogRequests.Top(_driver.Window) is { } dialog
            ? new AlertContents(dialog.Title, dialog.Message, dialog.Accept, dialog.Cancel)
            : null;
    }

    public IMauiElement? TryFindActiveDialog()
    {
        if (!_isApp)
            throw new NotSupportedException("Ask IMauiTestContext.AppElement.");

        _driver.TestContext.Dispatcher.RunPending();
        return DialogRequests.Top(_driver.Window) is { } dialog ? new MauiHeadlessElement(_driver, dialog) : null;
    }

    public IMauiElement? TryFindDeclared(string automationId)
        => _isApp
            ? _driver.Find(_driver.Window, Locator.ByAutomationId(automationId), includeHidden: true).FirstOrDefault()
            : throw new NotSupportedException("Ask IMauiTestContext.AppElement.");

    #endregion

    #region Web content and forms

    public string? GetDomAttribute(string attributeName) => null;

    public string? GetDomProperty(string propertyName) => null;

    public string? GetCssValue(string propertyName) => null;

    #endregion

    /// <summary>
    /// The accessible name: the semantic description, else the automation name, else what the
    /// platforms announce for a control that shows text (a button's or label's text, a tab's or a
    /// dialog's title).
    /// </summary>
    internal static string? NameOf(MauiElement element)
        => SemanticProperties.GetDescription(element)
           ?? AutomationProperties.GetName(element)
           ?? element switch
           {
               TestDialog dialog => dialog.Title,
               Microsoft.Maui.Controls.Button button => button.Text,
               Label label => DefaultElementAdapter.Instance.GetText(label),
               MenuItem item => item.Text,
               BaseShellItem shellItem => shellItem.Title,
               _ => null,
           };

    public override string ToString() => $"{TagName} #{AutomationId}";

    private void Do(Action action) => MauiHeadlessRefusals.Run(Element, action);

    // Under the driver's window, and its nearest page is the one shown or one that contains it (a
    // NavigationPage, a Shell, tabs). Window chrome and dialogs have no page of their own above them.
    private bool IsOnScreen()
    {
        if (_isApp)
            return true;

        // Up to the window, which is the Application's child, not the top.
        Page? nearestPage = null;
        var onWindow = false;
        for (var walker = Element; walker is not null; walker = walker.Parent)
        {
            if (walker is Window)
            {
                onWindow = ReferenceEquals(walker, _driver.Window);
                break;
            }

            nearestPage ??= walker as Page;
        }

        if (!onWindow)
            return false;
        if (nearestPage is null)
            return true;

        for (MauiElement? shown = Screen.CurrentPage(_driver.Window); shown is not null; shown = shown.Parent)
        {
            if (ReferenceEquals(shown, nearestPage))
                return true;
        }

        return false;
    }

    private MauiHeadlessElement Part(StepperPart part) => new(_driver, Element, isApp: false, part);

    private bool PartEnabled() => _part switch
    {
        StepperPart.Minus => ((Stepper)Element).Value > ((Stepper)Element).Minimum,
        StepperPart.Plus => ((Stepper)Element).Value < ((Stepper)Element).Maximum,
        _ => true,
    };

    private string? BindableValue(string name)
    {
        // Any bindable property by its name: "IsToggled", "Placeholder", "Text".
        var field = Element.GetType().GetField(name + "Property",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
        return field?.GetValue(null) is BindableProperty property
            ? Convert.ToString(Element.GetValue(property), CultureInfo.InvariantCulture)
            : null;
    }

    private InputView Input(string verb)
        => Element as InputView ?? throw new ElementNotInteractableException($"Cannot {verb} {TagName} #{AutomationId}: it is no text field.");

    private void SetFlyout(bool open)
    {
        if (!_isApp)
            throw new NotSupportedException("Ask IMauiTestContext.AppElement.");

        switch (_driver.Window.Page)
        {
            case Shell shell:
                _driver.TestContext.Dispatcher.Run(() => shell.FlyoutIsPresented = open);
                break;
            case FlyoutPage flyout:
                _driver.TestContext.Dispatcher.Run(() => flyout.IsPresented = open);
                break;
            default:
                throw new BruinetException("The app has no flyout.");
        }
    }

    private bool IsSelectedRow()
        => Element.Parent is SelectableItemsView view && Element.BindingContext is { } item
           && (Equals(view.SelectedItem, item) || view.SelectedItems?.Contains(item) == true);

    private static bool TrySelectRow(MauiElement row)
    {
        if (row.Parent is not SelectableItemsView { SelectionMode: not SelectionMode.None } view || row.BindingContext is not { } item)
            return false;

        view.Select(item);
        return true;
    }

    private static string? FirstText(VisualNode node)
        => node.DescendantsAndSelf().Select(n => n.Text).FirstOrDefault(text => !string.IsNullOrEmpty(text));
}
