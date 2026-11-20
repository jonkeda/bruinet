using System.Diagnostics;
using System.Text.RegularExpressions;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Places;

/// <summary>How one walk goes, besides the project's settings.</summary>
public sealed record WalkOptions
{
    /// <summary>The account walked as, as the list names it.</summary>
    public string Account { get; init; } = "";

    /// <summary>The longest the walk takes.</summary>
    public int MaxMinutes { get; init; } = 20;

    /// <summary>The most pages it opens; null for no limit.</summary>
    public int? MaxPlaces { get; init; }

    /// <summary>The most presses on one page, the presses in what a press opened counted in.</summary>
    public int MaxPresses { get; init; } = 25;

    /// <summary>How deep it presses into what a press opened: 2 is a dialog's own buttons, and no further.</summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>Only pages whose key starts with this; null for all.</summary>
    public string? Only { get; init; }

    /// <summary>Opens pages that were seen before too.</summary>
    public bool Again { get; init; }

    /// <summary>
    /// The addresses of records the walk owns (a test made them, their names carry the run marker): on a page whose
    /// address starts with one, a press's writes go through. Anywhere else they are held back.
    /// </summary>
    public IReadOnlyList<string> Own { get; init; } = [];

    /// <summary>Holds every write back, on an owned record too: an account that may not write.</summary>
    public bool HoldAll { get; init; }

    /// <summary>Follows links and tabs only: nothing else is pressed.</summary>
    public bool LinksAndTabs { get; init; }

    /// <summary>Today, as the list writes a day.</summary>
    public string Today { get; init; } = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Told what the walk is doing.</summary>
    public IProgress<string>? Progress { get; init; }
}

/// <summary>Why a walk stopped.</summary>
public enum WalkStop
{
    /// <summary>The frontier is empty: every known page with an address was opened.</summary>
    Done,

    /// <summary>The time was used.</summary>
    MaxMinutes,

    /// <summary>The number of pages was used.</summary>
    MaxPlaces,

    /// <summary>A sign-in page came.</summary>
    SignIn,

    /// <summary>A person stopped it.</summary>
    Stopped,
}

/// <summary>What a walk did.</summary>
/// <param name="Stop">Why it stopped.</param>
/// <param name="Opened">The pages it opened.</param>
/// <param name="Found">The places it added to the list.</param>
/// <param name="Presses">The presses it made.</param>
/// <param name="Held">The writes it held back.</param>
/// <param name="Left">The pages still on the frontier.</param>
/// <param name="Notes">What went wrong on the way.</param>
public sealed record WalkResult(WalkStop Stop, int Opened, int Found, int Presses, int Held, int Left, IReadOnlyList<string> Notes);

/// <summary>
/// Walks an app for its places: opens each page on the list's frontier, reads it, and presses everything a person can
/// press, each once, to see what it leads to: another page, a dialog, a menu, a tab, or a call to the back end. How
/// much it presses follows the project's <see cref="AppEnvironment"/>; a write lands only on a record the walk owns
/// and is held back anywhere else (<see cref="RequestGuard.HoldWrites"/>). It fills in no form and saves nothing.
/// </summary>
public sealed partial class Walker(ScrapeProject project, IExploreSource source, IPageCalls calls, RequestGuard guard, WalkOptions options, Func<CancellationToken, Task<ScrapeSnapshot>> capture, Action<PlacesList> save)
{
    private static readonly string[] SaveWords = ["save", "submit", "apply", "ok", "opslaan", "toepassen", "bewaren", "speichern", "ubernehmen", "enregistrer", "valider", "create", "aanmaken", "add", "toevoegen", "erstellen", "hinzufugen", "creer", "ajouter"];
    /// <summary>True when a button's label says it saves a form: the walk does not press it, and a save test does.</summary>
    public static bool Saves(string label) => SaveWords.Any(word => DangerWords.Contains(label, word));

    private static readonly string[] DeleteWords = DangerWords.ByCategory["delete"].Concat(DangerWords.ByCategory["approve"]).ToArray();
    private static readonly TimeSpan PageLimit = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Keeps a place as it was read (the capture, and for a part of a page what opened it) and says where: the
    /// place's <see cref="Place.Snapshot"/>. Null keeps nothing.
    /// </summary>
    public Func<ScrapeSnapshot, string?, string?>? Keep { get; init; }

    private readonly ExploreSettings _settings = project.Config.Explore;
    private readonly bool _careful = project.Config.Environment == AppEnvironment.Production;
    private readonly Stopwatch _clock = new();
    private readonly List<string> _notes = [];
    private readonly List<string> _downloads = [];
    private readonly HashSet<string> _tried = new(StringComparer.Ordinal);
    private readonly HashSet<string> _notOpened = new(StringComparer.Ordinal);
    private readonly HashSet<string> _triedAgain = new(StringComparer.Ordinal);
    private int _nativeDialogs;
    private int _opened;
    private int _found;
    private int _presses;
    private int _held;
    private bool _chromeDone;
    private bool _signIn;

