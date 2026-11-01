using System.Globalization;
using Bruinet.Uia;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// Every public MAUI API the bridge drives, in one file.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why they are gathered here.</b> When MAUI changes, the failure should be a compile error
/// in one known file rather than a gesture that silently stops working in a suite nobody has
/// run this week. Everything the dispatcher does to a MAUI control goes through a method here.
/// </para>
/// <para>
/// <b>Nothing here reflects into MAUI internals.</b> <c>SendTapped</c>, <c>SendPinch</c> and
/// <c>SendPan</c> are internal in MAUI 10, and reaching them by reflection would make every
/// MAUI upgrade a runtime failure discovered by a test rather than a build failure discovered
/// by a compiler. Where no public API exists, the verb is not supported and says so.
/// </para>
/// </remarks>
internal static class MauiCapabilities
{
    // ---- Gestures -----------------------------------------------------------------
    //
    // Every method below is either a question or a command, and none of them is both. That is
    // what removed the Try prefix from this section: TryTap used to search for a recognizer and
    // execute its command in one call, which is why it could not report which half had failed.
    // The searches now happen once, when an element is published (see VerbBindings), and what is
    // left here are plain actions that need no return value because the search already answered.

    /// <summary>
    /// Opens the swipe items a swipe in the given direction would reveal.
    /// </summary>
    /// <remarks>
    /// Opened without animation. A test that waits for an animation is a test with a sleep in
    /// it, and the state the assertion is about is reached either way.
    /// </remarks>
    /// <param name="swipeView">The view to open.</param>
    /// <param name="item">Which items to reveal, from <see cref="SwipeItemFor"/>.</param>
    internal static void OpenSwipeView(SwipeView swipeView, OpenSwipeItem item)
        => swipeView.Open(item, false);

    /// <summary>Which swipe items a swipe in this direction uncovers.</summary>
    /// <remarks>
    /// <para>
    /// <b>The mapping is inverted, and that is correct.</b> Swiping your finger to the right
    /// drags the content right and uncovers what is behind its left edge, so
    /// <see cref="BruinetVerb.SwipeRight"/> opens <see cref="OpenSwipeItem.LeftItems"/>. It
    /// reads like a bug and is the single most likely thing here to be "fixed" backwards, which
    /// is why <c>SwipeMappingTests</c> pins it.
    /// </para>
    /// <para>
    /// A question, and named like one: null means "that verb is not a swipe", which is an answer
    /// rather than a failure.
    /// </para>
    /// </remarks>
    /// <param name="verb">The swipe verb.</param>
    /// <returns>The items revealed, or null if the verb is not a swipe.</returns>
    internal static OpenSwipeItem? SwipeItemFor(BruinetVerb verb) => verb switch
    {
        BruinetVerb.SwipeRight => OpenSwipeItem.LeftItems,
        BruinetVerb.SwipeLeft => OpenSwipeItem.RightItems,
        BruinetVerb.SwipeDown => OpenSwipeItem.TopItems,
        BruinetVerb.SwipeUp => OpenSwipeItem.BottomItems,
        _ => null,
    };

    /// <summary>Closes an open swipe view.</summary>
    /// <param name="swipeView">The view to close.</param>
    internal static void CloseSwipeView(SwipeView swipeView) => swipeView.Close(false);

    /// <summary>
    /// Starts a pull-to-refresh.
    /// </summary>
    /// <remarks>
    /// Setting <c>IsRefreshing</c> is what the gesture itself does; the view raises
    /// <c>Refreshing</c> and runs its command from the property change. Whether a refresh is
    /// already running is the caller's question to ask - <c>RefreshView.IsRefreshing</c> answers
    /// it - and asking it there is what lets the bridge report "already refreshing" as its own
    /// state rather than as a failure to start one.
    /// </remarks>
    /// <param name="refreshView">The view to refresh.</param>
    internal static void StartRefresh(RefreshView refreshView) => refreshView.IsRefreshing = true;

    /// <summary>
    /// How many cards a swipe in this direction moves a carousel, given its orientation.
    /// </summary>
    /// <remarks>
    /// Content follows the finger, so a swipe left on a horizontal carousel brings in the card
    /// to the right: the next one. A swipe across the carousel's axis does not move it, and
    /// answers null, as does any verb that is not a swipe.
    /// </remarks>
    /// <param name="carousel">The carousel.</param>
    /// <param name="verb">The swipe verb.</param>
    /// <returns>+1 for next, -1 for previous, or null.</returns>
    internal static int? CarouselStepFor(CarouselView carousel, BruinetVerb verb)
    {
        var vertical = carousel.ItemsLayout is LinearItemsLayout { Orientation: ItemsLayoutOrientation.Vertical };
        return (verb, vertical) switch
        {
            (BruinetVerb.SwipeLeft, false) or (BruinetVerb.SwipeUp, true) => 1,
            (BruinetVerb.SwipeRight, false) or (BruinetVerb.SwipeDown, true) => -1,
            _ => null,
        };
    }

    /// <summary>
    /// Moves a carousel one card, as a swipe would.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Position, not the scroller.</b> Measured on Windows (2026-09-18, MAUI 10): a UI
    /// Automation scroll of the carousel's list brings the next card on screen, but MAUI never
    /// hears of it - <c>Position</c>, the bound view model and the IndicatorView all stay on the
    /// old card. Setting <c>Position</c> is what a finished swipe does, and everything bound to
    /// it follows.
    /// </para>
    /// <para>
    /// A carousel that cannot move - swiping disabled, or at either end without
    /// <c>Loop</c> - reports false, which the caller turns into a refusal rather than a success.
    /// </para>
    /// </remarks>
    /// <param name="carousel">The carousel.</param>
    /// <param name="step">+1 for the next card, -1 for the previous one.</param>
    /// <returns>Whether the carousel moved.</returns>
    internal static bool StepCarousel(CarouselView carousel, int step)
    {
        var count = Count(carousel.ItemsSource);
        if (!carousel.IsSwipeEnabled || count == 0)
        {
            return false;
        }

        var target = carousel.Position + step;
        if (carousel.Loop)
        {
            target = ((target % count) + count) % count;
        }
        else if (target < 0 || target >= count)
        {
            return false;
        }

        carousel.Position = target;
        return true;
    }

