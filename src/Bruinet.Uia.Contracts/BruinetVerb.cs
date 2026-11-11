namespace Bruinet.Uia;

/// <summary>
/// Everything the bridge can be asked to do, as wire values.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only.</b> These numbers cross a process boundary between two independently
/// built assemblies, so a value that has ever shipped keeps its meaning forever. Add at the
/// end of a range; never renumber, never reuse a retired value.
/// </para>
/// <para>
/// <b>Ranges are the versioning story.</b> Each block of a hundred is one capability area,
/// which lets a provider answer "I implement gestures but not text" by range rather than by
/// enumerating verbs, and leaves room for a range to grow without colliding with the next.
/// </para>
/// <para>
/// Not every verb here is implemented yet. The enum is the whole planned vocabulary because
/// the numbering has to be decided once; <see cref="BruinetVerbRange"/> and the provider's
/// own capability set say what is actually live.
/// </para>
/// </remarks>
public enum BruinetVerb
{
    /// <summary>Not a verb. Rejected with <see cref="HResults.E_INVALIDARG"/>.</summary>
    None = 0,

    // ---- 1-99: meta ----------------------------------------------------------------

    /// <summary>Returns the supported verbs as a comma-separated list of numbers.</summary>
    GetCapabilities = 1,

    /// <summary>Returns <see cref="BruinetUiaIds.ProtocolVersion"/> as a decimal string.</summary>
    GetProtocolVersion = 2,

    /// <summary>Succeeds and does nothing. Proves the round trip without side effects.</summary>
    Ping = 3,

    /// <summary>
    /// Returns the target's screen rectangle as "left,top,width,height" in physical pixels.
    /// </summary>
    /// <remarks>
    /// Answered for every target, declared or not, so a client can tell apart targets that share
    /// an <c>AutomationId</c>.
    /// </remarks>
    GetBounds = 4,

    /// <summary>
    /// Returns what the target threw the last time a verb failed with <c>E_FAIL</c> ("Tap threw
    /// InvalidOperationException: …"), or an empty string when nothing has.
    /// </summary>
    /// <remarks>
    /// Answered for every target, declared or not. An app built before it refuses it as unknown, and the
    /// client then says only that the verb failed (review 05, finding 8).
    /// </remarks>
    GetLastFailure = 5,

    // ---- 100-199: gestures ---------------------------------------------------------

    /// <summary>Single tap.</summary>
    Tap = 100,

    /// <summary>Double tap.</summary>
    DoubleTap = 101,

    /// <summary>Press and hold.</summary>
    LongPress = 102,

    /// <summary>Swipe towards decreasing x.</summary>
    SwipeLeft = 103,

    /// <summary>Swipe towards increasing x.</summary>
    SwipeRight = 104,

    /// <summary>Swipe towards decreasing y.</summary>
    SwipeUp = 105,

    /// <summary>Swipe towards increasing y.</summary>
    SwipeDown = 106,

    /// <summary>Pan by <c>arg1</c> and <c>arg2</c> device-independent pixels.</summary>
    Pan = 107,

    /// <summary>Pinch by <c>arg1</c> percent, where 100 is no change.</summary>
    Pinch = 108,

    // ---- 200-299: focus ------------------------------------------------------------

    /// <summary>Gives the element logical focus without touching the foreground window.</summary>
    Focus = 200,

    /// <summary>Removes logical focus from the element.</summary>
    Unfocus = 201,

    /// <summary>Returns <c>true</c> or <c>false</c>.</summary>
    IsFocused = 202,

    // ---- 300-399: text -------------------------------------------------------------

    /// <summary>Replaces the element's text with the argument.</summary>
    SetText = 300,

    /// <summary>Appends the argument to the element's text.</summary>
    AppendText = 301,

    /// <summary>Empties the element's text.</summary>
    ClearText = 302,

    /// <summary>Raises the element's completion action, as the Enter key would.</summary>
    Submit = 303,

    /// <summary>Returns the element's text.</summary>
    GetText = 304,

    // ---- 400-499: scrolling --------------------------------------------------------

    /// <summary>Scrolls the named descendant into view.</summary>
    ScrollTo = 400,

    /// <summary>Scrolls the item at the given index into view.</summary>
    ScrollToIndex = 401,