    /// <summary>Walks the list's frontier; the list is saved after every page.</summary>
    public async Task<WalkResult> RunAsync(PlacesList list, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        _clock.Start();
        // What is the same on every page was pressed when an earlier walk read a page.
        _chromeDone = list.Places.Any(place => place.Kind == PlaceKind.Page && place.State != PlaceState.Known && place.Sources.Contains(PlacesSeeds.Walk));
        source.Downloaded += OnDownloaded;
        source.DialogDismissed += OnDialog;
        guard.HoldWrites = true;
        var stop = WalkStop.Done;
        try
        {
            // The records the walk owns are opened at their own addresses, whatever the list holds of their pages:
            // a page that was seen on someone else's record is walked again on the walk's own, with its writes let through.
            foreach (var own in options.HoldAll ? [] : options.Own)
            {
                var (key, path) = PlacesList.KeyAndPath(own);
                _tried.Add(key);
                await PageAsync(list, (list[key] ?? PlacesList.Known(own, PlacesSeeds.Run)) with { Address = path }, cancellationToken).ConfigureAwait(false);
                save(list);
            }

            while (Next(list) is { } place)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_clock.Elapsed >= TimeSpan.FromMinutes(options.MaxMinutes))
                { stop = WalkStop.MaxMinutes; break; }
                if (options.MaxPlaces is { } most && _opened >= most)
                { stop = WalkStop.MaxPlaces; break; }

                _tried.Add(place.Key);
                await PageAsync(list, place, cancellationToken).ConfigureAwait(false);
                save(list);
                if (_signIn)
                { stop = WalkStop.SignIn; break; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stop = WalkStop.Stopped;
        }
        finally
        {
            source.Downloaded -= OnDownloaded;
            source.DialogDismissed -= OnDialog;
            save(list);
        }

        return new WalkResult(stop, _opened, _found, _presses, _held, list.Frontier.Count(Wanted), _notes);
    }

    // The next page to open; when none is left, a page that did not open comes once more: the environment may answer now.
    private Place? Next(PlacesList list)
    {
        var next = list.Places.FirstOrDefault(place => !_tried.Contains(place.Key) && Wanted(place) && (place.OnFrontier || ToOpenAs(place)));
        return next ?? list.Places.FirstOrDefault(place => _notOpened.Contains(place.Key) && _triedAgain.Add(place.Key) && place.OnFrontier);
    }

    // A page another account opened, or was refused, is this account's to open too: what an account sees follows its
    // rights and its organization. Asked for again, every page that opens is.
    private bool ToOpenAs(Place place)
    {
        if (place.Kind != PlaceKind.Page || place.Address is null || place.State == PlaceState.Known)
            return false;
        var mine = place.Accounts.Contains(options.Account, StringComparer.Ordinal) || place.RefusedTo.Contains(options.Account, StringComparer.Ordinal);
        if (place.State == PlaceState.Refused)
            return !mine && place.RefusedTo.Count > 0;
        return options.Again || !mine;
    }

    // The address is a record the walk owns: that record's address itself, or a page under it. Never another record
    // whose address only starts the same (/department/94 is not /department/940).
    private bool IsOwn(string address)
    {
        var path = PlacesList.KeyAndPath(address).Path;
        return options.Own.Select(own => PlacesList.KeyAndPath(own).Path).Any(own =>
            path.Equals(own, StringComparison.OrdinalIgnoreCase)
            || (path.StartsWith(own, StringComparison.OrdinalIgnoreCase) && path[own.Length] is '/' or '?' or '#' or '&'));
    }

    private bool Wanted(Place place) => options.Only is not { Length: > 0 } only || place.Key.StartsWith(only, StringComparison.OrdinalIgnoreCase);

