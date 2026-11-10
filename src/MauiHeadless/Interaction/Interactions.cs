using System.Collections;
using Microsoft.Maui.Controls;
using MauiHeadless.Inspection;
using MauiHeadless.Internals;

namespace MauiHeadless.Interaction;

/// <summary>
/// What a user does to a control, done through the control's own entry points so its events,
/// commands and two-way bindings run as on a device. Each runs on the context's dispatcher and
/// returns once the work it queued has run.
/// </summary>
/// <remarks>
/// Every interaction first checks the user could do it: the element is visible and enabled (and an
/// input is not read-only). A button whose command cannot execute is disabled by MAUI itself, so it
/// is refused too.
/// </remarks>
public static class Interactions
{
    #region Buttons and taps

    /// <summary>Clicks the button: Pressed, Released, then Clicked and its Command.</summary>
    public static Button Click(this Button button)
    {
        Act(button, "click", () =>
        {
            button.SendPressed();
            button.SendReleased();
            button.SendClicked();
        });
        return button;
    }

    /// <summary>Clicks the image button: Pressed, Released, then Clicked and its Command.</summary>
    public static ImageButton Click(this ImageButton button)
    {
        Act(button, "click", () =>
        {
            button.SendPressed();
            button.SendReleased();
            button.SendClicked();
        });
        return button;
    }

    /// <summary>Chooses a toolbar or menu item: its Clicked and Command.</summary>
    public static MenuItem Click(this MenuItem item)
    {
        Act(item, "click", () => ((IMenuItemController)item).Activate());
        return item;
    }

    /// <summary>
    /// Taps the view <paramref name="taps"/> times in a row, as the platform routes a touch: the
    /// recognizers on the view that want that many taps, else those on the single content it wraps
    /// (a ContentView or Border filled by one child), else those on the nearest ancestor up to the
    /// page. A touch inside a list row that nothing above takes selects the row, as on a device: a
    /// CollectionView row by its item, a ListView cell through ItemTapped.
    /// </summary>
    /// <exception cref="ElementNotInteractableException">Nothing on that path takes that many taps.</exception>
#pragma warning disable CS0618 // ListView is obsolete, but apps still show it.
    public static TView Tap<TView>(this TView view, int taps = 1) where TView : View
    {
        foreach (var target in TapPath(view))
        {
            switch (target)
            {
                case View candidate when Recognizers(candidate, taps) is { Count: > 0 } recognizers:
                    Act(view, "tap", () =>
                    {
                        foreach (var recognizer in recognizers)
                            MauiInternals.SendTapped(recognizer, candidate);
                    });
                    return view;
                case View row when taps == 1 && row.Parent is ItemsView items && ItemRealizer.TryGetItem(row, out var item):
                    Act(view, "tap", () =>
                    {
                        if (items is SelectableItemsView { SelectionMode: not SelectionMode.None } selectable && item is not null)
                            Select(selectable, item);
                    });
                    return view;
                case Cell cell when taps == 1 && cell.Parent is ListView list:
                    Act(view, "tap", () =>
                    {
                        var index = ((ITemplatedItemsView<Cell>)list).TemplatedItems.ToList().IndexOf(cell);
                        if (index >= 0)
                            list.NotifyRowTapped(index, cell);
                    });
                    return view;
            }
        }

        throw new ElementNotInteractableException($"Cannot tap {Name(view)}: it has no TapGestureRecognizer for {taps} tap(s), nor does its content or a parent.");
    }

    private static List<TapGestureRecognizer> Recognizers(View view, int taps)
        => view.GestureRecognizers.OfType<TapGestureRecognizer>()
            .Where(recognizer => recognizer.NumberOfTapsRequired == taps)
            .ToList();

    // The view, the single content it wraps going down, then its ancestors up to the row, page or list.
    private static IEnumerable<Element> TapPath(View view)
    {
        yield return view;
        for (var content = SingleContent(view); content is not null; content = SingleContent(content))
            yield return content;

        for (var parent = view.Parent; parent is not null and not Page and not ItemsView and not ListView; parent = parent.Parent)
            yield return parent;
    }
#pragma warning restore CS0618

    private static View? SingleContent(View view) => view switch
    {
        ContentView { Content: { } content } => content,
        Border { Content: { } content } => content,
        _ => null,
    };

    // A row's select, as a touch makes it: the same as Select, without its refusals.
    private static void Select(SelectableItemsView view, object item)
    {
        if (view.SelectionMode == SelectionMode.Single)
            view.SelectedItem = item;
        else if (!view.SelectedItems.Contains(item))
            view.SelectedItems.Add(item);
    }