    /// <summary>Returns the scroll offset and extent.</summary>
    ScrollPosition = 402,

    // ---- 500-599: navigation -------------------------------------------------------

    /// <summary>Pops the navigation stack.</summary>
    NavigateBack = 500,

    /// <summary>Navigates to the given route.</summary>
    NavigateTo = 501,

    /// <summary>Returns the current route.</summary>
    CurrentRoute = 502,

    // ---- 600-699: state reads ------------------------------------------------------

    /// <summary>Returns a named property of the element that a user could perceive.</summary>
    GetState = 600,

    /// <summary>Returns <c>true</c> once the dispatcher queue has drained.</summary>
    IsIdle = 601,

    // ---- 700-799: date and time ----------------------------------------------------

    /// <summary>Sets a date from a round-trip formatted string.</summary>
    SetDate = 700,

    /// <summary>Sets a time from a round-trip formatted string.</summary>
    SetTime = 701,

    /// <summary>Opens the element's flyout or picker.</summary>
    OpenFlyout = 702,

    /// <summary>Closes the element's flyout or picker.</summary>
    CloseFlyout = 703,

    // ---- 800-899: selection --------------------------------------------------------

    /// <summary>Selects the item at the given index.</summary>
    SelectIndex = 800,

    /// <summary>Selects the item with the given text.</summary>
    SelectByText = 801,

    // ---- 900-999: dialogs and menus ------------------------------------------------

    /// <summary>Returns the current alert's title, message and buttons.</summary>
    CurrentAlert = 900,

    /// <summary>Dismisses the current alert with the named result.</summary>
    DismissAlert = 901,

    /// <summary>
    /// Invokes a menu item by id without opening the popup.
    /// </summary>
    /// <remarks>
    /// The argument is the item's <c>AutomationId</c>. Answers with an empty string when the item
    /// was raised and <c>"disabled"</c> when it was found and declined - an outcome that cannot
    /// travel as <see cref="HResults.S_FALSE"/>, because UI Automation reports every success
    /// HRESULT from a custom pattern to the client as <see cref="HResults.S_OK"/>.
    /// </remarks>
    InvokeMenuItem = 902,

    /// <summary>
    /// Invokes a toolbar item, on the page on screen, by id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The argument is the item's <c>AutomationId</c>, and the answers are those of
    /// <see cref="InvokeMenuItem"/>: an empty string when raised, <c>"disabled"</c> when found and
    /// declined.
    /// </para>
    /// <para>
    /// <b>A verb of its own rather than a wider <see cref="InvokeMenuItem"/>.</b> A MAUI
    /// <c>ToolbarItem</c> is a <c>MenuItem</c> and is raised the same way, but an app that
    /// declared menu items agreed to have its menus reached, not its toolbar - and a client asking
    /// whether toolbar items are reachable needs an answer an older app cannot give by accident.
    /// </para>
    /// </remarks>
    InvokeToolbarItem = 903,
}

/// <summary>What a verb asks for: a reading, an act on the element, or an act on the app.</summary>
/// <remarks>
/// Not derivable from the number, unlike <see cref="BruinetVerbRange"/> - the ranges group verbs
/// by subject area, and this cuts across them. <c>NavigateBack</c> and <c>ScrollTo</c> are both
/// actions in different ranges; <c>CurrentRoute</c> and <c>ScrollPosition</c> are both reads in
/// those same two.
/// </remarks>
public enum BruinetVerbKind
{
    /// <summary>Answers a question and changes nothing.</summary>
    Read = 0,

    /// <summary>
    /// Something a person does to the element the verb is declared on.
    /// </summary>
    /// <remarks>
    /// The only kind the accessibility audit examines, because it is the only kind where "can a
    /// keyboard reach this element" is a question with a meaning.
    /// </remarks>
    ElementAction = 1,

    /// <summary>
    /// Something a person does to the app, posted through whichever element carries the verb.
    /// </summary>
    /// <remarks>
    /// Going back, invoking a menu item, dismissing an alert. The element is an address: a page
    /// declares <c>NavigateBack</c> because a page is somewhere to send the request, not because
    /// the page is what the user presses.
    /// </remarks>
    AppAction = 2,
}

