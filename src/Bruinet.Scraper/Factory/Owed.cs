using Bruinet.Scraper.Code;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Factory;

/// <summary>What one group of places still owes: the one answer to "what is left".</summary>
/// <param name="Group">The group: the first part of its pages' addresses (<c>/project</c>).</param>
/// <param name="Kinds">The kinds of its pages and dialogs, each once.</param>
/// <param name="Pages">How many pages it has.</param>
/// <param name="PagesWithoutClass">Pages that open by their address and that no class names.</param>
/// <param name="DialogFormsNotDrafted">Dialogs that hold a form no class was drafted from.</param>
/// <param name="RecordPages">Pages of one record that no class names: each needs a record to open.</param>
/// <param name="FieldsWithoutControl">Fields no made control stands for.</param>
/// <param name="FormsNotSaved">Forms that were read and never saved by a test.</param>
public sealed record OwedGroup(string Group, IReadOnlyList<PlaceGroupKind> Kinds, int Pages, IReadOnlyList<string> PagesWithoutClass, IReadOnlyList<string> DialogFormsNotDrafted, IReadOnlyList<string> RecordPages, int FieldsWithoutControl, int FormsNotSaved)
{
    /// <summary>What a loop can take up now: pages and dialog forms that need no record first.</summary>
    public int Count => PagesWithoutClass.Count + DialogFormsNotDrafted.Count;
}

/// <summary>What the places of an app still owe, by group, and added up.</summary>
/// <param name="Places">How many places the list holds.</param>
/// <param name="Kept">How many of them have a kept capture.</param>
/// <param name="Fields">How many fields the forms have, a form shown by two buttons counted once.</param>
/// <param name="FieldsWithControl">How many of those a made control stands for.</param>
/// <param name="Groups">Each group, the one that owes most first.</param>
public sealed record OwedReport(int Places, int Kept, int Fields, int FieldsWithControl, IReadOnlyList<OwedGroup> Groups)
{
    /// <summary>Pages that open and have no class, over all groups.</summary>
    public int PagesWithoutClass => Groups.Sum(group => group.PagesWithoutClass.Count);

    /// <summary>Dialog forms no class was drafted from, over all groups.</summary>
    public int DialogFormsNotDrafted => Groups.Sum(group => group.DialogFormsNotDrafted.Count);

    /// <summary>Pages of one record without a class, over all groups.</summary>
    public int RecordPages => Groups.Sum(group => group.RecordPages.Count);

    /// <summary>Forms read and never saved, over all groups.</summary>
    public int FormsNotSaved => Groups.Sum(group => group.FormsNotSaved);
}

/// <summary>What a suite's source says about the places: which addresses a class names, and which places a class was drafted from.</summary>
/// <param name="Text">Every C# file of the suite, one after another.</param>
/// <param name="DraftedFrom">The places a class was drafted from, by the first line of its file.</param>
public sealed record SuiteSource(string Text, IReadOnlySet<string> DraftedFrom)
{
    /// <summary>Reads a project's C# files, build output left out.</summary>
    public static SuiteSource Read(ScrapeProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var text = new System.Text.StringBuilder();
        var drafted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
                continue;
            var code = File.ReadAllText(file);
            text.Append(code).Append('\n');
            if (ScrapedHeader.Find(code)?.Page is { } page)
                drafted.Add(page);
        }

        return new SuiteSource(text.ToString(), drafted);
    }

    /// <summary>True when a class names the address in quotes, as a page that opens itself does.</summary>
    public bool Names(string address) => Text.Contains($"\"{address}\"", StringComparison.Ordinal);
}

/// <summary>Counts what the places of an app still owe (<see cref="OwedReport"/>).</summary>
public static class Owed
{
    /// <summary>What every group of the list owes, held against the suite's source.</summary>
    public static OwedReport Of(PlacesList list, SuiteSource suite)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(suite);
        var groups = new List<OwedGroup>();
        foreach (var group in list.Places.GroupBy(PlaceKinds.GroupOf, StringComparer.Ordinal))
        {
            var pages = group.Where(place => place.Kind == PlaceKind.Page).ToList();
            if (pages.Count == 0)
                continue;
            var keys = pages.Select(page => page.Key).ToHashSet(StringComparer.Ordinal);
            var withoutClass = pages.Where(page => page.Address is not null && !PlaceKinds.IsRecord(page) && page.State is PlaceState.Seen or PlaceState.FormNotSaved
                    && !suite.Names(page.Key) && !suite.DraftedFrom.Contains(page.Key))
                .Select(page => page.Key).ToList();
            var records = pages.Where(page => PlaceKinds.IsRecord(page) && !suite.Names(page.Key[..page.Key.IndexOf('{', StringComparison.Ordinal)].TrimEnd('/')))
                .Select(page => page.Key).ToList();
            var forms = group.Where(place => place.Kind == PlaceKind.Dialog && place.State == PlaceState.FormNotSaved && place.SameAs is null
                    && keys.Contains(place.Parent ?? place.Key) && !suite.DraftedFrom.Contains(place.Key))
                .Select(place => place.Key).ToList();
            var fields = group.Where(place => place.SameAs is null).SelectMany(place => place.Fields).Select(PlaceField.Parse).ToList();
            groups.Add(new OwedGroup(
                group.Key,
                [.. group.Where(place => place.Kind is PlaceKind.Page or PlaceKind.Dialog).Select(place => PlaceKinds.Of(place, list)).Distinct().Order()],
                pages.Count,
                withoutClass,
                forms,
                records,
                fields.Count(field => !field.HasControl),
                group.Count(place => place.State == PlaceState.FormNotSaved && place.SameAs is null)));
        }

        var all = list.Places.Where(place => place.SameAs is null).SelectMany(place => place.Fields).Select(PlaceField.Parse).ToList();
        return new OwedReport(
            list.Places.Count,
            list.Places.Count(place => place.Snapshot is not null),
            all.Count,
            all.Count(field => field.HasControl),
            [.. groups.OrderByDescending(group => group.Count).ThenBy(group => group.Group, StringComparer.Ordinal)]);
    }
}