    // ---- Dates and times ----------------------------------------------------------
    //
    // Two properties, and that is the entire feature. What it replaces on the client side is
    // roughly two hundred lines of WinUI calendar navigation - open the flyout, read the header,
    // page to the right month, find the day, select it - written because there was no other way
    // to set a date without a pointer. The app has always been able to just say so.

    /// <summary>
    /// The one format a date crosses the wire in.
    /// </summary>
    /// <remarks>
    /// Invariant and round-trippable, deliberately. The value is written by a test and read by an
    /// app that may be running under any culture, and a date that means one thing on one side of
    /// the wire and another on the other is the classic way for this to fail in June and pass in
    /// July.
    /// </remarks>
    internal const string DateFormat = "yyyy-MM-dd";

    /// <summary>The one format a time crosses the wire in. See <see cref="DateFormat"/>.</summary>
    /// <remarks>
    /// Twenty-four hour, and that is the point: <c>TimePicker</c> reading 15:30 back as 03:30 is
    /// an open defect against the flyout route this verb exists to replace.
    /// </remarks>
    internal const string TimeFormat = @"hh\:mm\:ss";

    /// <summary>Sets a date picker's date.</summary>
    /// <param name="picker">The picker.</param>
    /// <param name="date">The date, already parsed.</param>
    internal static void SetDate(DatePicker picker, DateTime date) => picker.Date = date;

    /// <summary>Sets a time picker's time.</summary>
    /// <param name="picker">The picker.</param>
    /// <param name="time">The time, already parsed.</param>
    internal static void SetTime(TimePicker picker, TimeSpan time) => picker.Time = time;

    /// <summary>Reads a date picker's date in the wire format.</summary>
    /// <remarks>
    /// <b>Both of these are nullable in MAUI 10</b> - <c>DatePicker.Date</c> is
    /// <c>DateTime?</c> and <c>TimePicker.Time</c> is <c>TimeSpan?</c> - which is easy to miss
    /// because the pickers always show something. An empty string is the honest reading of a
    /// picker holding no value, and it is distinguishable from every real one.
    /// </remarks>
    /// <param name="picker">The picker.</param>
    /// <returns>The date, or empty if it holds none.</returns>
    internal static string ReadDate(DatePicker picker)
        => picker.Date?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Reads a time picker's time in the wire format.</summary>
    /// <param name="picker">The picker.</param>
    /// <returns>The time, or empty if it holds none.</returns>
    internal static string ReadTime(TimePicker picker)
        => picker.Time?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    // ---- Scrolling ------------------------------------------------------------------
    //
    // ScrollToAsync and ScrollTo are cross-platform MAUI APIs, which makes this the largest
    // parity win in the catalogue: the same verb does the same thing on Windows, Android and
    // iOS. What it replaces on Windows is a loop of mouse-wheel clicks - an unquantified unit,
    // with no completion signal, so the caller polled a scroll percentage and guessed when it
    // had stopped moving.

    /// <summary>
    /// Brings a named descendant into view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never animated.</b> An animated scroll finishes some time after the call returns, so a
    /// test either sleeps or races; unanimated, the layout is settled when the await completes
    /// and the position the caller reads back is the real one.
    /// </para>
    /// <para>
    /// The search is by <c>AutomationId</c> and covers the whole subtree, because the thing a
    /// test wants to scroll to is usually nested several layouts deep inside the scroller.
    /// </para>
    /// </remarks>
    /// <param name="scrollView">The scroller.</param>
    /// <param name="automationId">The descendant to reveal.</param>
    /// <returns>Whether a descendant with that id was found.</returns>
    internal static bool ScrollTo(ScrollView scrollView, string automationId)
    {
        var target = Descendants(scrollView)
            .FirstOrDefault(child => child.AutomationId == automationId);

        if (target is null)
        {
            return false;
        }

        // Fire and forget is deliberate and safe here: with animation off, MAUI applies the
        // scroll synchronously and the returned task completes on the next tick. Awaiting it
        // would mean marshalling a result back through a verb that has nothing to report.
        _ = scrollView.ScrollToAsync(target, ScrollToPosition.MakeVisible, false);
        return true;
    }

    /// <summary>
    /// Brings the item at an index into view.
    /// </summary>
    /// <remarks>
    /// <c>ItemsView</c> covers <c>CollectionView</c> and <c>CarouselView</c>; <c>ListView</c> is
    /// separate and scrolls to an <i>item</i> rather than an index, so its index has to be
    /// resolved against <c>ItemsSource</c> first. That difference is MAUI's, not ours.
    /// </remarks>
    /// <param name="element">The collection.</param>
    /// <param name="index">The item index.</param>
    /// <returns>Whether the element is a collection that could scroll.</returns>
    internal static bool ScrollToIndex(VisualElement element, int index)
    {
        switch (element)
        {
            case ItemsView itemsView:
                // Range-checked rather than left to MAUI. An index past the end is how a caller
                // walking a virtualized list finds out it has reached the end, so it has to be a
                // quiet no and not an exception crossing the bridge as a failure.
                if (index < 0 || index >= Count(itemsView.ItemsSource))
                {
                    return false;
                }

                itemsView.ScrollTo(index, position: ScrollToPosition.MakeVisible, animate: false);
                return true;

#pragma warning disable CS0618 // ListView is deprecated, and apps under test still use it
            case ListView listView:
#pragma warning restore CS0618
                var item = listView.ItemsSource?.Cast<object>().ElementAtOrDefault(index);
                if (item is null)
                {
                    return false;
                }

                listView.ScrollTo(item, ScrollToPosition.MakeVisible, animated: false);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Where a scroller is, and how much there is to scroll.
    /// </summary>
    /// <remarks>
    /// <b>Offset and extent together, because neither means anything alone.</b> A test asserting
    /// "we are at the bottom" needs the offset, the viewport and the content size to say so; a
    /// percentage - which is what UI Automation offers - cannot distinguish a short page that
    /// cannot scroll from a long one already at the end.
    /// </remarks>
    /// <param name="scrollView">The scroller.</param>
    /// <returns>"x,y,viewportWidth,viewportHeight,contentWidth,contentHeight", invariant.</returns>
    internal static string ReadScrollPosition(ScrollView scrollView)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5}",
            scrollView.ScrollX,
            scrollView.ScrollY,
            scrollView.Width,
            scrollView.Height,
            scrollView.ContentSize.Width,
            scrollView.ContentSize.Height);