/// <summary>The capability area a verb belongs to, derived from its numeric range.</summary>
public enum BruinetVerbRange
{
    /// <summary>Not a recognised range.</summary>
    Unknown = 0,

    /// <summary>Verbs 1-99: discovery and liveness.</summary>
    Meta = 1,

    /// <summary>Verbs 100-199.</summary>
    Gestures = 100,

    /// <summary>Verbs 200-299.</summary>
    Focus = 200,

    /// <summary>Verbs 300-399.</summary>
    Text = 300,

    /// <summary>Verbs 400-499.</summary>
    Scrolling = 400,

    /// <summary>Verbs 500-599.</summary>
    Navigation = 500,

    /// <summary>Verbs 600-699.</summary>
    State = 600,

    /// <summary>Verbs 700-799.</summary>
    DateAndTime = 700,

    /// <summary>Verbs 800-899.</summary>
    Selection = 800,

    /// <summary>Verbs 900-999.</summary>
    DialogsAndMenus = 900,
}

/// <summary>Which of the contract's two methods a verb travels on.</summary>
/// <remarks>
/// <para>
/// <b>Part of the contract, and as fixed as the numbers.</b> The method table has exactly two
/// entries and a verb uses one of them; a client that sends a verb down the other reaches a
/// provider that has never heard of it, and is refused for a reason that has nothing to do with
/// the element. That failure is indistinguishable from "this element does not support that",
/// which is what makes it worth writing down rather than remembering.
/// </para>
/// <para>
/// The values match the method indexes in the frozen table.
/// </para>
/// </remarks>
public enum BruinetVerbTransport
{
    /// <summary><c>Invoke(verb, arg1, arg2)</c> - three ints in, nothing out.</summary>
    Invoke = 0,

    /// <summary><c>Exchange(verb, argument, out result)</c> - a string in, a string out.</summary>
    Exchange = 1,
}

/// <summary>Helpers over <see cref="BruinetVerb"/> that both ends of the wire rely on.</summary>
public static class BruinetVerbs
{
    /// <summary>
    /// Which of the two contract methods a verb travels on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule is what it looks like:</b> a verb goes on <c>Exchange</c> if it carries a
    /// string in either direction, and on <c>Invoke</c> otherwise. It is written out verb by
    /// verb anyway, because the rule is a description of the answers rather than a way of
    /// deriving them - <c>Submit</c> and <c>ClearText</c> carry no strings and still belong with
    /// the rest of the text range, since the provider answers a range on one path.
    /// </para>
    /// <para>
    /// <b>Append-only, like the numbers.</b> A verb that has shipped keeps its transport: moving
    /// one would silently break every client built before the move, with no version to check.
    /// Verbs this build does not implement yet are listed all the same, for the same reason the
    /// numbers are - deciding once is what stops the decision from being made twice differently.
    /// </para>
    /// </remarks>
    /// <param name="verb">The verb.</param>
    /// <returns>The method it is sent on.</returns>
    public static BruinetVerbTransport TransportFor(BruinetVerb verb) => verb switch
    {
        // Meta. Ping is answered on both, and is listed here so that asking about it gives one
        // answer rather than an arbitrary one.
        BruinetVerb.GetCapabilities => BruinetVerbTransport.Exchange,
        BruinetVerb.GetProtocolVersion => BruinetVerbTransport.Exchange,
        BruinetVerb.GetBounds => BruinetVerbTransport.Exchange,
        BruinetVerb.GetLastFailure => BruinetVerbTransport.Exchange,
        BruinetVerb.Ping => BruinetVerbTransport.Invoke,

        // Focus. IsFocused answers with a string, the other two do not.
        BruinetVerb.IsFocused => BruinetVerbTransport.Exchange,

        // Text, entire. Including the two that carry nothing.
        BruinetVerb.SetText => BruinetVerbTransport.Exchange,
        BruinetVerb.AppendText => BruinetVerbTransport.Exchange,
        BruinetVerb.ClearText => BruinetVerbTransport.Exchange,
        BruinetVerb.Submit => BruinetVerbTransport.Exchange,
        BruinetVerb.GetText => BruinetVerbTransport.Exchange,

        // Scrolling: to a named descendant, or reporting a position.
        BruinetVerb.ScrollTo => BruinetVerbTransport.Exchange,
        BruinetVerb.ScrollPosition => BruinetVerbTransport.Exchange,

        // Navigation by route.
        BruinetVerb.NavigateTo => BruinetVerbTransport.Exchange,
        BruinetVerb.CurrentRoute => BruinetVerbTransport.Exchange,

        // State reads, both of which answer with a string.
        BruinetVerb.GetState => BruinetVerbTransport.Exchange,
        BruinetVerb.IsIdle => BruinetVerbTransport.Exchange,

        // Dates and times travel as round-trip formatted strings.
        BruinetVerb.SetDate => BruinetVerbTransport.Exchange,
        BruinetVerb.SetTime => BruinetVerbTransport.Exchange,

        BruinetVerb.SelectByText => BruinetVerbTransport.Exchange,

        BruinetVerb.CurrentAlert => BruinetVerbTransport.Exchange,
        BruinetVerb.DismissAlert => BruinetVerbTransport.Exchange,
        BruinetVerb.InvokeMenuItem => BruinetVerbTransport.Exchange,
        BruinetVerb.InvokeToolbarItem => BruinetVerbTransport.Exchange,

        // Everything else is ints or nothing: the gestures, Focus and Unfocus, the flyouts,
        // going back, scrolling to an index, selecting by index.
        _ => BruinetVerbTransport.Invoke,
    };