    #endregion

    #region Text input

    /// <summary>
    /// Types <paramref name="text"/> over what the field holds: Text changes (TextChanged, two-way
    /// bindings), cut at MaxLength as the platforms cut it.
    /// </summary>
    public static TInput SetText<TInput>(this TInput input, string? text) where TInput : InputView
    {
        EnsureWritable(input, "type into");
        // Typed text goes through the platform's field, which shows it in the field's TextTransform and
        // gives the transformed text back to Text.
        Act(input, "type into", () =>
            input.Text = TextTransforms.Apply(
                text is not null && input.MaxLength >= 0 && text.Length > input.MaxLength
                    ? text[..input.MaxLength]
                    : text,
                input.TextTransform));
        return input;
    }

    /// <summary>Empties the field.</summary>
    public static TInput ClearText<TInput>(this TInput input) where TInput : InputView => input.SetText(string.Empty);

    /// <summary>Presses return in the entry: Completed and ReturnCommand.</summary>
    public static Entry Complete(this Entry entry)
    {
        Act(entry, "complete", entry.SendCompleted);
        return entry;
    }

    /// <summary>Presses the search button: SearchButtonPressed and SearchCommand.</summary>
    public static SearchBar Search(this SearchBar searchBar)
    {
        Act(searchBar, "search", ((ISearchBarController)searchBar).OnSearchButtonPressed);
        return searchBar;
    }

    #endregion

    #region Toggles

    /// <summary>Flips the switch.</summary>
    public static Switch Toggle(this Switch toggle) => toggle.SetToggled(!toggle.IsToggled);

    /// <summary>Sets the switch on or off; already there is no change and raises nothing.</summary>
    public static Switch SetToggled(this Switch toggle, bool on)
    {
        Act(toggle, "toggle", () => toggle.IsToggled = on);
        return toggle;
    }

    /// <summary>Flips the check box.</summary>
    public static CheckBox Toggle(this CheckBox box) => box.SetChecked(!box.IsChecked);

    /// <summary>Checks the check box.</summary>
    public static CheckBox Check(this CheckBox box) => box.SetChecked(true);

    /// <summary>Unchecks the check box.</summary>
    public static CheckBox Uncheck(this CheckBox box) => box.SetChecked(false);

    /// <summary>Sets the check box; already there is no change and raises nothing.</summary>
    public static CheckBox SetChecked(this CheckBox box, bool isChecked)
    {
        Act(box, "check", () => box.IsChecked = isChecked);
        return box;
    }

    /// <summary>Chooses the radio button; its group unchecks the others.</summary>
    public static RadioButton Select(this RadioButton radio)
    {
        Act(radio, "select", () => radio.IsChecked = true);
        return radio;
    }

    #endregion

    #region Focus

    /// <summary>
    /// Gives the element focus, as a tap into it does: whatever had focus in its window loses it
    /// (Unfocused), then this one gains it (Focused). MAUI's own <c>Focus()</c> needs a platform.
    /// </summary>
    public static TElement SetFocus<TElement>(this TElement element) where TElement : VisualElement
    {
        Act(element, "focus", () =>
        {
            foreach (var other in FocusedIn(Top(element)).Where(other => !ReferenceEquals(other, element)))
                MauiInternals.SetFocused(other, false);
            MauiInternals.SetFocused(element, true);
        });
        return element;
    }

    /// <summary>Takes focus away from the element (Unfocused), as tapping elsewhere does.</summary>
    public static TElement ClearFocus<TElement>(this TElement element) where TElement : VisualElement
    {
        var context = MauiTestContext.Of(element);
        if (context is null)
            MauiInternals.SetFocused(element, false);
        else
            context.Dispatcher.Run(() => MauiInternals.SetFocused(element, false));
        return element;
    }

    private static Element Top(Element element)
    {
        var top = element;
        while (top.Parent is { } parent)
            top = parent;
        return top;
    }

    private static IEnumerable<VisualElement> FocusedIn(Element root)
        => VisualTree.Read(root).DescendantsAndSelf().Select(node => node.Element).OfType<VisualElement>().Where(e => e.IsFocused).ToList();

    #endregion

    #region Swipes

