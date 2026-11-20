using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

/// <summary>What reading the app's scripts for routes gave.</summary>
/// <param name="Pages">The pages opened.</param>
/// <param name="Scripts">The scripts read: a page's own, not what every page loads.</param>
/// <param name="Routes">The routes they name.</param>
/// <param name="New">How many of those the list did not hold.</param>
/// <param name="Problem">What stopped it; null when nothing did.</param>
public sealed record RoutesRead(int Pages, int Scripts, int Routes, int New, string? Problem);

/// <summary>A class that no longer fits the place it was drafted from.</summary>
/// <param name="Class">The class.</param>
/// <param name="Place">Its place.</param>
/// <param name="Note">What is the matter besides the two lists; empty when they say it.</param>
/// <param name="NotOnPlace">Labels the class names that the place does not show.</param>
/// <param name="NotInClass">What the place shows that the class does not name.</param>
public sealed record PlaceDrift(string Class, string Place, string Note, IReadOnlyList<string> NotOnPlace, IReadOnlyList<string> NotInClass);

public sealed partial class ScraperService
{
    /// <summary>
    /// Adds to a list of places what is known without opening the app: the pages the project's recordings captured.
    /// </summary>
    /// <returns>How many places were new to the list.</returns>
    public int SeedPlaces(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return PlacesSeeds.FromRecordings(Project(projectName)).Count(list.Add);
    }

    /// <summary>
    /// Walks the app for its places (see <see cref="Walker"/>), from the list's frontier, as the project's saved
    /// sign-in or another account's beside it. The list is saved after every page, so a walk that stops loses one
    /// place. A sign-in that ran out is renewed by the config's <c>RenewSignIn</c>, once, and the walk goes on.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="listFile">The list's file; read when it is there, written as the walk goes.</param>
    /// <param name="options">How this walk goes.</param>
    /// <param name="account">The account to walk as (<c>Reader</c>); null for the project's own.</param>
    /// <param name="visible">Whether the browser shows.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <exception cref="ScraperProblem">A recording holds the profile, or the platform cannot be walked.</exception>
    public async Task<WalkResult> WalkPlacesAsync(string? projectName, string listFile, WalkOptions options, string? account, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var project = Project(projectName);
        var list = PlacesList.Load(listFile);
        var run = options with { Account = string.IsNullOrWhiteSpace(account) ? Path.GetFileNameWithoutExtension(project.SignInStatePath) ?? "own" : account };
        var result = await WalkOnceAsync(project, list, listFile, run, account, visible, cancellationToken).ConfigureAwait(false);
        if (result.Stop != WalkStop.SignIn || project.Config.Explore.RenewSignIn.Length == 0)
            return result;

        var command = RenewLine(project.Config.Explore.RenewSignIn, run.Account);
        if (await RunRenewAsync(command, project.Folder, cancellationToken).ConfigureAwait(false) is { } failed)
            return result with { Notes = [.. result.Notes, $"The saved sign-in ran out, and the command that renews it failed ({command}): {failed}"] };
        var again = await WalkOnceAsync(project, list, listFile, run with { MaxMinutes = Math.Max(1, run.MaxMinutes) }, account, visible, cancellationToken).ConfigureAwait(false);
        return again with
        {
            Opened = result.Opened + again.Opened,
            Found = result.Found + again.Found,
            Presses = result.Presses + again.Presses,
            Held = result.Held + again.Held,
            Notes = [.. result.Notes, .. again.Notes],
        };
    }

    /// <summary>The places of a list whose capture the walk kept and that is still on disk, each with that capture.</summary>
    public IReadOnlyList<(Place Place, ScrapeSnapshot Snapshot)> KeptPlaces(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        return [.. list.Places.Where(place => place.Snapshot is not null)
            .Select(place => (Place: place, File: Path.Combine(project.RecordingsFolder, place.Snapshot!)))
            .Where(each => File.Exists(each.File))
            .Select(each => (each.Place, Recordings.SnapshotFile.Read(each.File)))];
    }