    /// <summary>
    /// What kind of thing a verb is: a read, something a person does to the element, or
    /// something a person does to the app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For the accessibility audit, and it is the line the audit turns on.</b> An element that
    /// can only be swiped is an accessibility defect; an element a test reads the state of is not;
    /// and a page carrying <c>NavigateBack</c> is neither, because the page is an address rather
    /// than the thing being acted on.
    /// </para>
    /// <para>
    /// <b>The third case was measured, not anticipated.</b> The first audit reported eight of the
    /// sample app's fifteen "defects" as pages with no keyboard route - true, meaningless, and
    /// more than half the list. A backlog that is mostly noise is one nobody reads, so the
    /// distinction that removes it belongs in the vocabulary rather than in a filter somewhere
    /// downstream.
    /// </para>
    /// <para>
    /// <b><c>OpenFlyout</c> and <c>CloseFlyout</c> are app-level here, though they are genuinely
    /// ambiguous.</b> On a <c>Picker</c> the subject is the picker; on a <c>Shell</c> it is the
    /// app's own chrome, and asking whether a keyboard can focus the <c>Shell</c> answers nothing.
    /// Calling them app-level gets both right in practice: a picker is audited through the
    /// selection verbs it also declares, and a shell drops out. An element declaring nothing but
    /// a flyout verb would escape the audit, which is the known cost.
    /// </para>
    /// <para>
    /// <b>Reads and app actions are listed; everything else acts on its element.</b> That way
    /// round on purpose - a verb added without a thought here shows up in the audit as a possible
    /// defect, which someone will query. The other default would drop it silently out of an
    /// accessibility report, which is the failure nobody notices.
    /// </para>
    /// <para>
    /// <b><c>Focus</c> acts on the element</b>, though it looks like plumbing. An element
    /// declaring <c>Focus</c> that no keyboard can reach is precisely the finding worth having.
    /// </para>
    /// <para>
    /// Every verb is pinned by name in <c>ContractTests</c>, so adding one to the enum without
    /// deciding this fails a test rather than quietly taking the default.
    /// </para>
    /// </remarks>
    /// <param name="verb">The verb.</param>
    /// <returns>What kind of verb it is.</returns>
    public static BruinetVerbKind KindOf(BruinetVerb verb) => verb switch
    {
        // Not a verb.
        BruinetVerb.None => BruinetVerbKind.Read,

        // Meta: discovery and liveness, asked of the bridge rather than of the app.
        BruinetVerb.GetCapabilities => BruinetVerbKind.Read,
        BruinetVerb.GetProtocolVersion => BruinetVerbKind.Read,
        BruinetVerb.GetBounds => BruinetVerbKind.Read,
        BruinetVerb.GetLastFailure => BruinetVerbKind.Read,
        BruinetVerb.Ping => BruinetVerbKind.Read,

        // Reads. Each names a question about current state; none of them changes it.
        BruinetVerb.IsFocused => BruinetVerbKind.Read,
        BruinetVerb.GetText => BruinetVerbKind.Read,
        BruinetVerb.ScrollPosition => BruinetVerbKind.Read,
        BruinetVerb.CurrentRoute => BruinetVerbKind.Read,
        BruinetVerb.GetState => BruinetVerbKind.Read,
        BruinetVerb.IsIdle => BruinetVerbKind.Read,
        BruinetVerb.CurrentAlert => BruinetVerbKind.Read,

        // App actions. Real things a person does, but not to the element carrying the verb -
        // that element is where the request is posted, not what it acts on.
        BruinetVerb.NavigateBack => BruinetVerbKind.AppAction,
        BruinetVerb.NavigateTo => BruinetVerbKind.AppAction,
        BruinetVerb.InvokeMenuItem => BruinetVerbKind.AppAction,
        BruinetVerb.InvokeToolbarItem => BruinetVerbKind.AppAction,
        BruinetVerb.DismissAlert => BruinetVerbKind.AppAction,
        BruinetVerb.OpenFlyout => BruinetVerbKind.AppAction,
        BruinetVerb.CloseFlyout => BruinetVerbKind.AppAction,

        // Everything else acts on the element it is declared on: the gestures, focusing and
        // unfocusing, editing text, scrolling, setting a date or a time, selecting.
        _ => BruinetVerbKind.ElementAction,
    };