    // Opens one page, reads it, and presses what is on it.
    private async Task PageAsync(PlacesList list, Place place, CancellationToken cancellationToken)
    {
        var address = Address(place.Address!);
        Say($"opening {place.Key}");
        var snapshot = await OpenAsync(address, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            var tries = place.Tries + 1;
            if ((source as Html.PlaywrightScrapeSource)?.LastOpenProblem?.Contains("Download is starting", StringComparison.Ordinal) == true)
            {
                list.Set(place with { State = PlaceState.Refused, Note = "a download, not a page" });
                return;
            }

            list.Set(place with { Tries = tries, State = tries >= 3 ? PlaceState.Refused : place.State, Note = tries >= 3 ? "did not open, three times" : place.Note });
            _notOpened.Add(place.Key);
            _notes.Add($"{place.Key} did not open (try {tries}){((source as Html.PlaywrightScrapeSource)?.LastOpenProblem is { } why ? ": " + why : "")}.");
            return;
        }

        _opened++;
        var seen = new Place { Key = place.Key, Kind = PlaceKind.Page, Accounts = [options.Account], Sources = [PlacesSeeds.Walk], Seen = options.Today };
        if (IsSignIn(snapshot))
        {
            _signIn = true;
            _opened--;
            return;
        }

        if (!project.IsOwnPage(snapshot.Location))
        {
            list.Add(seen with { State = PlaceState.Refused, Accounts = [], RefusedTo = [options.Account], Note = $"leaves the app for {new Uri(snapshot.Location).Authority}" });
            return;
        }

        var landed = PlacesList.KeyAndPath(snapshot.Location).Key;
        if (landed != place.Key)
        {
            // The address leads elsewhere: that page is a place of its own, opened in its turn.
            list.Add(seen with { State = PlaceState.Seen, Note = $"lands on {landed}" });
            Found(list, PlacesSeeds.Opened(snapshot.Location, PlacesSeeds.Walk, $"where {place.Key} lands"));
            return;
        }

        var facts = PageFacts.Of(snapshot, project.ControlSigns, project.PlaceSigns);
        if (facts.Headings.FirstOrDefault(heading => Refusal().IsMatch(heading)) is { } refusal)
        {
            list.Add(seen with { State = PlaceState.Refused, Accounts = [], RefusedTo = [options.Account], Note = $"the app answered: {Place.Clean(refusal)}" });
            return;
        }

        var page = Read(seen, facts, place.Key.Contains("{id}", StringComparison.Ordinal), isDialog: false) with { Snapshot = Keep?.Invoke(snapshot, null) };
        var budget = new Budget(options.MaxPresses, Stopwatch.StartNew());
        var notPressed = new List<string>();
        await PressAllAsync(list, page, address, [], snapshot, 0, new HashSet<string>(StringComparer.Ordinal), budget, notPressed, inForm: page.State == PlaceState.FormNotSaved, cancellationToken).ConfigureAwait(false);
        list.Add(page with { NotPressed = [.. notPressed.Distinct(StringComparer.Ordinal).Take(12)], Note = budget.Left <= 0 ? $"more than {options.MaxPresses} to press: not all were" : null });
        _chromeDone = true;
    }

    // Presses each candidate of a state once, and what each opened in its turn.
    private async Task PressAllAsync(PlacesList list, Place parent, string address, IReadOnlyList<NodeRef> prefix, ScrapeSnapshot snapshot, int depth, IReadOnlySet<string> known, Budget budget, List<string> notPressed, bool inForm, CancellationToken cancellationToken)
    {
        var pageKey = PlacesList.KeyAndPath(address).Key;
        var candidates = await CandidatesAsync(list, pageKey, snapshot, known, notPressed, inForm, cancellationToken).ConfigureAwait(false);
        var hold = options.HoldAll || !IsOwn(address);
        var dirty = false;
        var dull = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.Left <= 0 || budget.Clock.Elapsed > PageLimit || _signIn)
                return;
            if (dirty)
            {
                if (await ReachAsync(address, prefix, cancellationToken).ConfigureAwait(false) is not { } again)
                    return;
                snapshot = again;
                dirty = false;
            }

            _ = calls.TakeCalls();
            var blockedBefore = guard.Blocked.Count;
            guard.HoldWrites = hold;
            if (await PressAsync(candidate, snapshot, address, prefix, cancellationToken).ConfigureAwait(false) is not { } pressed)
            {
                guard.HoldWrites = true;
                dirty = true;
                continue;
            }

            budget.Left--;
            _presses++;
            snapshot = pressed.Snapshot;
            var node = pressed.Node;
            var before = pressed.Before;

            await PauseAsync(cancellationToken).ConfigureAwait(false);
            var after = await MovingPage.ReadAsync(capture, PauseAsync, cancellationToken).ConfigureAwait(false);
            guard.HoldWrites = true;
            var made = calls.TakeCalls();
            var held = guard.Blocked.Skip(blockedBefore).Where(blocked => blocked.Reason.StartsWith(RequestGuard.HeldReason, StringComparison.Ordinal)).ToList();
            _held += held.Count;
            if (after is null)
            { dirty = true; continue; }
            if (IsSignIn(after))
            { _signIn = true; return; }

            var via = $"'{candidate.Label}' on {parent.Key}";
            var writes = CallTexts(made, held, all: false);
            if (LeftThePage(list, parent, pageKey, candidate, after, via, writes, notPressed))
            {
                dirty = true;
                continue;
            }

            var afterRefs = NodeRef.Of(after);
            var fresh = afterRefs.Values.Where(r => after[r.Number].Bounds is not null && !before.Contains(r.Key)).Select(r => after[r.Number]).ToList();
            var facts = PageFacts.Of(after, project.ControlSigns, project.PlaceSigns);
            var kind = Kind(snapshot, after, candidate, facts, fresh, made.Count + held.Count);
            // In a menu, a line that only changes the page is a setting (a list's column chooser): it is no
            // place, and after two in a row the rest of the lines are taken to be the same.
            if (depth > 0 && kind is null or PlaceKind.Part)
            {
                // In a dialog it is a button like any other that showed nothing new; in a menu, a line of settings.
                if (parent.Kind != PlaceKind.Menu)
                {
                    dirty = true;
                    continue;
                }

                if (++dull >= 2)
                {
                    notPressed.Add("the rest of its lines (two in a row only changed the page)");
                    return;
                }

                dirty = true;
                continue;
            }