    /// <summary>How many items a source holds, without enumerating it if it can be asked.</summary>
    /// <param name="source">The items source, possibly null.</param>
    /// <returns>The count, or zero.</returns>
    internal static int Count(System.Collections.IEnumerable? source) => source switch
    {
        null => 0,
        System.Collections.ICollection collection => collection.Count,
        _ => source.Cast<object>().Count(),
    };

    /// <summary>Every element below this one, depth first.</summary>
    /// <remarks>
    /// <c>IVisualTreeElement</c> rather than <c>LogicalChildrenInternal</c>, which is internal in
    /// MAUI 10 - and this file does not reflect into MAUI internals, for the reason stated at the
    /// top of it. The visual tree is also the more correct one to walk here: a test scrolls to
    /// something it can see.
    /// </remarks>
    /// <param name="root">Where to start. Not included in the result.</param>
    /// <returns>The descendants.</returns>
    private static IEnumerable<VisualElement> Descendants(IVisualTreeElement root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is VisualElement visual)
            {
                yield return visual;
            }

            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    // ---- State the platform cannot be asked for ------------------------------------
    //
    // Each of these replaces an assertion that passed for the wrong reason. That is a worse
    // failure than one that does not pass at all: a green test is a claim nobody re-examines.

    /// <summary>
    /// Where an image's bitmap comes from, as the app declared it.
    /// </summary>
    /// <remarks>
    /// <b>What the client had instead was rendered size.</b> "An image is loaded if it occupies
    /// space" - and a broken one occupies exactly as much space as a working one, because the
    /// layout reserves it either way. The test that asserted an image had loaded was therefore
    /// asserting that MAUI had done arithmetic. The source is app state and never reaches the
    /// accessibility tree, which is why the old comment was right that nothing else could be
    /// observed - from outside the app.
    /// </remarks>
    /// <param name="image">The image.</param>
    /// <returns>The source, in a form a test can compare, or empty when there is none.</returns>
    internal static string ReadImageSource(Image image) => image.Source switch
    {
        null => string.Empty,
        FileImageSource file => file.File ?? string.Empty,
        UriImageSource uri => uri.Uri?.ToString() ?? string.Empty,

        // Stream and font sources have nothing stable to compare, so they report their kind
        // rather than a fabricated identity. A test asserting on those wants IsLoading anyway.
        _ => image.Source.GetType().Name,
    };

    /// <summary>Whether an image is still fetching its bitmap.</summary>
    /// <remarks>
    /// The other half of the same problem: <c>IsLoading</c> is how an image says it has not
    /// finished, and a size check cannot distinguish "still fetching" from "failed" from "done".
    /// </remarks>
    /// <param name="image">The image.</param>
    /// <returns>Whether a load is in flight.</returns>
    internal static bool IsImageLoading(Image image) => image.IsLoading;

    /// <summary>
    /// A progress bar's progress, in MAUI's own units.
    /// </summary>
    /// <remarks>
    /// <b>0 to 1, with no normalising.</b> The client reads this through the UIA range pattern,
    /// where WinUI reports 0-100, and rescales it against the reported minimum and maximum. That
    /// arithmetic is correct and is also a second place for the definition of "progress" to live
    /// - so a platform that reported a different range, or none, would quietly change what the
    /// number meant. The app holds one number and this is it.
    /// </remarks>
    /// <param name="bar">The progress bar.</param>
    /// <returns>Progress from 0 to 1.</returns>
    internal static double ReadProgress(ProgressBar bar) => bar.Progress;

    /// <summary>
    /// Whether the app has finished the work it had queued.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A real drained-dispatcher signal, which is what <c>AD-004</c> asks for.</b> "No
    /// arbitrary sleeps" is only followable if there is something to wait *on*; without one, a
    /// test that needs the UI to settle either sleeps or invents a sentinel element whose
    /// appearance approximates settling.
    /// </para>
    /// <para>
    /// Posting to the dispatcher and waiting for the callback establishes that everything queued
    /// before the call has run - which is the definition of idle that matters to a test, and the
    /// only one obtainable from inside. It does not promise that nothing new will be queued:
    /// an app with a running animation or a timer is never idle by any definition, and saying so
    /// is better than a number that hides it.
    /// </para>
    /// </remarks>
    /// <param name="element">Any element, for its dispatcher.</param>
    /// <param name="timeoutMs">How long to wait for the queue to drain.</param>
    /// <returns>Whether the queue drained within the budget.</returns>
    internal static bool IsIdle(VisualElement element, int timeoutMs)
    {
        var dispatcher = element.Dispatcher;
        if (dispatcher is null)
        {
            return true;
        }

        using var drained = new ManualResetEventSlim(false);

        // Queued at the back, so it runs after everything already waiting. Whether it runs at
        // all within the budget is the answer.
        if (!dispatcher.Dispatch(() => drained.Set()))
        {
            return false;
        }

        return drained.Wait(timeoutMs);
    }

    // ---- Selection ------------------------------------------------------------------
    //
    // A picker's items are the app's own list, and every question about them was answered from
    // outside by opening the dropdown and reading the popup. That read moves the app - the
    // flyout opens, the items render, the flyout closes - so asking what a picker holds changed
    // what a user would see, and two identical questions in a row were two different journeys.
    // Selecting had the same shape, plus a two-second poll waiting for the popup's items to
    // appear in the tree.
    //
    // OpenFlyout is deliberately absent. MAUI has no public API to open a Picker's dropdown; it
    // belongs to the platform control, and an app cannot answer that verb honestly without
    // reaching into WinUI. The client opens it through the ExpandCollapse pattern instead, which
    // is what that pattern is for. A test that means "the flyout opens and shows these items"
    // still says exactly that - it is only selecting and reading that no longer have to.

