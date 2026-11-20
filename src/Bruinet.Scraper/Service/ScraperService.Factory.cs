using System.Diagnostics;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Service;

// The factory's measures: what replaying the kept places gives, and the examples held against their places.
public sealed partial class ScraperService
{
    /// <summary>
    /// Drafts a spec from the drafted pages of one address (<see cref="SpecDrafter"/>): a rule and an example per page
    /// that a tab of that address shows, in the suite's own phrases, without the steps the suite's other specs have.
    /// </summary>
    /// <param name="projectName">The project.</param>
    /// <param name="list">The list of places.</param>
    /// <param name="address">The address the pages share, before its <c>#</c> or <c>?</c> (<c>/project/view</c>).</param>
    /// <param name="id">The spec's id.</param>
    /// <param name="title">The spec's title.</param>
    /// <param name="results">A results file of a run of the spec's scenarios: a rule whose scenario failed is left out. Null for none.</param>
    /// <returns>The draft, and the file it belongs in: the specs folder beside the pages' folder.</returns>
    /// <exception cref="ScraperProblem">The suite's templates do not say how a spec is written for the address, or no drafted page of it is reached by a tab.</exception>
    public (SpecDraft Draft, string File) DraftSpec(string? projectName, PlacesList list, string address, string id, string title, string? results = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        var templatesFile = Path.Combine(project.Home, DraftTemplates.FileName);
        var templates = File.Exists(templatesFile) ? DraftTemplates.Parse(File.ReadAllText(templatesFile)) : throw new ScraperProblem($"{project.Name} has no {DraftTemplates.FileName}.");
        if (!SpecDrafter.CanDraft(templates, address))
            throw new ScraperProblem($"{DraftTemplates.FileName} does not say how a spec is written for {address}: it needs '## {SpecDrafter.Head}', '## {SpecDrafter.Opening}{address}', '## {SpecDrafter.PageSteps}', '## {SpecDrafter.ButtonStep}' and '## {SpecDrafter.PageRule}'.");

        var pages = new List<SpecPage>();
        foreach (var (place, snapshot) in KeptPlaces(project.Name, list).Where(each => each.Place.Kind == PlaceKind.Page).OrderBy(each => each.Place.Key, StringComparer.Ordinal))
        {
            if (PlaceDrafter.FamilyOf(place, list.Places, templates) is not { } family || family.Path != address || DraftedFrom(project, place.Key, "form") is not { } className)
                continue;
            // The tab that shows the page: the one its capture has selected, of the tabs its address shares.
            var selected = PageFacts.Of(snapshot, project.ControlSigns, project.PlaceSigns).Tabs.Where(tab => tab.EndsWith(" *", StringComparison.Ordinal)).Select(tab => Place.Clean(tab[..^2]));
            if (selected.FirstOrDefault(tab => family.Tabs.Contains(tab, StringComparer.Ordinal)) is not { } shownBy)
                continue;
            var (opens, under) = OpensOn(list, place);
            var form = PlaceDrafter.Form(place, templates, className, "", null, opens, project.PlaceSigns, under, family);
            var buttons = form.Members.Where(member => form.Pressed.Contains(member.Name) && place.Buttons.Any(button => button == member.Label || button == member.Label + " (disabled)")).Select(member => member.Label).ToList();
            pages.Add(new SpecPage(place.Key, shownBy, SpecDrafter.PageName(className), buttons));
        }

        if (pages.Count == 0)
            throw new ScraperProblem($"No drafted page at {address} is shown by a tab its address shares: there is nothing to draft a spec from.");
        var (folder, _) = project.PagesFor(pages[0].Place);
        var specs = Path.Combine(project.Folder, Path.GetDirectoryName(folder.TrimEnd('/', '\\')) ?? "", "Specs");
        var file = Path.Combine(specs, id + ".spec.md");
        // What the suite's other specs say already is not said again.
        var existing = Directory.EnumerateFiles(project.Folder, "*.spec.md", SearchOption.AllDirectories)
            .Where(each => !string.Equals(Path.GetFullPath(each), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase) && !each.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(each => SpecDrafter.StepsOf(File.ReadAllText(each))).ToHashSet(StringComparer.Ordinal);
        return (SpecDrafter.Draft(id, title, address, pages, templates, existing, results is null ? null : FailedPlaces(results, id, pages, templates, existing)), file);
    }

    // The pages whose scenario failed in a run: a scenario is named after its rule (B7.PRJ.04.R3 …), and a rule's
    // number is its page's place in the draft those scenarios were run from.
    private static Dictionary<string, string> FailedPlaces(string results, string id, IReadOnlyList<SpecPage> pages, DraftTemplates templates, IReadOnlySet<string> existing)
    {
        var before = SpecDrafter.Draft(id, "x", pages.Count == 0 ? "" : PlaceDrafterFamilyPath(pages[0].Place), pages, templates, existing);
        var failed = new Dictionary<string, string>(StringComparer.Ordinal);
        var ruled = pages.Where(page => !before.LeftOut.Any(left => left.StartsWith(page.Place + ":", StringComparison.Ordinal))).ToList();
        foreach (var outcome in Testing.TestRunner.ReadTrx(File.ReadAllText(results)).Where(outcome => !outcome.Passed))
        {
            var at = outcome.Name.IndexOf(id + ".R", StringComparison.Ordinal);
            if (at < 0)
                continue;
            var digits = new string([.. outcome.Name[(at + id.Length + 2)..].TakeWhile(char.IsAsciiDigit)]);
            if (!int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) || number < 1 || number > ruled.Count)
                continue;
            var said = string.Join(' ', (outcome.Message ?? "failed").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            failed[ruled[number - 1].Place] = said.Length > 200 ? said[..200] + "…" : said;
        }

        return failed;
    }

    private static string PlaceDrafterFamilyPath(string key)
    {
        var cut = key.IndexOfAny(['#', '?']);
        return cut < 0 ? key : key[..cut];
    }

    private sealed record DraftedFile(string Key, string File, ScrapedHeader Header);

    private static IEnumerable<DraftedFile> DraftedFiles(Bruinet.Scraper.Projects.ScrapeProject project) =>
        Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(file => (File: file, Header: ScrapedHeader.Find(File.ReadAllText(file))))
            .Where(each => each.Header is { Page: not null, Variant: "form" or "row" or "tabspart" })
            .Select(each => new DraftedFile(PageDrafter.ClassOf(each.File), each.File, each.Header!))
            .DistinctBy(each => each.Key, StringComparer.Ordinal);

    /// <summary>
    /// Reads every kept place again with the signs, holders and components the project names now, drafts again every
    /// class that was drafted from a place, and says what differs from the list and the suite as they are
    /// (<see cref="ReplayReport"/>). Nothing is written and the app is not opened.
    /// </summary>
    public ReplayReport ReplayPlaces(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var clock = Stopwatch.StartNew();
        var project = Project(projectName);
        var code = ProjectCode.Read(project);
        var (fresh, fields, kept) = ReadAgain(project, list, code);

        var changed = new List<ClassChange>();
        var classes = 0;
        // Every file that starts with the Scraper's line for a form or a row, whatever its class is built on: a dialog
        // on a control library's base is drafted as much as a page is.
        foreach (var group in DraftedFiles(project))
        {
            var drafted = group;
            classes++;
            var key = drafted.Header!.Page!;
            if (fresh[key] is null)
            {
                changed.Add(new ClassChange(group.Key, key, [], [], "the list has no such place any more"));
                continue;
            }

            try
            {
                // A shared tabs part is drafted from a page of its address: any of them gives the same part.
                var page = drafted.Header.Variant != "tabspart" ? key
                    : fresh.Places.Where(each => each.Kind == PlaceKind.Page && each.Tabs.Count > 0 && (each.Key == key || each.Key.StartsWith(key + "#", StringComparison.Ordinal) || each.Key.StartsWith(key + "?", StringComparison.Ordinal))).Select(each => each.Key).Order(StringComparer.Ordinal).FirstOrDefault()
                    ?? throw new ScraperProblem($"no page at {key} shows tabs any more");
                var (draft, _) = drafted.Header.Variant == "tabspart"
                    ? DraftPlace(project.Name, fresh, page, PlaceDraftKind.TabsPart)
                    : DraftPlace(project.Name, fresh, key, drafted.Header.Variant == "row" ? PlaceDraftKind.Row : PlaceDraftKind.Form, group.Key);
                var (then, today) = (Lines(File.ReadAllText(drafted.File)), Lines(draft.Code));
                if (then == today)
                    continue;
                // Both read the same way: a template's own members are in the code, not in the draft's list of members.
                var (was, now) = (MemberNames(then), MemberNames(today));
                var added = now.Except(was, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                var removed = was.Except(now, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                changed.Add(new ClassChange(group.Key, key, added, removed, added.Count + removed.Count == 0 ? $"the same members, written differently: {FirstDifference(then, today)}" : ""));
            }
            catch (ScraperProblem problem)
            {
                changed.Add(new ClassChange(group.Key, key, [], [], problem.Message));
            }
        }

        var owed = Owed.Of(fresh, SuiteSource.Read(project), project.PlaceSigns);
        var wouldDraft = 0;
        var cannot = new List<NotDrafted>();
        foreach (var key in owed.Groups.SelectMany(group => group.PagesWithoutClass.Concat(group.DialogFormsNotDrafted)))
        {
            try
            {
                _ = DraftPlace(project.Name, fresh, key, PlaceDraftKind.Form);
                wouldDraft++;
            }
            catch (ScraperProblem problem)
            {
                cannot.Add(new NotDrafted(key, problem.Message));
            }
        }

        return new ReplayReport(kept.Count, classes, fields, changed, wouldDraft, cannot, Math.Round(clock.Elapsed.TotalSeconds, 1));
    }

    /// <summary>
    /// Holds each example of the project's <c>## Examples</c> (a place and the class a person wrote for it) against
    /// what the place reads as now, and says for each difference who is right (<see cref="BenchVerdict"/>).
    /// </summary>
    public BenchReport BenchPlaces(string? projectName, PlacesList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var project = Project(projectName);
        var code = ProjectCode.Read(project);
        var (fresh, _, kept) = ReadAgain(project, list, code);
        var captures = kept.ToDictionary(each => each.Place.Key, each => each.Snapshot, StringComparer.Ordinal);
        var differences = new List<BenchDifference>();
        var problems = new List<string>();
        var sources = project.Config.Examples.Count == 0 ? [] : Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetRelativePath(project.Folder, file).Replace('\\', '/') is var relative && !relative.StartsWith("bin/", StringComparison.Ordinal) && !relative.StartsWith("obj/", StringComparison.Ordinal))
            .Select(file => (File: file, Text: File.ReadAllText(file))).ToList();
        foreach (var example in project.Config.Examples)
        {
            // Any class of the suite may be an example, a dialog too: found by its declaration, not by what it derives from.
            var parts = sources.Where(source => source.Text.Contains(example.Class, StringComparison.Ordinal)).Select(source => ClassText(source.Text, example.Class)).Where(text => text.Length > 0).ToList();
            if (fresh[example.Place] is not { } place)
            {
                problems.Add($"{example.Class}: the list has no place '{example.Place}'.");
                continue;
            }

            if (parts.Count == 0)
            {
                problems.Add($"{example.Class}: the project has no such class.");
                continue;
            }

            var (notOnPlace, notInClass) = PlaceDrafter.Drift(place, string.Join('\n', parts), example.Row ? PlaceDraftKind.Row : PlaceDraftKind.Form, project.PlaceSigns);
            differences.AddRange(notInClass.Select(label => new BenchDifference(example.Class, example.Place, label, BenchVerdict.DraftIsRight)));
            differences.AddRange(notOnPlace.Select(label => new BenchDifference(example.Class, example.Place, label,
                captures.TryGetValue(example.Place, out var capture) && Shows(capture, label) ? BenchVerdict.ExampleIsRight : BenchVerdict.NotOnThePage)));
        }

        return new BenchReport(project.Config.Examples.Count, differences, problems);
    }

    // The list with every kept place read again, the fields that read differently, and the kept places with their captures.
    private (PlacesList Fresh, List<FieldChange> Fields, IReadOnlyList<(Place Place, ScrapeSnapshot Snapshot)> Kept) ReadAgain(Projects.ScrapeProject project, PlacesList list, IReadOnlyList<CodeClass> code)
    {
        var fresh = new PlacesList();
        foreach (var place in list.Places)
            fresh.Set(place);
        var fields = new List<FieldChange>();
        var kept = KeptPlaces(project.Name, list);
        foreach (var (place, snapshot) in kept)
        {
            var read = PlacesReading.Read(place, snapshot, project.ControlSigns, project.Config.Components, code, project.PlaceSigns);
            fresh.Set(read);
            fields.AddRange(FieldChanges(place, read));
        }

        return (fresh, fields, kept);
    }

    // Field by field, by label and by which of that label it is: a form may show two fields of one label.
    private static IEnumerable<FieldChange> FieldChanges(Place before, Place after)
    {
        if (before.Fields.SequenceEqual(after.Fields, StringComparer.Ordinal))
            yield break;
        var was = Numbered(before.Fields);
        var now = Numbered(after.Fields);
        foreach (var key in was.Keys.Concat(now.Keys).Distinct())
        {
            var (old, read) = (was.GetValueOrDefault(key), now.GetValueOrDefault(key));
            if (old != read)
                yield return new FieldChange(before.Key, key.Label, old, read);
        }
    }

    private static Dictionary<(string Label, int Nth), string> Numbered(IReadOnlyList<string> fields)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var numbered = new Dictionary<(string, int), string>();
        foreach (var field in fields)
        {
            var label = PlaceField.Parse(field).Label;
            var nth = seen[label] = seen.GetValueOrDefault(label) + 1;
            numbered[(label, nth)] = field;
        }

        return numbered;
    }

    // What is on the screen says the label somewhere: as an element's name or as its text.
    private static bool Shows(ScrapeSnapshot capture, string label) =>
        capture.Nodes.Any(node => node.Bounds is { Width: > 0, Height: > 0 }
            && (string.Equals(node.Name?.Trim(), label, StringComparison.OrdinalIgnoreCase) || string.Equals(node.Text?.Trim(), label, StringComparison.OrdinalIgnoreCase)));

    // A class's own code in a file that may hold several: every part of it the file declares; empty when it declares none.
    private static string ClassText(string code, string name)
    {
        var root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code).GetRoot();
        return string.Join('\n', root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText == name).Select(type => type.ToFullString()));
    }

    // The public members a class's code declares: a property or a method, by its name.
    private static HashSet<string> MemberNames(string code) =>
        MemberDeclaration().Matches(code).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s+public\s+(?!sealed|partial|class|record|static\s+class)[^=;(]*?\b(\w+)\s*(?:=>|\{|\()", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex MemberDeclaration();

    // The first line that differs, as a draft writes it now.
    private static string FirstDifference(string then, string today)
    {
        var (old, draft) = (then.Split('\n'), today.Split('\n'));
        for (var at = 0; at < Math.Max(old.Length, draft.Length); at++)
        {
            if (at >= old.Length || at >= draft.Length || old[at] != draft[at])
                return at < draft.Length ? $"line {at + 1} is now '{draft[at].Trim()}'" : $"line {at + 1} is gone";
        }

        return "";
    }

    private static string Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
}