    /// <summary>
    /// Reads each kept place again with the project's signs and components as they are now
    /// (<see cref="PlacesReading.Read"/>): after a sign or a component was added, no walk is needed.
    /// </summary>
    /// <returns>How many places changed.</returns>
    public int ReadPlacesAgain(string? projectName, PlacesList list)
    {
        var project = Project(projectName);
        var code = Code.ProjectCode.Read(project);
        var changed = 0;
        foreach (var (place, snapshot) in KeptPlaces(projectName, list))
        {
            var read = PlacesReading.Read(place, snapshot, project.ControlSigns, project.Config.Components, code, project.PlaceSigns);
            if (read.Component == place.Component && read.Fields.SequenceEqual(place.Fields, StringComparer.Ordinal) && read.Buttons.SequenceEqual(place.Buttons, StringComparer.Ordinal))
                continue;
            list.Set(read);
            changed++;
        }

        return changed;
    }

    /// <summary>What the places of a list still owe, by group, held against the suite's source (<see cref="Factory.Owed"/>).</summary>
    public Factory.OwedReport OwedPlaces(string? projectName, PlacesList list) =>
        Factory.Owed.Of(list, Factory.SuiteSource.Read(Project(projectName)), Project(projectName).PlaceSigns);

    /// <summary>The app's own elements over the kept places, with what stands for each (<see cref="PlacesReading.Elements"/>).</summary>
    public IReadOnlyList<ElementCount> PlaceElements(string? projectName, PlacesList list)
    {
        var project = Project(projectName);
        return PlacesReading.Elements(KeptPlaces(projectName, list), project.ControlSigns, Code.ProjectCode.Read(project));
    }

    /// <summary>
    /// Drafts a class from a place of the list, in the suite's shape (<see cref="PlaceDrafter"/>,
    /// <c>draft-templates.md</c> beside the project's config): a form, a list's row, or the test that holds a form
    /// against its class.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="list">The list.</param>
    /// <param name="placeKey">The place's key.</param>
    /// <param name="kind">What to draft.</param>
    /// <param name="className">The class's name; from the place when null.</param>
    /// <param name="pageClass">For a dialog, the class of its page; found by the page's address in the project's pages when null.</param>
    /// <returns>The draft, and the file it belongs in (a form and a row; null for a test, whose place is the person's).</returns>
    /// <exception cref="ScraperProblem">The place is not in the list, the project has no templates, or a dialog's page has no class.</exception>
    public (PlaceDraft Draft, string? File) DraftPlace(string? projectName, PlacesList list, string placeKey, PlaceDraftKind kind, string? className = null, string? pageClass = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        var place = list[placeKey] ?? throw new ScraperProblem($"The list has no place '{placeKey}'.");
        var file = Path.Combine(project.Home, DraftTemplates.FileName);
        if (!File.Exists(file))
            throw new ScraperProblem($"{project.Name} has no {DraftTemplates.FileName}: how the suite writes a form, a row and a test is not known.");
        var templates = DraftTemplates.Parse(File.ReadAllText(file));
        var (folder, ns) = project.PagesFor(place.Parent ?? place.Key);
        try
        {
            if (kind == PlaceDraftKind.Row)
            {
                var row = PlaceDrafter.Row(place, templates, className ?? PlaceDrafter.ClassName(place, kind, project.PlaceSigns), ns, project.PlaceSigns);
                return Target(Path.Combine(project.Folder, folder), row);
            }

            // A dialog's page: the class that names the page's address, or the one that was drafted from the page (a
            // record's page names its address with the record's id in it, which the page's key does not have).
            var page = pageClass ?? (place.Kind == PlaceKind.Page ? null : PageClassAt(project, place.Parent ?? "") ?? DraftedFrom(project, place.Parent ?? "", "form"))
                ?? (place.Kind == PlaceKind.Page ? null : throw new ScraperProblem($"No page class of {project.Name} names the address {place.Parent}: draft or write that page first, or give its class."));
            // A form's test names the class that was drafted from the place, whatever it was called then.
            var formName = DraftedFrom(project, place.Key, "form")
                ?? PlaceDrafter.ClassName(place, PlaceDraftKind.Form, project.PlaceSigns);
            if (kind == PlaceDraftKind.FormTest)
                return (PlaceDrafter.FormTest(place, templates, className ?? formName + "Tests", ns.Replace(".Pages", ".Tests", StringComparison.Ordinal), formName, page ?? formName, ns, project.PlaceSigns, place.Parent is null ? null : list[place.Parent]), null);
            // What a page's cards open: the dialogs of the page that the walk named after a card.
            var opens = place.Kind == PlaceKind.Page ? list.Places.Where(each => each.Kind == PlaceKind.Dialog && each.Parent == place.Key).Select(each => each.Key[(each.Key.LastIndexOf(" › ", StringComparison.Ordinal) + 3)..]).ToList() : null;
            var form = PlaceDrafter.Form(place, templates, className ?? formName, ns, page, opens, project.PlaceSigns);
            return Target(Path.Combine(project.Folder, folder), form);
        }
        catch (FormatException e)
        {
            throw new ScraperProblem(e.Message);
        }
    }