    /// <summary>The capability area <paramref name="verb"/> falls in.</summary>
    /// <remarks>
    /// Works on unknown values on purpose. A client from a newer build can ask an older
    /// provider "do you do text at all" and get a useful answer about a verb the provider has
    /// never heard of.
    /// </remarks>
    /// <param name="verb">Any verb value, known or not.</param>
    /// <returns>The range, or <see cref="BruinetVerbRange.Unknown"/> outside 1-999.</returns>
    public static BruinetVerbRange RangeOf(int verb) => verb switch
    {
        >= 1 and < 100 => BruinetVerbRange.Meta,
        >= 100 and < 200 => BruinetVerbRange.Gestures,
        >= 200 and < 300 => BruinetVerbRange.Focus,
        >= 300 and < 400 => BruinetVerbRange.Text,
        >= 400 and < 500 => BruinetVerbRange.Scrolling,
        >= 500 and < 600 => BruinetVerbRange.Navigation,
        >= 600 and < 700 => BruinetVerbRange.State,
        >= 700 and < 800 => BruinetVerbRange.DateAndTime,
        >= 800 and < 900 => BruinetVerbRange.Selection,
        >= 900 and < 1000 => BruinetVerbRange.DialogsAndMenus,
        _ => BruinetVerbRange.Unknown,
    };

    /// <summary>The wire form of a capability set: verb numbers, comma separated, ascending.</summary>
    /// <param name="verbs">The verbs the provider implements for one element.</param>
    /// <returns>A string suitable for returning from <see cref="BruinetVerb.GetCapabilities"/>.</returns>
    public static string FormatCapabilities(IEnumerable<BruinetVerb> verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);

        return string.Join(
            ",",
            verbs.Select(v => (int)v).Distinct().Order());
    }

    /// <summary>Reads what <see cref="FormatCapabilities"/> wrote.</summary>
    /// <remarks>
    /// Unparseable and unknown entries are dropped rather than throwing. The list comes from
    /// another process which may be newer than this one, so an entry this build cannot name is
    /// expected traffic, not corruption.
    /// </remarks>
    /// <param name="capabilities">The string returned by the provider.</param>
    /// <returns>The verbs, ascending.</returns>
    public static IReadOnlyList<BruinetVerb> ParseCapabilities(string? capabilities)
    {
        if (string.IsNullOrWhiteSpace(capabilities))
        {
            return [];
        }

        var parsed = new List<BruinetVerb>();

        foreach (var part in capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries
                                                     | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var value) && Enum.IsDefined((BruinetVerb)value))
            {
                parsed.Add((BruinetVerb)value);
            }
        }

        parsed.Sort();
        return parsed;
    }
}