            dull = 0;
            if (kind is null)
            {
                dirty = held.Count > 0;
                continue;
            }

            var child = new Place { Key = Place.PartKey(parent.Key, candidate.Label), Kind = kind.Value, Parent = pageKey, Via = via, Opener = PlaceOpener.KindOf(candidate.Label, candidate.IsTab, candidate.InRow), Accounts = [options.Account], Sources = [PlacesSeeds.Walk], Seen = options.Today };
            if (kind == PlaceKind.Action)
            {
                Found(list, child with { State = PlaceState.Seen, Calls = CallTexts(made, held, all: true) });
                dirty = held.Count > 0;
                continue;
            }

            // A dialog, a menu and a tab are read as what the page shows then; a part is only known to be there.
            child = kind switch
            {
                PlaceKind.Part => child with { State = PlaceState.Seen, Calls = writes },
                // A menu is its lines: what the page under it shows is the page's.
                PlaceKind.Menu => child with { State = PlaceState.Seen, Calls = writes, Buttons = [.. facts.Options.Select(Place.Clean).Where(line => line.Length > 0 && line.Split(' ').Length <= 8).Distinct(StringComparer.Ordinal).Take(40)] },
                _ => Read(child, facts, ofRecord: true, isDialog: kind is PlaceKind.Dialog) with { Calls = writes },
            };
            child = Kept(list, child, after, candidate.Label);
            var childNotPressed = new List<string>();
            if (depth + 1 < options.MaxDepth && !options.LinksAndTabs)
                await PressAllAsync(list, child, address, [.. prefix, node with { Label = candidate.Label }], after, depth + 1, before, budget, childNotPressed, inForm: child.State == PlaceState.FormNotSaved, cancellationToken).ConfigureAwait(false);
            Found(list, child with { NotPressed = [.. childNotPressed.Distinct(StringComparer.Ordinal).Take(12)] });
            // A tab leaves the page as a person would go on from it; a dialog or a menu covers it.
            dirty = kind != PlaceKind.Tab || depth + 1 < options.MaxDepth || held.Count > 0;
            if (!dirty)
                snapshot = after;
        }
    }

    // Presses a candidate in a state; when it is covered or moved by what an earlier press showed, once more on the page as it opens.
    private async Task<(ScrapeSnapshot Snapshot, NodeRef Node, HashSet<string> Before)?> PressAsync(Candidate candidate, ScrapeSnapshot snapshot, string address, IReadOnlyList<NodeRef> prefix, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var refs = NodeRef.Of(snapshot);
            var node = refs.Values.FirstOrDefault(r => r.Key == candidate.Key);
            if (node is null)
                return null;
            var before = Visible(snapshot, refs);
            if (await source.ClickAsync(node.Number, cancellationToken).ConfigureAwait(false) is null)
                return (snapshot, node, before);
            if (attempt == 1 || await ReachAsync(address, prefix, cancellationToken).ConfigureAwait(false) is not { } again)
                return null;
            snapshot = again;
        }

        return null;
    }

    // What a press did that left the page as it was read: a download, another site, another page. True when it did.
    private bool LeftThePage(PlacesList list, Place parent, string pageKey, Candidate candidate, ScrapeSnapshot after, string via, string[] writes, List<string> notPressed)
    {
        if (_downloads.Count > 0)
        {
            _downloads.Clear();
            Found(list, new Place { Key = Place.PartKey(parent.Key, candidate.Label), Kind = PlaceKind.Action, State = PlaceState.Seen, Parent = pageKey, Via = via, Calls = writes, Note = "starts a download", Accounts = [options.Account], Sources = [PlacesSeeds.Walk], Seen = options.Today });
            return true;
        }

        if (!project.IsOwnPage(after.Location))
        {
            notPressed.Add($"{candidate.Label} (leaves the app)");
            return true;
        }

        if (PlacesList.KeyAndPath(after.Location).Key == pageKey)
            return false;
        Found(list, PlacesSeeds.Opened(after.Location, PlacesSeeds.Walk, via) with { Calls = writes });
        return true;
    }

    // What a press led to, by what the page shows after it that it did not before; null when nothing.
    private PlaceKind? Kind(ScrapeSnapshot before, ScrapeSnapshot after, Candidate candidate, PageFacts facts, List<ScrapeNode> fresh, int callCount)
    {
        if (facts.Dialog is not null && fresh.Any(project.PlaceSigns.IsDialog) || (facts.Dialog is not null && PageFacts.Of(before).Dialog is null))
            return PlaceKind.Dialog;
        if (fresh.Any(project.PlaceSigns.IsOpenMenu))
            return PlaceKind.Menu;
        if (candidate.IsTab)
            return fresh.Count > 0 || callCount > 0 ? PlaceKind.Tab : null;
        if (fresh.Count >= 4)
            return PlaceKind.Part;
        return callCount > 0 ? PlaceKind.Action : null;
    }

    // What may be pressed in a state, in the order of the page, what deletes last. Links are not pressed: where they go is put on the frontier.
    private async Task<List<Candidate>> CandidatesAsync(PlacesList list, string pageKey, ScrapeSnapshot snapshot, IReadOnlySet<string> known, List<string> notPressed, bool inForm, CancellationToken cancellationToken)
    {
        var never = new HashSet<int>(await MatchAsync(_settings.NeverClick.Where(match => match.Kind == "selector").Select(match => match.Value), cancellationToken).ConfigureAwait(false));
        if (!_careful)
            never.ExceptWith(await MatchAsync(_settings.WalkPresses, cancellationToken).ConfigureAwait(false));
        var context = new JudgeContext(_settings, uri => project.IsOwnPage(uri.AbsoluteUri))
        {
            NeverNodes = never,
            AlwaysNodes = await MatchAsync(_settings.AlwaysClick.Where(match => match.Kind == "selector").Select(match => match.Value), cancellationToken).ConfigureAwait(false),
        };
        var chrome = await MatchAsync(_settings.WalkOnce, cancellationToken).ConfigureAwait(false);
        var refs = NodeRef.Of(snapshot);
        var firstRow = new Dictionary<int, int>();
        var perSignature = new Dictionary<string, int>(StringComparer.Ordinal);
        var found = new List<Candidate>();
        foreach (var judgement in ClickJudge.Judge(snapshot, context))
        {
            var node = snapshot[judgement.Node];
            if (judgement.Action == JudgedAction.Visit)
            {
                if (judgement.Verdict == Verdict.Safe && judgement.Url is { } url)
                    Found(list, PlacesList.Known(url, PlacesSeeds.Walk, $"a link on {pageKey}") with { Address = PlacesList.KeyAndPath(url).Path });
                continue;
            }

            var target = refs[judgement.Node];
            // A button drawn inside a button is the same button: the outer one is pressed.
            if (known.Contains(target.Key) || !CanBePressed(snapshot, node) || InAButton(snapshot, node) || (_chromeDone && IsChrome(snapshot, node, chrome)))
                continue;
            var label = Label(snapshot, node, judgement.Label);
            var isTab = project.PlaceSigns.IsTab(node);
            // A cross closes what is open or empties a field: it leads nowhere.
            if (judgement.Reason is "sorts a table" or "a pager" or "closes" || project.PlaceSigns.IsClose(label) || (options.LinksAndTabs && !isTab))
                continue;
            if (WhyNot(judgement, label, inForm) is { } why)
            {
                notPressed.Add($"{label} ({why})");
                continue;
            }

            // One row stands for all rows of its table.
            var inRow = false;
            if (RowOf(snapshot, node) is { } row)
            {
                inRow = true;
                if (!firstRow.TryAdd(row.Table, row.Row) && firstRow[row.Table] != row.Row)
                    continue;
            }

            var signature = target.Key[..target.Key.LastIndexOf('#')];
            if ((perSignature[signature] = perSignature.GetValueOrDefault(signature) + 1) > 2)
                continue;
            // Two things of one name on a page are two places: the second says which it is.
            var name = found.Count(other => other.Label == label || other.Label.StartsWith(label + " (", StringComparison.Ordinal)) is var same and > 0 ? $"{label} ({same + 1}nd of that name)" : label;
            found.Add(new Candidate(target.Key, name, isTab, inRow, DeleteWords.Any(word => DangerWords.Contains(label, word))));
        }

        if (!_careful && !options.LinksAndTabs)
            found.AddRange(FirstRows(snapshot, refs, known));
        return [.. found.OrderBy(candidate => candidate.Deletes)];
    }

    // A row that opens its record when it is pressed says so nowhere. So the first row of each table is pressed once,
    // on a cell with a text and no field in it, unless the row has a link: where that goes is known already.
    private static IEnumerable<Candidate> FirstRows(ScrapeSnapshot snapshot, IReadOnlyDictionary<int, NodeRef> refs, IReadOnlySet<string> known)
    {
        var tables = new HashSet<int>();
        foreach (var row in snapshot.Nodes.Where(node => node.Type == "tr" && node.Bounds is { Width: > 0, Height: > 0 }))
        {
            var cells = snapshot.ChildrenOf(row.Number).Where(cell => cell.Type == "td" && cell.Bounds is { Width: > 0, Height: > 0 }).ToList();
            if (cells.Count == 0 || RowOf(snapshot, cells[0]) is not { } at || !tables.Add(at.Table))
                continue;
            if (Below(snapshot, row, 5).Any(node => node.Type == "a" && node.Attribute("href") is { Length: > 0 }))
                continue;
            var cell = cells.FirstOrDefault(each => snapshot.TextWithin(each) is { Length: > 0 } && !Below(snapshot, each, 4).Any(node => IsField(node) || IsTick(node) || node.Type == "button"));
            if (cell is null || known.Contains(refs[cell.Number].Key))
                continue;
            yield return new Candidate(refs[cell.Number].Key, tables.Count == 1 ? "the first row" : $"the first row ({tables.Count}nd table)", IsTab: false, InRow: true, Deletes: false);
        }
    }

    // Why a button is not pressed, or null when it is.
    private string? WhyNot(Judgement judgement, string label, bool inForm)
    {
        if (judgement.Verdict == Verdict.Unsafe && judgement.Reason != "submits a form")
            return judgement.Reason;
        if (_careful && judgement.Verdict != Verdict.Safe)
            return "production: only what is known to show more";
        // A form is saved by a test, through its page object: the walk reads it and leaves it.
        return judgement.Reason == "submits a form" || (inForm && SaveWords.Any(word => DangerWords.Contains(label, word))) ? "a save: by a page object" : null;
    }

    // The place as the page's facts fill it in. A record's own page keeps no heading and no title: they are the record's name.
    private static Place Read(Place place, PageFacts facts, bool ofRecord, bool isDialog)
    {
        var fields = facts.Fields.Select(field => field.ToListText()).ToArray();
        var open = facts.Fields.Count(field => !field.Disabled && !field.InTable);
        var saves = facts.Buttons.Any(button => SaveWords.Any(word => DangerWords.Contains(button, word)));
        // A list with a search box beside it is no form; a form with a table of lines in it is one.
        var form = saves && (isDialog ? open >= 1 : open >= 2 && (facts.Columns.Count == 0 || open >= 4));
        return place with
        {
            State = form ? PlaceState.FormNotSaved : PlaceState.Seen,
            Title = ofRecord || facts.Title.Length == 0 ? null : facts.Title,
            Heading = ofRecord ? null : facts.Headings.Select(Place.Clean).FirstOrDefault(heading => heading.Length > 0),
            Fields = fields,
            Buttons = [.. facts.Buttons.Select(Place.Clean).Where(button => button.Length > 0).Distinct(StringComparer.Ordinal).Take(40)],
            Tabs = [.. facts.Tabs.Select(tab => Place.Clean(tab.TrimEnd('*', ' '))).Where(tab => tab.Length > 0).Distinct(StringComparer.Ordinal)],
            Columns = [.. facts.Columns.Select(Place.Clean).Where(column => column.Length > 0).Distinct(StringComparer.Ordinal).Take(40)],
        };
    }

    // The calls a press made, as the list holds them: a writing call always, a reading one for a button that does nothing else.
    private string[] CallTexts(IReadOnlyList<PageCall> made, IReadOnlyList<BlockedRequest> held, bool all)
    {
        // What a writing call sends is kept as its fields' names, never their values: a form's contract with the back end.
        static string Sends(IReadOnlyList<string> fields) => fields.Count == 0 ? "" : $" sends: {string.Join(", ", fields)}";
        var texts = held.Select(blocked => $"{blocked.Method} {CallKey(blocked.Url)} (held){Sends(blocked.Sends)}")
            .Concat(made.Where(call => all || call.Method is not ("GET" or "HEAD" or "OPTIONS")).Select(call => $"{call.Method} {CallKey(call.Url)}{(call.Status >= 400 ? $" -> {call.Status}" : "")}{Sends(PageCall.FieldsOf(call.Body))}"));
        return [.. texts.Distinct(StringComparer.Ordinal).Take(8)];
    }

    // An address of a call without its ids and its query; its host when it is not the app's own.
    private string CallKey(string url)
    {
        if (!Uri.TryCreate(url.TrimEnd('…'), UriKind.Absolute, out var uri))
            return url;
        var key = PageKey.Of(uri.GetLeftPart(UriPartial.Path));
        return project.IsOwnPage(uri.AbsoluteUri) ? key : uri.Host + key;
    }

    private async Task<ScrapeSnapshot?> OpenAsync(string address, CancellationToken cancellationToken)
    {
        // What a page asks for while it opens is a read: a POST among it is remembered as one.
        guard.Learning = true;
        try
        {
            _ = calls.TakeCalls();
            // A shared environment stops answering for a while: once more before the page is given up.
            if (!await source.GoToAsync(address, cancellationToken).ConfigureAwait(false) && !await source.GoToAsync(address, cancellationToken).ConfigureAwait(false))
                return null;
            await PauseAsync(cancellationToken).ConfigureAwait(false);
            return await MovingPage.ReadAsync(capture, PauseAsync, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            guard.Learning = false;
        }
    }

    // The page as it opens, and the presses that led to a state made again.
    private async Task<ScrapeSnapshot?> ReachAsync(string address, IReadOnlyList<NodeRef> prefix, CancellationToken cancellationToken)
    {
        var snapshot = await OpenAsync(address, cancellationToken).ConfigureAwait(false);
        foreach (var press in prefix)
        {
            if (snapshot is null)
                return null;
            var node = NodeRef.Of(snapshot).Values.FirstOrDefault(r => r.Key == press.Key);
            if (node is null || await source.ClickAsync(node.Number, cancellationToken).ConfigureAwait(false) is not null)
                return null;
            await PauseAsync(cancellationToken).ConfigureAwait(false);
            snapshot = await MovingPage.ReadAsync(capture, PauseAsync, cancellationToken).ConfigureAwait(false);
        }

        if (snapshot is not null && IsSignIn(snapshot))
        {
            _signIn = true;
            return null;
        }

        return snapshot;
    }

    private Place Kept(PlacesList list, Place child, ScrapeSnapshot after, string label) =>
        child with { Snapshot = Keep?.Invoke(after, label), SameAs = child.Kind == PlaceKind.Dialog ? SameForm(list, child) : null };

    // A dialog of the same page with the same fields is the same form, opened another way.
    private static string? SameForm(PlacesList list, Place dialog) =>
        dialog.Fields.Count == 0 ? null : list.Places.FirstOrDefault(other => other.Kind == PlaceKind.Dialog && other.Parent == dialog.Parent && other.Key != dialog.Key && other.SameAs is null
            && other.Fields.Select(FieldLabel).SequenceEqual(dialog.Fields.Select(FieldLabel), StringComparer.Ordinal))?.Key;

    private static string FieldLabel(string field) => PlaceField.Parse(field).Label;

    private void Found(PlacesList list, Place place)
    {
        if (list.Add(place))
        {
            _found++;
            Say($"found {place.Key} ({place.Kind.ToString().ToLowerInvariant()})");
        }
    }

    private string Address(string path) => new Uri(new Uri(project.Config.StartUrl), path).AbsoluteUri;

    private bool IsSignIn(ScrapeSnapshot snapshot)
    {
        var password = snapshot.Nodes.Any(node => node.Type == "input" && node.Attribute("type") == "password" && node.Bounds is not null);
        return password && (!project.IsOwnPage(snapshot.Location) || (_settings.LoginUrl.Length > 0 && snapshot.Location.Contains(_settings.LoginUrl, StringComparison.OrdinalIgnoreCase)))
            || (_settings.LoginUrl.Length > 0 && Uri.TryCreate(_settings.LoginUrl, UriKind.Absolute, out var login) && snapshot.Location.StartsWith(login.GetLeftPart(UriPartial.Path), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlySet<int>> MatchAsync(IEnumerable<string> selectors, CancellationToken cancellationToken)
    {
        var nodes = new HashSet<int>();
        foreach (var selector in selectors)
            nodes.UnionWith(await source.MatchAsync(selector, cancellationToken).ConfigureAwait(false));
        return nodes;
    }

    // What is the same on every page: what the config names (and what is in it), a nav, a header, a footer.
    private static bool IsChrome(ScrapeSnapshot snapshot, ScrapeNode node, IReadOnlySet<int> named)
    {
        for (var n = node.Number; n != 0; n = snapshot[n].Parent)
        {
            var at = snapshot[n];
            if (named.Contains(n) || at.Type is "nav" or "header" or "footer" || at.Role is "navigation" or "banner" or "contentinfo")
                return true;
        }

        return false;
    }

    // On the screen, not off, and a button: a field is read, not pressed (a box that is ticked, a choice of a list, a
    // field that opens its list, a line of a menu with a box in it), and an editor's own buttons lead nowhere.
    private bool CanBePressed(ScrapeSnapshot snapshot, ScrapeNode node) =>
        node.Bounds is { Width: > 0, Height: > 0 } && !node.Attributes.ContainsKey("disabled") && node.Attribute("aria-disabled") != "true"
        && !IsField(node) && !Below(snapshot, node, 3).Skip(1).Any(IsTick) && !InToolbar(snapshot, node);

    private static bool IsField(ScrapeNode node) =>
        node.Type is "input" or "select" or "textarea" or "option"
        || node.Role is "checkbox" or "switch" or "radio" or "option" or "menuitemcheckbox" or "menuitemradio" or "combobox" or "listbox" or "textbox"
        || node.Attribute("aria-haspopup") == "listbox";

    private static bool InAButton(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var level = 0;
        for (var at = node.Parent; at != 0 && level < 4; at = snapshot[at].Parent, level++)
        {
            if (snapshot[at].Type == "button" || snapshot[at].Role == "button")
                return true;
        }

        return false;
    }

    // A line of a menu with a box to tick in it (a list's column chooser) is a field too.
    private static bool IsTick(ScrapeNode node) =>
        node.Role is "checkbox" or "switch" or "radio" || (node.Type == "input" && node.Attribute("type") is "checkbox" or "radio");

    // A text editor's own buttons (bold, italic) change the text in it, and lead nowhere.
    private bool InToolbar(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        for (var n = node.Number; n != 0; n = snapshot[n].Parent)
        {
            if (project.PlaceSigns.IsToolbar(snapshot[n]))
                return true;
        }

        return false;
    }

    // The row of a table an element is in, and its table: a body's row, not a header's.
    private static (int Table, int Row)? RowOf(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        int? row = null;
        for (var n = node.Parent; n != 0; n = snapshot[n].Parent)
        {
            var at = snapshot[n];
            if (at.Type == "thead" || at.Role == "columnheader")
                return null;
            if (row is null && (at.Type == "tr" || at.Role == "row"))
                row = n;
            else if (row is not null && (at.Type == "table" || at.Role is "table" or "grid" or "treegrid"))
                return (n, row.Value);
        }

        return row is { } alone ? (snapshot[alone].Parent, alone) : null;
    }

    // What a button says; for one that only shows an icon, the icon's name.
    private string Label(ScrapeSnapshot snapshot, ScrapeNode node, string judged)
    {
        var said = Place.Clean(judged.Trim().Trim((char)0x200B));
        // A button's name is a few words. A longer text is what a record says (a note in a calendar, a card's first line): the list keeps none of it.
        if (said.Split(' ').Length > 7)
            return "a long text";
        if (said.Length > 0 && said != node.Type)
            return said;
        var below = Below(snapshot, node, 5).ToList();
        // What the page says of a button with only an icon comes before the icon's own name.
        var told = below.Select(at => at.Attribute("aria-label") ?? at.Attribute("title") ?? at.Attribute("data-original-title"))
            .Select(text => text is null ? "" : Place.Clean(text)).FirstOrDefault(text => text.Length > 0 && text.Split(' ').Length <= 7);
        if (told is not null)
            return told;
        var icon = below.SelectMany(Classes).FirstOrDefault(project.PlaceSigns.IsIconName);
        if (icon is not null)
            return RowOf(snapshot, node) is null && CardTitle(snapshot, node) is { } card ? $"{card}: {icon}" : icon;
        // A field that opens a list says what it is in its label or its placeholder, not in itself.
        var named = below.Select(at => at.Attribute("aria-label") ?? at.Attribute("placeholder") ?? at.Attribute("title") ?? at.Name ?? at.Text)
            .Select(text => text is null ? "" : Place.Clean(text)).FirstOrDefault(text => text.Length > 0);
        return named ?? node.Attribute("data-cy") ?? node.Attribute("data-testid") ?? node.Attribute("id") ?? node.Type;
    }

    // The title of the card a button is in, when it is in the card's title bar: two pens on one page are the pens of
    // two cards, and each is named after its own. A title is a few words; a longer text is what a record says.
    private string? CardTitle(ScrapeSnapshot snapshot, ScrapeNode node)
    {
        var level = 0;
        for (var at = node.Parent; at != 0 && level < 6; at = snapshot[at].Parent, level++)
        {
            if (!project.PlaceSigns.IsTitle(snapshot[at]))
                continue;
            var title = Below(snapshot, snapshot[at], 2).Where(each => each.Number < node.Number && each.Text is { Length: > 0 }).Select(each => Place.Clean(each.Text!)).FirstOrDefault(text => text.Length > 0);
            return title is not null && title.Split(' ').Length <= 5 ? title : null;
        }

        return null;
    }

    private static IEnumerable<ScrapeNode> Below(ScrapeSnapshot snapshot, ScrapeNode node, int depth)
    {
        yield return node;
        if (depth == 0)
            yield break;
        foreach (var child in snapshot.ChildrenOf(node.Number))
            foreach (var below in Below(snapshot, child, depth - 1))
                yield return below;
    }

    private static string[] Classes(ScrapeNode node) => node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static HashSet<string> Visible(ScrapeSnapshot snapshot, IReadOnlyDictionary<int, NodeRef> refs) =>
        refs.Values.Where(r => snapshot[r.Number].Bounds is not null).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

    private Task PauseAsync(CancellationToken cancellationToken) => Task.Delay(_settings.DelayMs, cancellationToken);

    private void Say(string doing) => options.Progress?.Report(doing);

    private void OnDownloaded(object? sender, string file) => _downloads.Add(file);

    private void OnDialog(object? sender, string message) => _nativeDialogs++;

    [GeneratedRegex(@"^(40[134]|500)\b|\bForbidden\b|\bNot Found\b|\bInternal Server Error\b|could not be executed|access denied|not allowed|no access|no permission|geen toegang|niet toegestaan", RegexOptions.IgnoreCase)]
    private static partial Regex Refusal();

    private sealed record Candidate(string Key, string Label, bool IsTab, bool InRow, bool Deletes);

    private sealed class Budget(int left, Stopwatch clock)
    {
        public int Left { get; set; } = left;

        public Stopwatch Clock { get; } = clock;
    }
}