    /// <summary>
    /// Swipes a finger across the view in <paramref name="direction"/>, as each control answers a
    /// swipe: a SwipeView opens the items the finger uncovers (right opens the left items), a
    /// CarouselView moves one card along its axis, a RefreshView pulled down refreshes, and any
    /// other view runs its SwipeGestureRecognizers for that direction.
    /// </summary>
    /// <exception cref="ElementNotInteractableException">Nothing answers that swipe, or the control declines it.</exception>
    public static TView Swipe<TView>(this TView view, SwipeDirection direction) where TView : View
    {
        switch (view)
        {
            case SwipeView swipeView:
                // Without a platform view Open only announces itself; the items' side is MAUI's to show.
                Act(swipeView, "swipe", () => swipeView.Open(direction switch
                {
                    SwipeDirection.Right => OpenSwipeItem.LeftItems,
                    SwipeDirection.Left => OpenSwipeItem.RightItems,
                    SwipeDirection.Down => OpenSwipeItem.TopItems,
                    _ => OpenSwipeItem.BottomItems,
                }, animated: false));
                return view;

            case CarouselView carousel when CarouselStep(carousel, direction) is { } step:
                var count = carousel.ItemsSource?.Cast<object?>().Count() ?? 0;
                var target = carousel.Position + step;
                if (carousel.Loop && count > 0)
                    target = ((target % count) + count) % count;
                if (!carousel.IsSwipeEnabled || count == 0 || target < 0 || target >= count)
                    throw new ElementNotInteractableException($"Cannot swipe {Name(carousel)} {direction}: there is no card that way.");
                Act(carousel, "swipe", () => carousel.Position = target);
                return view;

            case RefreshView refresh when direction == SwipeDirection.Down:
                if (refresh.IsRefreshing)
                    throw new ElementNotInteractableException($"Cannot pull {Name(refresh)}: it is refreshing already.");
                refresh.Refresh();
                return view;
        }

        // A view that keeps a RefreshView inside, however deep (a reusable list control's private one),
        // is pulled through it, as a finger pulling the list pulls its refresh. Two would be a guess.
        if (direction == SwipeDirection.Down && view is Layout or ContentView or Border or ScrollView)
        {
            var inside = VisualTree.Read(view).DescendantsAndSelf().Skip(1)
                .Select(node => node.Element).OfType<RefreshView>().Distinct().ToList();
            if (inside.Count > 1)
                throw new ElementNotInteractableException(
                    $"Cannot pull {Name(view)}: it holds {inside.Count} RefreshViews ({string.Join(", ", inside.Select(Name))}); pull one of them.");
            if (inside.Count == 1)
            {
                inside[0].Swipe(direction);
                return view;
            }
        }

        var recognizers = view.GestureRecognizers.OfType<SwipeGestureRecognizer>()
            .Where(recognizer => recognizer.Direction.HasFlag(direction))
            .ToList();
        // A finger on a SwipeView's content drags the SwipeView when nothing under it takes the swipe (D19).
        if (recognizers.Count == 0 && EnclosingSwipeView(view, direction) is { } enclosing)
        {
            enclosing.Swipe(direction);
            return view;
        }

        if (recognizers.Count == 0)
            throw new ElementNotInteractableException(
                $"Cannot swipe {Name(view)} {direction}: it is not a SwipeView, CarouselView or RefreshView, and no SwipeGestureRecognizer takes {direction}.");

        Act(view, "swipe", () =>
        {
            foreach (var recognizer in recognizers)
                recognizer.SendSwiped(view, direction);
        });
        return view;
    }

    /// <summary>The nearest SwipeView around <paramref name="view"/> with items on the side a swipe toward <paramref name="direction"/> uncovers.</summary>
    private static SwipeView? EnclosingSwipeView(View view, SwipeDirection direction)
    {
        for (var parent = view.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is SwipeView swipeView)
                return (direction switch
                {
                    SwipeDirection.Right => swipeView.LeftItems,
                    SwipeDirection.Left => swipeView.RightItems,
                    SwipeDirection.Down => swipeView.TopItems,
                    _ => swipeView.BottomItems,
                }) is { Count: > 0 } ? swipeView : null;
        }