    /// <summary>
    /// Writes a draft into its file. A file that is there and declares the class is merged into
    /// (<see cref="ClassMerge"/>): a class the Scraper drafted from a place gets the draft's members, a class a person
    /// wrote is only added to; what only the class has stays either way.
    /// </summary>
    /// <param name="file">The file the draft belongs in.</param>
    /// <param name="draft">The draft.</param>
    /// <returns>What the merge did; null when the file was not there, or does not declare the class, and the draft was written as it is.</returns>
    public static MergedClass? WriteDraft(string file, PlaceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(draft);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var before = File.Exists(file) ? File.ReadAllText(file) : null;
        if (before is null || !DeclaresClass(before, draft.ClassName))
        {
            File.WriteAllText(file, draft.Code);
            return null;
        }

        var merged = ClassMerge.Merge(before, draft.Code, draft.ClassName, addOnly: ScrapedHeader.Find(before)?.Page is null);
        if (merged.Code != before)
            File.WriteAllText(file, merged.Code);
        return merged;
    }

    private static bool DeclaresClass(string code, string className) =>
        System.Text.RegularExpressions.Regex.IsMatch(code, $@"\bclass\s+{System.Text.RegularExpressions.Regex.Escape(className)}\b");

    // The file of the suite that declares a class: its own file before an older draft's part.
    private static string? ClassFile(ScrapeProject project, string className) =>
        Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(PageDrafter.IsOlderDraft).ThenBy(file => file, StringComparer.Ordinal)
            .FirstOrDefault(file => DeclaresClass(File.ReadAllText(file), className));

    // Where a draft goes: its class's own file. A class drafted before a draft was the whole class stays what it is,
    // one part of a partial class in its older file, beside a person's part.
    private static (PlaceDraft Draft, string? File) Target(string folder, PlaceDraft draft)
    {
        var older = Path.Combine(folder, draft.ClassName + PageDrafter.OlderExtension);
        if (!File.Exists(older))
            return (draft, Path.Combine(folder, draft.ClassName + PageDrafter.Extension));
        var declared = $" class {draft.ClassName}(";
        var asPart = draft.Code.Contains(" partial" + declared, StringComparison.Ordinal) ? draft.Code : draft.Code.Replace(declared, " partial" + declared, StringComparison.Ordinal);
        return (draft with { Code = asPart }, older);
    }

    // The class a draft of a place was written as: the file whose first line names the place and the kind.
    private static string? DraftedFrom(ScrapeProject project, string placeKey, string variant) =>
        Directory.EnumerateFiles(project.Folder, "*" + PageDrafter.Extension, SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => ScrapedHeader.Find(File.ReadAllText(file)) is { } header && header.Page == placeKey && header.Variant == variant)
            .Select(PageDrafter.ClassOf).FirstOrDefault();

    // The page class that opens at an address: the class whose declaration, up to its body, names that address in
    // quotes (a base that takes it, whatever the base is called). One file may hold several pages.
    private static string? PageClassAt(ScrapeProject project, string key)
    {
        var addresses = new[] { key, key.TrimEnd('/', '#') }.Distinct(StringComparer.Ordinal).Select(System.Text.RegularExpressions.Regex.Escape);
        var declaration = new System.Text.RegularExpressions.Regex("class\\s+(\\w+)[^{;]*?\"(?:" + string.Join('|', addresses) + ")\"");
        return Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(file => declaration.Match(File.ReadAllText(file)))
            .FirstOrDefault(match => match.Success)?.Groups[1].Value;
    }

