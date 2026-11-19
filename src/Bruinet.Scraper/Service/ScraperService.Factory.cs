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
        foreach (var group in code.Where(each => File.Exists(each.File)).GroupBy(each => each.Name.Split('<')[0], StringComparer.Ordinal))
        {
            var drafted = group.FirstOrDefault(each => each.Header?.Page is not null && each.Header.Variant is "form" or "row");
            if (drafted is null)
                continue;
            classes++;
            var key = drafted.Header!.Page!;
            if (fresh[key] is null)
            {
                changed.Add(new ClassChange(group.Key, key, [], [], "the list has no such place any more"));
                continue;
            }

            try
            {
                var (draft, _) = DraftPlace(project.Name, fresh, key, drafted.Header.Variant == "row" ? PlaceDraftKind.Row : PlaceDraftKind.Form, group.Key);
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