    /// <summary>
    /// Selects a picker's item by position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Four answers, not two.</b> A negative index is nonsense the caller can never be right
    /// about (<see cref="HResults.E_INVALIDARG"/>). An index past the end is well formed and
    /// names nothing - which for a picker bound to a source still filling is worth retrying, so
    /// it is <see cref="HResults.UIA_E_ELEMENTNOTAVAILABLE"/> rather than a refusal. A control
    /// that is not a picker refuses. Only a landed selection is
    /// <see cref="HResults.S_OK"/>, and a two-way binding that declines the value reports
    /// <see cref="HResults.S_FALSE"/> rather than letting the caller assume.
    /// </para>
    /// <para>
    /// <c>Picker.Items</c> is the display strings, kept in step with <c>ItemsSource</c> by MAUI
    /// itself - so this counts what the user can see rather than what the view model holds.
    /// </para>
    /// </remarks>
    /// <param name="element">The element the verb arrived on.</param>
    /// <param name="index">The item position.</param>
    /// <returns>The HRESULT to answer with.</returns>
    internal static int SelectIndex(VisualElement element, int index)
    {
        if (element is not Picker picker)
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        if (index < 0)
        {
            return HResults.E_INVALIDARG;
        }

        if (index >= picker.Items.Count)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        // Refused rather than attempted, because attempting it hangs the app.
        //
        // MAUI keeps SelectedIndex and SelectedItem in step by setting each from the other:
        // SelectedIndex sets SelectedItem to the item at that position, and SelectedItem sets
        // SelectedIndex back to IndexOf(item). Where IndexOf cannot return the index it was
        // given - the item is equal to an earlier one - the two never agree, and the pair
        // ping-pongs forever on the UI thread. Measured: one core at 100%, the window stops
        // responding, and every later verb times out. It is MAUI's, not the bridge's; the app's
        // own code doing the same assignment freezes it identically, with no automation running.
        //
        // SelectByText needs no such guard: it selects the position IndexOf gives it, so the two
        // already agree.
        if (FirstEqualPosition(picker, index) != index)
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        picker.SelectedIndex = index;
        // BRUINET_E_DECLINED rather than S_FALSE: a binding that refuses the index is a refusal
        // the caller has to be able to hear, and a success code cannot carry it across the wire.
        return picker.SelectedIndex == index
            ? HResults.S_OK
            : HResults.BRUINET_E_DECLINED;
    }

    /// <summary>
    /// Where MAUI would find the item at this position when asked to look it up.
    /// </summary>
    /// <remarks>
    /// Matched to what MAUI itself does: it searches <c>ItemsSource</c> when there is one, which
    /// compares items rather than the text they render - so two distinct objects displaying the
    /// same string are fine, and two equal entries are not.
    /// </remarks>
    /// <param name="picker">The picker.</param>
    /// <param name="index">The position being selected.</param>
    /// <returns>The position MAUI's own lookup would return.</returns>
    private static int FirstEqualPosition(Picker picker, int index)
    {
        if (picker.ItemsSource is { } source)
        {
            return index < source.Count ? source.IndexOf(source[index]) : index;
        }

        return picker.Items.IndexOf(picker.Items[index]);
    }