    /// <summary>
    /// The classes drafted from a place whose place reads differently now: for each, the labels it names that the
    /// place no longer shows and what the place shows that it does not name (<see cref="PlaceDrafter.Drift"/>).
    /// A class is held against the place its first line names (<c>// &lt;scraped page="…" variant="form" …&gt;</c>).
    /// </summary>
    public IReadOnlyList<PlaceDrift> PlaceDrifts(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        var drifts = new List<PlaceDrift>();
        foreach (var group in ProjectCode.Read(project).Where(each => File.Exists(each.File)).GroupBy(each => each.Name.Split('<')[0], StringComparer.Ordinal))
        {
            var header = group.Select(each => each.Header).FirstOrDefault(each => each?.Page is not null && each.Variant is "form" or "row");
            if (header is null)
                continue;
            var kind = header.Variant == "row" ? PlaceDraftKind.Row : PlaceDraftKind.Form;
            if (list[header.Page!] is not { } place)
            {
                drifts.Add(new PlaceDrift(group.Key, header.Page!, "the list has no such place any more", [], []));
                continue;
            }

            if (PlaceDrafter.Shape(place, kind, project.PlaceSigns) == header.Hash)
                continue;
            var code = string.Join('\n', group.Select(each => each.File).Distinct(StringComparer.OrdinalIgnoreCase).Select(File.ReadAllText));
            var (notOnPlace, notInClass) = PlaceDrafter.Drift(place, code, kind, project.PlaceSigns);
            drifts.Add(new PlaceDrift(group.Key, place.Key, notOnPlace.Count + notInClass.Count == 0 ? "read differently, though every label is named: draft it again for its new first line" : "", notOnPlace, notInClass));
        }

        return drifts;
    }