        return null;
    }

    private static int? CarouselStep(CarouselView carousel, SwipeDirection direction)
    {
        var vertical = carousel.ItemsLayout is LinearItemsLayout { Orientation: ItemsLayoutOrientation.Vertical };
        return (direction, vertical) switch
        {
            (SwipeDirection.Left, false) or (SwipeDirection.Up, true) => 1,
            (SwipeDirection.Right, false) or (SwipeDirection.Down, true) => -1,
            _ => null,
        };
    }

    #endregion

    #region Ranges, dates and times

    /// <summary>Drags the slider to <paramref name="value"/>: DragStarted, ValueChanged, DragCompleted; MAUI clamps it.</summary>
    public static Slider SlideTo(this Slider slider, double value)
    {
        Act(slider, "slide", () =>
        {
            var controller = (ISliderController)slider;
            controller.SendDragStarted();
            slider.Value = value;
            controller.SendDragCompleted();
        });
        return slider;
    }

    /// <summary>Steps the stepper to <paramref name="value"/>; MAUI clamps it.</summary>
    public static Stepper StepTo(this Stepper stepper, double value)
    {
        Act(stepper, "step", () => stepper.Value = value);
        return stepper;
    }

    /// <summary>Picks a date.</summary>
    /// <exception cref="ElementNotInteractableException">The date is outside MinimumDate and MaximumDate.</exception>
    public static DatePicker SetDate(this DatePicker picker, DateTime date)
    {
        if (date < picker.MinimumDate || date > picker.MaximumDate)
            throw new ElementNotInteractableException(
                $"Cannot pick {date:yyyy-MM-dd} in {Name(picker)}: it allows MinimumDate {picker.MinimumDate:yyyy-MM-dd} to MaximumDate {picker.MaximumDate:yyyy-MM-dd}.");

        Act(picker, "pick a date in", () => picker.Date = date);
        return picker;
    }

    /// <summary>Picks a time.</summary>
    public static TimePicker SetTime(this TimePicker picker, TimeSpan time)
    {
        Act(picker, "pick a time in", () => picker.Time = time);
        return picker;
    }

    #endregion

    #region Selection

    /// <summary>Picks <paramref name="item"/>: one of ItemsSource, or one of Items by its text.</summary>
    /// <exception cref="ElementNotInteractableException">The picker does not offer it.</exception>
    public static Picker Select(this Picker picker, object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = picker.ItemsSource is { } source
            ? IndexOf(source, item)
            : picker.Items.IndexOf(item as string ?? item.ToString() ?? string.Empty);
        if (index < 0)
            throw new ElementNotInteractableException($"Cannot pick '{item}' in {Name(picker)}: it does not offer it.");

        return picker.SelectIndex(index);
    }

    /// <summary>Picks the item at <paramref name="index"/>.</summary>
    /// <exception cref="ElementNotInteractableException">
    /// No item there, or an earlier item is equal to it: MAUI resolves the selection by equality,
    /// so it would hold the earlier one, and a device would show that one too.
    /// </exception>
    public static Picker SelectIndex(this Picker picker, int index)
    {
        var items = picker.ItemsSource is { } source ? source.Cast<object?>().ToList() : picker.Items.Cast<object?>().ToList();
        if (index < 0 || index >= items.Count)
            throw new ElementNotInteractableException($"Cannot pick item {index} in {Name(picker)}: it is past the end of its {items.Count} items.");
        if (items.Take(index).Any(earlier => Equals(earlier, items[index])))
            throw new ElementNotInteractableException(
                $"Cannot pick item {index} in {Name(picker)}: an earlier item is equal to this one, so MAUI would select that one instead.");

        Act(picker, "pick in", () => picker.SelectedIndex = index);
        return picker;
    }

    /// <summary>
    /// Selects <paramref name="item"/>, as tapping its row does: SelectedItem (single) or SelectedItems
    /// (multiple), SelectionChanged and SelectionChangedCommand.
    /// </summary>
    /// <exception cref="ElementNotInteractableException">
    /// Selection is off, or the item is not in ItemsSource (in one of its groups, for a grouped list).
    /// </exception>
    public static TView Select<TView>(this TView view, object item) where TView : SelectableItemsView
    {
        if (view.SelectionMode == SelectionMode.None)
            throw new ElementNotInteractableException($"Cannot select in {Name(view)}: its SelectionMode is None.");
        if (view.ItemsSource is not { } source || !Holds(view, source, item))
            throw new ElementNotInteractableException(view is GroupableItemsView { IsGrouped: true }
                ? $"Cannot select '{item}' in {Name(view)}: it is in none of the groups in ItemsSource."
                : $"Cannot select '{item}' in {Name(view)}: it is not in ItemsSource.");

        Act(view, "select in", () =>
        {
            if (view.SelectionMode == SelectionMode.Single)
                view.SelectedItem = item;
            else if (!view.SelectedItems.Contains(item))
                view.SelectedItems.Add(item);
        });
        return view;
    }

    /// <summary>Clears <paramref name="item"/> from the selection, as tapping a selected row in multiple mode does.</summary>
    public static TView Deselect<TView>(this TView view, object item) where TView : SelectableItemsView
    {
        Act(view, "deselect in", () =>
        {
            if (view.SelectionMode == SelectionMode.Single)
            {
                if (Equals(view.SelectedItem, item))
                    view.SelectedItem = null;
            }
            else
            {
                view.SelectedItems.Remove(item);
            }
        });
        return view;
    }

    /// <summary>
    /// Presses the title bar's back button: the top modal is popped, else the window's page is told
    /// the back button was pressed, so the page's <c>OnBackButtonPressed</c> may keep it, and
    /// NavigationPage and Shell pop.
    /// </summary>
    /// <exception cref="ElementNotInteractableException">There is nothing to go back to, so no button is shown.</exception>
    public static NavigationBackButton Click(this NavigationBackButton back)
    {
        if (!back.CanGoBack)
            throw new ElementNotInteractableException("Cannot press back: there is nothing to go back to.", InteractionRefusal.Hidden);

        Act(back, "press back", () =>
        {
            if (back.Window.Navigation.ModalStack.Count > 0)
                _ = back.Window.Navigation.PopModalAsync();
            else
                back.Window.Page?.SendBackButtonPressed();
        });
        return back;
    }

    /// <summary>Taps a Shell tab: its item and section become current, and Shell navigates there.</summary>
    public static ShellSection Select(this ShellSection tab)
    {
        if (tab.Parent is not ShellItem item || item.Parent is not Shell shell)
            throw new ElementNotInteractableException($"Cannot select tab '{tab.Title}': it is in no Shell.");

        Act(tab, "select", () =>
        {
            shell.CurrentItem = item;
            item.CurrentItem = tab;
        });
        return tab;
    }

    /// <summary>Taps a Shell flyout entry: it becomes current, and the flyout closes.</summary>
    public static ShellItem Select(this ShellItem item)
    {
        if (item.Parent is not Shell shell)
            throw new ElementNotInteractableException($"Cannot select '{item.Title}': it is in no Shell.");

        Act(item, "select", () =>
        {
            shell.CurrentItem = item;
            shell.FlyoutIsPresented = false;
        });
        return item;
    }

    /// <summary>Pulls to refresh: IsRefreshing and the Command.</summary>
    public static RefreshView Refresh(this RefreshView view)
    {
        Act(view, "refresh", () => view.IsRefreshing = true);
        return view;
    }

    #endregion

    /// <summary>
    /// Runs <paramref name="action"/> as an interaction with <paramref name="element"/>: refused when a user
    /// could not reach the element, else run on its context's dispatcher with what it queued.
    /// </summary>
    /// <exception cref="ElementNotInteractableException">The element is hidden or disabled.</exception>
    public static void Act(Element element, string verb, Action action)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(action);
        var context = MauiTestContext.Of(element);
        context?.Dispatcher.RunPending();

        if (!VisualTree.IsEffectivelyVisible(element))
            throw new ElementNotInteractableException($"Cannot {verb} {Name(element)}: it is not visible (IsVisible is false on it or an ancestor).", InteractionRefusal.Hidden);
        if (!VisualTree.IsEffectivelyEnabled(element))
            throw new ElementNotInteractableException($"Cannot {verb} {Name(element)}: it is disabled (IsEnabled is false on it or an ancestor, or its command cannot execute).", InteractionRefusal.Disabled);

        if (context is null)
            action();
        else
            context.Dispatcher.Run(action);
    }

    private static void EnsureWritable(InputView input, string verb)
    {
        if (input.IsReadOnly)
            throw new ElementNotInteractableException($"Cannot {verb} {Name(input)}: it is read-only.", InteractionRefusal.ReadOnly);
    }

    // A grouped list's ItemsSource holds the groups, and its items are in them.
    private static bool Holds(SelectableItemsView view, IEnumerable source, object item)
        => view is GroupableItemsView { IsGrouped: true }
            ? source.Cast<object?>().OfType<IEnumerable>().Any(group => IndexOf(group, item) >= 0)
            : IndexOf(source, item) >= 0;

    private static int IndexOf(IEnumerable source, object item)
    {
        var index = 0;
        foreach (var candidate in source)
        {
            if (Equals(candidate, item))
                return index;
            index++;
        }

        return -1;
    }

    private static string Name(Element element)
        => element.GetType().Name + (string.IsNullOrEmpty(element.AutomationId) ? "" : $" #{element.AutomationId}");
}