    /// <summary>
    /// Selects a picker's item by the text it shows.
    /// </summary>
    /// <remarks>
    /// <b>Ordinal, and the first match wins.</b> Two items may read alike - the sample has a
    /// picker where they do - and this verb cannot tell them apart, which is not a defect but
    /// the reason <see cref="SelectIndex"/> exists beside it. What it will not do is guess: the
    /// text now selected comes back, so a caller that asked for an ambiguous one can see which
    /// it got without a second round trip.
    /// </remarks>
    /// <param name="element">The element the verb arrived on.</param>
    /// <param name="text">The item text to select.</param>
    /// <param name="selected">The text the picker now shows.</param>
    /// <returns>The HRESULT to answer with.</returns>
    internal static int SelectByText(VisualElement element, string text, out string selected)
    {
        selected = string.Empty;

        if (element is not Picker picker)
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        var index = picker.Items.IndexOf(text);
        if (index < 0)
        {
            selected = ReadSelectedItem(picker);
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        picker.SelectedIndex = index;
        selected = ReadSelectedItem(picker);

        return selected == text ? HResults.S_OK : HResults.S_FALSE;
    }

    /// <summary>The text a picker is showing, or empty when it holds no selection.</summary>
    /// <remarks>
    /// Read out of <c>Items</c> by index rather than from <c>SelectedItem</c>, which is the
    /// source object and not what the control renders when <c>ItemDisplayBinding</c> is set.
    /// </remarks>
    /// <param name="picker">The picker.</param>
    /// <returns>The displayed text, or empty.</returns>
    internal static string ReadSelectedItem(Picker picker)
        => picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count
            ? picker.Items[picker.SelectedIndex]
            : string.Empty;

    /// <summary>
    /// Everything a picker offers, as a count and then one item per line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The count is on the wire so that a lie is detectable.</b> Items are arbitrary user
    /// text and one of them may contain a newline, which would silently split into two - and a
    /// test comparing item lists would then be comparing a different list without being told.
    /// Sending the count the app counted lets the far end notice the disagreement and say so,
    /// the way <c>ScrollPosition</c>'s fixed six numbers do.
    /// </para>
    /// <para>
    /// One read rather than one per item: the alternative was opening the dropdown and walking
    /// the popup, which is a read that changes what it reads.
    /// </para>
    /// </remarks>
    /// <param name="picker">The picker.</param>
    /// <returns>The count, then the items, newline separated.</returns>
    internal static string ReadItems(Picker picker)
        => string.Join(
            "\n",
            new[] { picker.Items.Count.ToString(CultureInfo.InvariantCulture) }
                .Concat(picker.Items));

    // ---- Menus and flyouts ----------------------------------------------------------
    //
    // The menu range exists because of a measurement: NavigationProbeTests reports that
    // 'PageMenuFile' and 'PageMenuFileNew' are addressable neither by AutomationId nor by name
    // on Windows. MAUI does not propagate AutomationId to menu chrome (dotnet/maui#3996), so a
    // menu item is not a control a test can find and press - it is invisible to the very tree
    // the test is looking at. That is the SwipeView situation again, and it has the same answer:
    // ask the app by id, because the app is the only party that has the id.
    //
    // What it replaces is worse than a wrong route. A context menu could only be reached by
    // right-clicking at a coordinate and then clicking a popup entry at another coordinate -
    // physical, positional, and requiring the app to hold the foreground, which is the thing
    // stage B exists to stop.

    /// <summary>
    /// What <see cref="InvokeMenuItem"/> answers with when it declined to raise the item.
    /// </summary>
    /// <remarks>
    /// <b>A value rather than <c>S_FALSE</c>, because <c>S_FALSE</c> does not survive the
    /// bridge.</b> UI Automation reports every success HRESULT from a custom pattern as
    /// <c>S_OK</c>, measured from both ends: the app returns 1, the client reads 0. Anything a
    /// caller has to tell apart from plain success must therefore travel as a value or as a
    /// failure HRESULT.
    /// </remarks>
    internal const string MenuItemDisabled = "disabled";

    /// <summary>
    /// Raises a menu item by its <c>AutomationId</c>, without opening any menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>IMenuItemController.Activate</c> is MAUI's own entry point for "the user picked
    /// this".</b> It is what each platform's handler calls when a menu entry is clicked, so the
    /// app cannot tell this apart from the real thing: the same <c>Command</c> runs and the same
    /// <c>Clicked</c> fires. Setting properties by hand, or driving the popup, would both be
    /// imitations of it.
    /// </para>
    /// <para>
    /// Opening the menu is deliberately not part of this. An open menu is a different state of
    /// the app - it takes focus and covers the page - and a verb that opened one on the way past
    /// would leave a test asserting about a screen it never asked for. A test that means "the
    /// menu opens and shows these items" is a test about the menu, and needs the pointer.
    /// </para>
    /// </remarks>
    /// <param name="element">The element the verb arrived on; its page is searched.</param>
    /// <param name="automationId">The menu item's id.</param>
    /// <param name="outcome">Empty when the item was raised, <see cref="MenuItemDisabled"/> when it was declined.</param>
    /// <returns>The HRESULT to answer with.</returns>
    internal static int InvokeMenuItem(
        VisualElement element, string automationId, out string outcome)
    {
        outcome = string.Empty;

        if (string.IsNullOrWhiteSpace(automationId))
        {
            return HResults.E_INVALIDARG;
        }

        var item = MenuItems(element)
            .FirstOrDefault(candidate => candidate is Element named
                                         && named.AutomationId == automationId);

        // Not a refusal: this element does menu items perfectly well, and the one named was not
        // among them. It may belong to another page, or to a flyout the app has not built yet.
        if (item is not IMenuItemController controller)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        // Refused for an item a user could not have picked. A disabled entry reporting success
        // would let a test assert an action the app refuses to perform, which is exactly the
        // assertion a disabled entry exists to make false.
        //
        // Note that a MenuItem's IsEnabled follows its command: MAUI overwrites the markup from
        // CanExecute, so an entry disabled with IsEnabled="False" beside a runnable command is
        // enabled again by the time this reads it. Measured, in the sample.
        if (item is MenuItem { IsEnabled: false })
        {
            outcome = MenuItemDisabled;
            return HResults.S_OK;
        }

        controller.Activate();
        return HResults.S_OK;
    }

    /// <summary>
    /// Every menu entry reachable from this element's page, submenus included.
    /// </summary>
    /// <remarks>
    /// Two sources, because MAUI has two: the page's own <c>MenuBarItems</c>, which render into
    /// the window's menu bar, and the context flyouts attached to individual views, which render
    /// only once someone right-clicks. The second is why this walks the visual tree at all - an
    /// unopened context flyout exists in the app and nowhere else.
    /// </remarks>
    /// <param name="element">Any element on the page.</param>
    /// <returns>The menu entries.</returns>
    private static IEnumerable<IMenuElement> MenuItems(VisualElement element)
    {
        var page = PageOf(element);

        if (page is not null)
        {
            foreach (var bar in page.MenuBarItems)
            {
                foreach (var entry in Flatten(bar))
                {
                    yield return entry;
                }
            }
        }

        var root = (VisualElement?)page ?? element;

        foreach (var view in new[] { root }.Concat(Descendants(root)))
        {
            if (FlyoutBase.GetContextFlyout(view) is not MenuFlyout flyout)
            {
                continue;
            }

            foreach (var entry in Flatten(flyout))
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Raises a toolbar item by its <c>AutomationId</c>, answering only for the page on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The same entry point as <see cref="InvokeMenuItem"/>.</b> <c>ToolbarItem</c> derives from
    /// <c>MenuItem</c>, so <c>IMenuItemController.Activate</c> is what the platform's toolbar calls
    /// when the item is pressed: the same <c>Command</c> runs and the same <c>Clicked</c> fires.
    /// </para>
    /// <para>
    /// <b>Why the client needs this at all.</b> On Windows a <c>ToolbarItem</c>'s automation peer
    /// accepts the Invoke pattern, reports success and raises nothing - measured four ways - so the
    /// only other route was a real mouse click.
    /// </para>
    /// <para>
    /// <b>Only the page on screen answers, which is stricter than menu items and has to be.</b> The
    /// client takes the first target that answers, and a popped page keeps answering until its
    /// handler goes. Its toolbar items still hold their commands, and a command like "go back"
    /// acts on the <i>live</i> navigation stack - so a stale page raising its Back item would pop
    /// the page the test is on. The same rule <see cref="NavigateBack"/> applies, for the same
    /// reason.
    /// </para>
    /// <para>
    /// Items are looked for on the page and then on each page containing it, because a
    /// <c>NavigationPage</c>, <c>TabbedPage</c> or <c>FlyoutPage</c> can contribute toolbar items
    /// of its own to the bar the user sees.
    /// </para>
    /// </remarks>
    /// <param name="element">The element the verb arrived on; its page is searched.</param>
    /// <param name="automationId">The toolbar item's id.</param>
    /// <param name="outcome">Empty when raised, <see cref="MenuItemDisabled"/> when declined.</param>
    /// <returns>The HRESULT to answer with.</returns>
    internal static int InvokeToolbarItem(
        VisualElement element, string automationId, out string outcome)
    {
        outcome = string.Empty;

        if (string.IsNullOrWhiteSpace(automationId))
        {
            return HResults.E_INVALIDARG;
        }

        var page = PageOf(element);

        // UIA_E_ELEMENTNOTAVAILABLE, not a refusal: a page that is not on screen has nothing to
        // say about the toolbar the user sees. The client asks the next target.
        if (page is null || !IsOnScreen(page))
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        var item = PageAndContainers(page)
            .SelectMany(level => level.ToolbarItems)
            .FirstOrDefault(candidate => candidate.AutomationId == automationId);

        if (item is null)
        {
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        // Refused for an item a user could not press, as for menu items. IsEnabled follows the
        // command's CanExecute, so this is the app's own answer rather than the markup's.
        if (!item.IsEnabled)
        {
            outcome = MenuItemDisabled;
            return HResults.S_OK;
        }

        ((IMenuItemController)item).Activate();
        return HResults.S_OK;
    }

    /// <summary>
    /// Whether this page is the one the user is looking at.
    /// </summary>
    /// <remarks>
    /// On top of its navigation stack, and not covered by a modal. In a Shell app, Shell's own
    /// current page. A page in no stack at all is being torn down.
    /// </remarks>
    /// <param name="page">The page.</param>
    /// <returns>Whether it is on screen.</returns>
    private static bool IsOnScreen(Page page)
    {
        if (Shell.Current is { } shell)
        {
            return ReferenceEquals(shell.CurrentPage, page);
        }

        var navigation = page.Navigation;
        var stack = navigation?.NavigationStack;

        if (stack is not { Count: > 0 } || !ReferenceEquals(stack[^1], page))
        {
            return false;
        }

        var modals = navigation!.ModalStack;
        return modals.Count == 0 || ReferenceEquals(modals[^1], page);
    }

    /// <summary>The page, then each page that contains it, innermost first.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The pages whose toolbar items can appear together.</returns>
    private static IEnumerable<Page> PageAndContainers(Page page)
    {
        for (Element? walker = page; walker is not null; walker = walker.Parent)
        {
            if (walker is Page level)
            {
                yield return level;
            }
        }
    }

    /// <summary>Flattens a menu, including its submenus.</summary>
    /// <param name="entries">The menu's own entries.</param>
    /// <returns>Every entry beneath it.</returns>
    private static IEnumerable<IMenuElement> Flatten(IEnumerable<IMenuElement> entries)
    {
        foreach (var entry in entries)
        {
            yield return entry;

            if (entry is MenuFlyoutSubItem submenu)
            {
                foreach (var deeper in Flatten(submenu))
                {
                    yield return deeper;
                }
            }
        }
    }

    /// <summary>
    /// Opens or closes a Shell's flyout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One public property, replacing a hamburger button nothing can find.</b> Shell's flyout
    /// is opened by chrome MAUI gives no <c>AutomationId</c>, so a client reached it by name, by
    /// control type, or by clicking where it usually is - and each of those is a guess about a
    /// control the platform draws.
    /// </para>
    /// <para>
    /// Answered by any element in a Shell app: the flyout belongs to the Shell, not to the page
    /// the verb happened to arrive on, so <c>Shell.Current</c> is consulted when the element is
    /// not itself one. An app with no Shell has no flyout, and refuses rather than pretending.
    /// </para>
    /// </remarks>
    /// <param name="element">The element the verb arrived on.</param>
    /// <param name="presented">Whether the flyout should be showing.</param>
    /// <returns>The HRESULT to answer with.</returns>
    internal static int PresentFlyout(VisualElement element, bool presented)
    {
        if (ShellOf(element) is not { } shell)
        {
            return HResults.UIA_E_NOTSUPPORTED;
        }

        // S_FALSE for a flyout already in the state asked for: nothing happened, and saying so
        // lets a caller tell "it was already open" from "I opened it".
        if (shell.FlyoutIsPresented == presented)
        {
            return HResults.S_FALSE;
        }

        shell.FlyoutIsPresented = presented;
        return HResults.S_OK;
    }

    /// <summary>Whether a Shell's flyout is showing, or null in an app without one.</summary>
    /// <param name="element">The element the read arrived on.</param>
    /// <returns>The state, or null.</returns>
    internal static bool? IsFlyoutPresented(VisualElement element)
        => ShellOf(element)?.FlyoutIsPresented;

    /// <summary>The Shell this element belongs to, if the app has one.</summary>
    private static Shell? ShellOf(VisualElement element)
        => element as Shell ?? Shell.Current;

    /// <summary>The page this element sits on, walking up its parents.</summary>
    private static Page? PageOf(VisualElement element)
    {
        Element? walker = element;

        while (walker is not null and not Page)
        {
            walker = walker.Parent;
        }

        return walker as Page;
    }

    /// <summary>MAUI's name for the direction a swipe verb travels in.</summary>
    /// <param name="verb">The swipe verb.</param>
    /// <returns>The direction, or null if the verb is not a swipe.</returns>
    internal static SwipeDirection? SwipeDirectionFor(BruinetVerb verb) => verb switch
    {
        BruinetVerb.SwipeLeft => SwipeDirection.Left,
        BruinetVerb.SwipeRight => SwipeDirection.Right,
        BruinetVerb.SwipeUp => SwipeDirection.Up,
        BruinetVerb.SwipeDown => SwipeDirection.Down,
        _ => null,
    };


    // ---- Focus (step 13) ----------------------------------------------------------

    /// <summary>
    /// Gives the element logical focus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the verb the background-execution story rests on.</b> Keyboard focus and the
    /// desktop foreground window are two different things, and the framework had been
    /// conflating them: every physical action called <c>SetForeground</c> first, because global
    /// keystrokes go wherever the foreground is. <c>VisualElement.Focus</c> moves focus inside
    /// the app and touches no desktop-global state at all, so the machine stays with whoever is
    /// sitting at it.
    /// </para>
    /// <para>
    /// Returns MAUI's own answer rather than asserting success. A disabled or unfocusable
    /// element returns false, which travels back as a refusal and lets the client fall back.
    /// </para>
    /// </remarks>
    /// <param name="element">The element to focus.</param>
    /// <returns>Whether focus was taken.</returns>
    internal static bool TryFocus(VisualElement element) => element.Focus();

    /// <summary>Removes logical focus from the element.</summary>
    /// <param name="element">The element to unfocus.</param>
    internal static void Unfocus(VisualElement element) => element.Unfocus();

    // ---- Text (step 14) -----------------------------------------------------------

    /// <summary>
    /// Reads an element's editable text.
    /// </summary>
    /// <remarks>
    /// <see cref="InputView"/> rather than each of <c>Entry</c>, <c>Editor</c> and
    /// <c>SearchBar</c> in turn: it is their common MAUI base and the one that declares
    /// <c>Text</c>, so a control MAUI adds later is covered without an edit here. A
    /// <c>Label</c> has a <c>Text</c> too and is deliberately excluded - it is not input, and
    /// writing to one would let a test assert something no user could have done.
    /// </remarks>
    /// <param name="element">The element to read.</param>
    /// <param name="text">Its text, when this returns true.</param>
    /// <returns>Whether the element holds editable text.</returns>
    internal static bool TryGetText(VisualElement element, out string text)
    {
        if (element is InputView input)
        {
            text = input.Text ?? string.Empty;
            return true;
        }

        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Replaces an element's text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this replaces is worth naming.</b> The physical route for the same job is
    /// <c>TextInputMethod.Paste</c>, which writes the machine-wide clipboard and sends Ctrl+V -
    /// destroying whatever the person at the keyboard had copied, and corrupting a second run
    /// happening at the same time. This writes the property.
    /// </para>
    /// <para>
    /// <b>It is not a replacement for typing, and must not become one.</b> Setting <c>Text</c>
    /// raises <c>TextChanged</c> once for the whole value; a keyboard raises it per character,
    /// applies <c>MaxLength</c> as it goes, and lets a numeric keyboard refuse a letter. A test
    /// of input behaviour still types - that is what <c>TextInputMethod.Keys</c> is for.
    /// </para>
    /// </remarks>
    /// <param name="element">The element to write to.</param>
    /// <param name="text">The new text.</param>
    /// <returns>Whether the element accepted it.</returns>
    internal static bool TrySetText(VisualElement element, string text)
    {
        if (element is not InputView input)
        {
            return false;
        }

        // Refused here rather than left to the platform. WinUI enforces IsReadOnly on the
        // control it renders, but MAUI's own property setter does not - so a write through the
        // bridge would succeed on a field where every other route correctly fails, and a test
        // would pass against something a user cannot do.
        if (input is Entry { IsReadOnly: true } or Editor { IsReadOnly: true })
        {
            return false;
        }

        input.Text = text;
        return true;
    }

    /// <summary>
    /// Raises the element's completion action, as pressing Enter in it would.
    /// </summary>
    /// <remarks>
    /// The command, never the event. <c>Entry.SendCompleted</c> and its relatives are internal
    /// in MAUI 10, so an element whose completion handling is an event handler rather than a
    /// bound command cannot be reached from outside and this returns false - which travels back
    /// as a refusal and lets the client press a real Enter instead. That fallback is the honest
    /// outcome rather than a gap: the app has published no semantic route to its own behaviour.
    /// </remarks>
    /// <param name="element">The element to submit.</param>
    /// <returns>Whether a completion command was raised.</returns>
    internal static bool TrySubmit(VisualElement element) => element switch
    {
        Entry entry => Execute(entry.ReturnCommand, entry.ReturnCommandParameter),
        SearchBar search => Execute(search.SearchCommand, search.SearchCommandParameter),

        // Editor has no completion command in MAUI, and should not: Enter in a multi-line
        // field is a newline, so there is nothing to raise.
        _ => false,
    };

    // ---- Navigation ---------------------------------------------------------------

    /// <summary>
    /// Pops the element's navigation stack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pulled forward from step 22 because step 15 cannot be finished without it. Returning to
    /// the hub was the suite's entire physical-input footprint, and the two alternatives are a
    /// mouse click on a <c>ToolbarItem</c> - which is what this replaces, and which no
    /// automation pattern can drive, measured four ways - or a desktop-wide Alt+Left.
    /// </para>
    /// <para>
    /// <b>Not awaited.</b> A verb's contract is that the action has been delivered, not that the
    /// animation has finished; a test that needs the next page waits for that page. Awaiting
    /// here would also block the UI thread the pop itself has to run on. The continuation exists
    /// only so a failed pop is reported rather than lost in an unobserved task.
    /// </para>
    /// </remarks>
    /// <param name="element">Any element on the page to leave.</param>
    /// <returns>Whether there was something to pop.</returns>
    internal static int NavigateBack(VisualElement element)
    {
        if (element is not Page page)
        {
            // Only a page can pop itself. Nothing else declares this verb today, and an element
            // inside a page that did would be asking on the page's behalf without being able to
            // check the one thing that makes the answer safe.
            return HResults.UIA_E_NOTSUPPORTED;
        }

        var navigation = page.Navigation;
        var stack = navigation?.NavigationStack;

        BridgeDiagnostics.Report(
            $"NavigateBack asked of '{page.AutomationId}' [{page.GetType().Name}]: "
            + $"stack depth {stack?.Count ?? -1}, top is "
            + $"'{(stack is { Count: > 0 } ? stack[^1]?.GetType().Name : "none")}', "
            + $"modal depth {navigation?.ModalStack.Count ?? -1}");

        if (stack is null)
        {
            // UIA_E_ELEMENTNOTAVAILABLE: this page has no navigation at all, so it is detached -
            // popped long ago and still answering because its handler has not gone yet. Ask
            // somebody else.
            //
            // THIS LINE IS STEP 36. It used to share a branch with "we are at the root" and
            // return S_FALSE, which arrives at the client as S_OK (see HResults.S_FALSE). So a
            // detached page ended InvokeAnywhere's walk with a reported success, the live page
            // was never asked, nothing popped, and the fixture waited out its timeout for a hub
            // that was never coming - then blamed whichever test navigated next.
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        if (stack.Count <= 1)
        {
            // BRUINET_E_DECLINED: we are at the root and there is nothing to pop. A true
            // statement about the app, and one the caller must be able to hear - it was S_FALSE,
            // which does not survive the wire, so "go back" at the root reported success.
            //
            // A failure code for something that is not a fault, deliberately. IsAtNavigationRoot
            // exists so a caller can ask before commanding; one that commands anyway is asking
            // for something the app cannot do.
            return HResults.BRUINET_E_DECLINED;
        }

        // A page may only pop itself, and only while it is the one on top.
        //
        // Without this the verb was a menace. Every page that has been visited leaves a bridge
        // target behind - the registration is withdrawn on Unloaded, but a popped page keeps its
        // handler for a while and stays answerable - and a caller asking the app to go back
        // reaches whichever of them the walk finds first, which is the oldest. A stale page's
        // Navigation still reports the live stack, so it agreed to pop, returned success, and
        // popped nothing. The caller then waited out its timeout for a page change that was
        // never coming, three times over, and gave up on the hub still not being there.
        //
        // That one line cost five failures and doubled the suite's runtime, and it presented as
        // flakiness scattered across unrelated areas rather than as anything to do with
        // navigation.
        if (!ReferenceEquals(stack[^1], page))
        {
            // UIA_E_ELEMENTNOTAVAILABLE: this target is stale. Distinct from S_FALSE because the
            // caller should do something different - ask another target, not give up - and
            // because a stale page agreeing to pop is the defect described above.
            return HResults.UIA_E_ELEMENTNOTAVAILABLE;
        }

        // A pop already under way is not another pop to report.
        //
        // PopAsync is deliberately not awaited - see the remarks - so the stack still reads two
        // deep for a moment afterwards. Without this, a caller that asks twice in quick
        // succession is told "yes" twice: it pops a second page it never meant to, or, at the
        // root, hears that something was popped when nothing was. Both were observed.
        if (!PopsInFlight.Add(page))
        {
            // S_OK, and not a second pop. The caller asked for this page to go back and this page
            // is going back; saying so is true, and starting another pop is what the guard is
            // here to prevent.
            //
            // It returned S_FALSE, which the client read as S_OK anyway - so this is the same
            // behaviour, now stated rather than arrived at by a marshalling accident.
            BridgeDiagnostics.Report("NavigateBack: a pop is already in flight");
            return HResults.S_OK;
        }

        BridgeDiagnostics.Report("NavigateBack: popping");

        _ = navigation!.PopAsync().ContinueWith(
            popped =>
            {
                PopsInFlight.Remove(page);

                if (popped.IsFaulted)
                {
                    BridgeDiagnostics.Report($"PopAsync failed: {popped.Exception}");
                }
            },
            TaskScheduler.Default);

        return HResults.S_OK;
    }

    /// <summary>
    /// How deep the app's navigation stack is, answered only by a page that is in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This used to say that any target answers correctly, stale or not. That was measured to
    /// be false, and it was step 36.</b> A page part-way through being torn down still answers -
    /// its handler outlives its registration - and its <c>Navigation</c> reports a stack it is no
    /// longer part of. Caught in the bridge log with a page count of <c>0</c> returned while the
    /// app was two deep on another page:
    /// </para>
    /// <code>
    /// 14:03:21.943  withdraw 'PageHub' -> True                       (the next page was pushed)
    /// 14:03:21.967  GetState('NavigationDepth') on 'ContainerPage' -> '0'
    /// 14:03:22.023  withdraw 'ContainerPage' -> True                 (the stale page, finally)
    /// </code>
    /// <para>
    /// The caller asks any target and takes the first answer, so <c>0</c> won: the fixture
    /// concluded it was already at the root, did nothing, and waited out its timeout for a hub
    /// that was on nobody's screen - then failed whichever test had asked to navigate. Order
    /// dependent, and it landed on a different test every run.
    /// </para>
    /// <para>
    /// <b>So the same rule as <see cref="NavigateBack"/>, one notch looser.</b> That verb requires
    /// the page to be on top, because only the top page may pop itself. This one requires only
    /// that the page is <i>in</i> the stack, because every page in it can see the same depth - the
    /// hub answers correctly while a page is open above it, and should. A page in no stack knows
    /// nothing and says so.
    /// </para>
    /// </remarks>
    /// <param name="element">Any page that is part of the live navigation stack.</param>
    /// <returns>The depth, or null if this element cannot answer for the app.</returns>
    internal static int? NavigationDepth(VisualElement element)
    {
        if (element is not Page page)
        {
            return null;
        }

        var stack = page.Navigation?.NavigationStack;

        // Not in the stack it is reporting on, so it is being torn down and its answer is about
        // an app state that no longer includes it. Null reaches the client as
        // UIA_E_NOTSUPPORTED, which makes the walk ask the next target instead of believing this
        // one.
        return stack is not null && stack.Contains(page) ? stack.Count : null;
    }

    /// <summary>
    /// Where the app is, in whatever terms its navigation model uses.
    /// </summary>
    /// <remarks>
    /// Shell has a route and says so. A <c>NavigationPage</c> app has no such thing, so the
    /// honest answer is the identity of the page on top - which is what a test asserting "we are
    /// on the hub" actually means. Returning a fabricated route for the second case would make
    /// the two look alike when they are not.
    /// </remarks>
    /// <param name="element">Any element that can see the navigation stack.</param>
    /// <returns>
    /// The Shell route, or the top page's AutomationId, or null when this element cannot answer
    /// for the app - which the dispatcher turns into a refusal, so the caller asks another page.
    /// </returns>
    internal static string? CurrentRoute(VisualElement element)
    {
        if (Shell.Current is { } shell)
        {
            return shell.CurrentState?.Location?.ToString() ?? string.Empty;
        }

        if (element is not Page page)
        {
            return null;
        }

        var stack = page.Navigation?.NavigationStack;

        // The same rule as NavigationDepth, and for the same measured reason: a page being torn
        // down still answers, about a stack it has left. Null reaches the client as
        // UIA_E_NOTSUPPORTED, so the walk asks the next page instead of believing this one.
        if (stack is null || !stack.Contains(page))
        {
            return null;
        }

        var top = stack[^1];

        return top.AutomationId ?? top.GetType().Name;
    }

    /// <summary>
    /// Pages whose pop has been started and has not yet finished.
    /// </summary>
    /// <remarks>
    /// A conditional weak table rather than a set, so a page that is popped and dropped is not
    /// kept alive by the bookkeeping that tracked its own removal.
    /// </remarks>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, object>
        InFlight = new();

    private static class PopsInFlight
    {
        internal static bool Add(Page page)
        {
            lock (InFlight)
            {
                if (InFlight.TryGetValue(page, out _))
                {
                    return false;
                }

                InFlight.Add(page, Marker);
                return true;
            }
        }

        internal static void Remove(Page page)
        {
            lock (InFlight)
            {
                InFlight.Remove(page);
            }
        }

        private static readonly object Marker = new();
    }

    /// <summary>Runs a bound command if it will accept the call.</summary>
    /// <param name="command">The command, which is commonly null.</param>
    /// <param name="parameter">Its parameter.</param>
    /// <returns>Whether the command ran.</returns>
    private static bool Execute(System.Windows.Input.ICommand? command, object? parameter)
    {
        if (command is null || !command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }
}