    private async Task<WalkResult> WalkOnceAsync(ScrapeProject project, PlacesList list, string listFile, WalkOptions options, string? account, bool visible, CancellationToken cancellationToken)
    {
        var (source, guard) = await StartGuardedAsync(project, account, visible, cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            // What the walk reads is kept as a recording session of its own, started with its first place: the
            // patterns, the control suggestions and the drafts read a walked place as they read a recorded page.
            RecordingSession? session = null;
            var walker = new Walker(project, source, source, guard, options, source.CaptureAsync, changed => changed.Save(listFile))
            {
                Keep = (snapshot, state) =>
                {
                    session ??= project.StartSession($"walk-{options.Account}", DateTimeOffset.Now);
                    return $"{session.Id}/{session.AddPage(snapshot, null, state).Snapshot}";
                },
            };
            return await walker.RunAsync(list, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the routes the app's own scripts name into a list of places: each page of the list is opened, the scripts
    /// only it (and a few like it) loads are read as text with the browser's session, and the route tables in them
    /// become known pages (<see cref="PlacesSeeds.FromScript"/>). Nothing is pressed.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="list">The list: its pages are opened, the routes are added to it.</param>
    /// <param name="account">The account to read as; null for the project's own.</param>
    /// <param name="visible">Whether the browser shows.</param>
    /// <param name="progress">Told what is being read.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    public async Task<RoutesRead> ReadRoutesAsync(string? projectName, PlacesList list, string? account, bool visible, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        // A single-page app's routes hang under a page of the server: one visit per such page.
        var pages = list.Places.Where(place => place.Kind == PlaceKind.Page && place.Address is not null && place.State != PlaceState.Refused)
            .Select(place => place.Address!.Split('#')[0].Split('?')[0])
            .Where(path => !path.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        var loadedBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var (source, guard) = await StartGuardedAsync(project, account, visible, cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            // Nothing is pressed here: what a page asks for while it opens is a read.
            guard.Learning = true;
            var opened = 0;
            string? last = null;
            var again = new List<string>();
            // A page that did not open comes once more at the end: the first pages of a run wait for the app's scripts.
            for (var at = 0; at < pages.Count + again.Count; at++)
            {
                var page = at < pages.Count ? pages[at] : again[at - pages.Count];
                cancellationToken.ThrowIfCancellationRequested();
                // What a page asked for after its turn was over is still its own.
                if (last is not null)
                    Loaded(loadedBy, last, source.TakeScripts().Where(project.IsOwnPage));
                else
                    _ = source.TakeScripts();
                last = null;
                progress?.Report($"opening {page}");
                var address = new Uri(new Uri(project.Config.StartUrl), page).AbsoluteUri;
                // A shared environment stops answering for a while: once more before the page is given up.
                if (!await source.GoToAsync(address, cancellationToken).ConfigureAwait(false) && !await source.GoToAsync(address, cancellationToken).ConfigureAwait(false))
                {
                    progress?.Report($"{page} did not open: {source.LastOpenProblem}");
                    if (at < pages.Count)
                        again.Add(page);
                    continue;
                }
                // The scripts are what is wanted, not the page: it gets a moment to ask for them, and is not read.
                await Task.Delay(Math.Max(project.Config.Explore.DelayMs, 1000), cancellationToken).ConfigureAwait(false);
                if (!project.IsOwnPage(source.Location) || (project.Config.Explore.LoginUrl.Length > 0 && source.Location.Contains(project.Config.Explore.LoginUrl, StringComparison.OrdinalIgnoreCase)))
                    return new RoutesRead(opened, 0, 0, 0, $"A sign-in page came ({source.Location}): the saved sign-in ran out. Run a test that signs in, then read the routes again.");
                opened++;
                Loaded(loadedBy, page, source.TakeScripts().Where(project.IsOwnPage));
                last = page;
            }

            if (last is not null)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                Loaded(loadedBy, last, source.TakeScripts().Where(project.IsOwnPage));
            }

            // What every page loads (the framework, the shared parts) holds no page's routes.
            var few = Math.Max(3, opened / 4);
            var read = 0;
            var routes = 0;
            var fresh = 0;
            foreach (var (script, by) in loadedBy.Where(pair => pair.Value.Count <= few).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await source.ReadTextAsync(script, cancellationToken).ConfigureAwait(false) is not { } text)
                    continue;
                read++;
                foreach (var place in by.SelectMany(page => PlacesSeeds.FromScript(page, text)))
                {
                    routes++;
                    if (list.Add(place))
                        fresh++;
                }
            }

            return new RoutesRead(opened, read, routes, fresh, null);
        }
    }

    private static void Loaded(Dictionary<string, List<string>> loadedBy, string page, IEnumerable<string> scripts)
    {
        foreach (var script in scripts.Distinct(StringComparer.Ordinal))
        {
            if (!loadedBy.TryGetValue(script, out var by))
                loadedBy[script] = by = [];
            if (!by.Contains(page, StringComparer.Ordinal))
                by.Add(page);
        }
    }

    // The browser, started as the account's saved sign-in in its own profile, with the project's guards on.
    private async Task<(PlaywrightScrapeSource Source, RequestGuard Guard)> StartGuardedAsync(ScrapeProject project, string? account, bool visible, CancellationToken cancellationToken)
    {
        if (_recording is not null)
            throw new ScraperProblem("A recording is in progress, and it holds the project's browser profile: stop it first.");
        if (_sourceFor(project.Config.Platform) is not PlaywrightScrapeSource source)
            throw new ScraperProblem($"{project.Config.Platform} apps cannot be walked yet: html and blazor can.");
        if (!Uri.TryCreate(project.Config.StartUrl, UriKind.Absolute, out var start))
            throw new ScraperProblem($"{project.Name} has no StartUrl in its {ScraperConfig.FileName}.");
        if (RequestGuard.RefuseHost(start, project.Config.Explore) is { } refused)
            throw new ScraperProblem(refused);
        var guard = new RequestGuard(project.Config.Explore) { HoldWrites = true };
        try
        {
            await source.StartAsync(new ScrapeStart("about:blank", ProfileFor(project.ProfileFolder, account), visible) { SignInState = SignInFor(project.SignInStatePath, account) }, cancellationToken).ConfigureAwait(false);
            await source.GuardAsync(guard).ConfigureAwait(false);
        }
        catch
        {
            await source.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return (source, guard);
    }
}
