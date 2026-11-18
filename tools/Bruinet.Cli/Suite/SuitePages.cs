using System.Text.RegularExpressions;

namespace Bruinet.Cli.Suite;

/// <summary>A page object of a suite and what its source shows of it: a row of the checklist.</summary>
/// <param name="Module">The folder under <c>Modules/</c>; empty for a page outside the modules.</param>
/// <param name="Subject">The folder under <c>Pages/</c>; empty when the page is in <c>Pages/</c> itself.</param>
/// <param name="Page">The class.</param>
/// <param name="PageName">The name a spec uses (<c>[BddPage]</c>); null when it has none.</param>
/// <param name="File">The file the class is in, from the suite's folder.</param>
/// <param name="Kinds"><c>list</c>, <c>form</c>, both, or <c>view</c>.</param>
/// <param name="Address">Where it opens; null when a test reaches it another way.</param>
/// <param name="Fields">Members that are fields, its own, inherited and of its parts.</param>
/// <param name="Buttons">Members that are buttons.</param>
/// <param name="Tabs">Members that are tabs.</param>
/// <param name="Rows">Each collection's row type with its number of columns.</param>
/// <param name="ByText">Members whose phrase takes a text.</param>
/// <param name="Specs">The specs that name the page.</param>
/// <param name="Tests">The test classes that name the class.</param>
/// <param name="InMap">The map leads to its address.</param>
/// <param name="Gaps">What the source shows is missing.</param>
internal sealed record PageFacts(
    string Module,
    string Subject,
    string Page,
    string? PageName,
    string File,
    IReadOnlyList<string> Kinds,
    string? Address,
    int Fields,
    int Buttons,
    int Tabs,
    IReadOnlyList<string> Rows,
    int ByText,
    IReadOnlyList<string> Specs,
    IReadOnlyList<string> Tests,
    bool InMap,
    IReadOnlyList<string> Gaps);

/// <summary>An address of the map and what its row says before it.</summary>
/// <param name="Address">The address.</param>
/// <param name="Row">The row's cells before the address, joined.</param>
internal sealed record MapAddress(string Address, string Row);

/// <summary>A suite's pages, and the map's addresses none of them opens at.</summary>
/// <param name="Pages">The pages, by module, subject and name.</param>
/// <param name="Unmade">The map's rows without a page.</param>
/// <param name="MapRows">How many rows of the map have an address.</param>
internal sealed record SuitePages(IReadOnlyList<PageFacts> Pages, IReadOnlyList<MapAddress> Unmade, int MapRows)
{
    /// <summary>What the addresses were read from, as the checklist names it: the map, or the list of places.</summary>
    public string AddressesFrom { get; init; } = "the map";

    private const int PartDepth = 4;

    // Bruinet's plain controls carry no [BddKind] yet: known by name.
    private static readonly Dictionary<string, string> PlainKinds = new(StringComparer.Ordinal)
    {
        ["ButtonControl"] = "button",
        ["LinkControl"] = "button",
        ["TextInputControl"] = "field",
        ["TextAreaControl"] = "field",
        ["SelectControl"] = "field",
        ["CheckBoxControl"] = "field",
        ["RadioButtonControl"] = "field",
    };

    /// <summary>Reads a suite's pages from its folder.</summary>
    /// <param name="suite">The suite's folder: every project under it is read.</param>
    /// <param name="map">A Markdown file whose tables hold the app's addresses in backticks; null for none.</param>
    /// <param name="mapText">The map's own text, in place of the file's; null to read the file.</param>
    public static SuitePages Read(string suite, string? map, string? mapText = null)
    {
        var files = Directory.EnumerateFiles(suite, "*", SearchOption.AllDirectories)
            .Select(file => (Path: Path.GetRelativePath(suite, file).Replace('\\', '/'), Full: file))
            .Where(file => !file.Path.Split('/').Any(part => part is "bin" or "obj" or "artifacts" or "node_modules"))
            .ToList();
        var source = new SuiteSource();
        var tests = new Dictionary<string, string>(StringComparer.Ordinal);
        var specs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, full) in files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            if (path.EndsWith(".cs", StringComparison.Ordinal))
            {
                var text = File.ReadAllText(full);
                source.Add(path, text);
                if (path.Contains("/Tests/", StringComparison.Ordinal))
                    tests[Path.GetFileNameWithoutExtension(path)] = text;
            }
            else if (path.EndsWith(".spec.md", StringComparison.Ordinal))
            {
                specs[Path.GetFileName(path)[..^".spec.md".Length]] = File.ReadAllText(full);
            }
        }

        return From(source, specs, tests, mapText ?? (map is null ? null : File.ReadAllText(map)));
    }

    /// <summary>The pages of a suite's source.</summary>
    /// <param name="source">The suite's types.</param>
    /// <param name="specs">Each spec's text, by its id.</param>
    /// <param name="tests">Each test file's text, by its name.</param>
    /// <param name="map">The map's Markdown; null for none.</param>
    public static SuitePages From(SuiteSource source, IReadOnlyDictionary<string, string> specs, IReadOnlyDictionary<string, string> tests, string? map)
    {
        var mapRows = RowsOf(map).ToList();
        var mapped = mapRows.SelectMany(row => row.Addresses).Select(Normal).ToHashSet(StringComparer.Ordinal);
        var testWords = tests.ToDictionary(test => test.Key, test => Word().Matches(test.Value).Select(match => match.Value).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var pages = new List<PageFacts>();
        foreach (var type in source.Types.Values.Where(type => !type.IsBase && source.Chain(type.Name).Contains("PageObjectBase", StringComparer.Ordinal)))
        {
            var module = Segment(type.File, "Modules");
            var count = new Count();
            Walk(source, type.Name, module, PartDepth, new HashSet<string>(StringComparer.Ordinal), count);
            var address = source.Chain(type.Name).Where(source.Types.ContainsKey).Select(name => source.Types[name].Address).FirstOrDefault(found => found is not null);
            var named = specs.Where(spec => type.PageName is { Length: > 0 } name && spec.Value.Contains($"the {name} page", StringComparison.Ordinal))
                .Select(spec => spec.Key).Order(StringComparer.Ordinal).ToList();
            var used = testWords.Where(test => test.Value.Contains(type.Name)).Select(test => test.Key).Order(StringComparer.Ordinal).ToList();
            var inMap = address is not null && mapped.Contains(Normal(address));
            var kinds = new List<string>();
            if (count.Rows.Count > 0 || count.Table)
                kinds.Add("list");
            if (count.Fields > 0)
                kinds.Add("form");
            if (kinds.Count == 0)
                kinds.Add("view");
            var gaps = new List<string>();
            if (count.ByText > 0)
                gaps.Add($"by text ({count.ByText})");
            if (count.Table && count.Rows.Count == 0)
                gaps.Add("no typed row");
            if (count.Fields + count.Buttons + count.Tabs + count.Rows.Count == 0)
                gaps.Add("nothing named");
            if (named.Count == 0 && used.Count == 0 && !inMap)
                gaps.Add("no spec, no test");
            pages.Add(new PageFacts(module, Segment(type.File, "Pages", folderOnly: true), type.Name, type.PageName is { Length: > 0 } pageName ? pageName : null, type.File,
                kinds, address, count.Fields, count.Buttons, count.Tabs, count.Rows, count.ByText, named, used, inMap, gaps));
        }

        var opened = pages.Where(page => page.Address is not null).Select(page => Normal(page.Address!)).ToHashSet(StringComparer.Ordinal);
        var unmade = mapRows.Where(row => !row.Addresses.Any(address => opened.Contains(Normal(address))))
            .Select(row => new MapAddress(row.Addresses[0], row.Before)).ToList();
        return new SuitePages(
            [.. pages.OrderBy(page => page.Module, StringComparer.Ordinal).ThenBy(page => page.Subject, StringComparer.Ordinal).ThenBy(page => page.Page, StringComparer.Ordinal)],
            unmade,
            mapRows.Count);
    }

    // What a page's members are, with those of its parts in the same module.
    private static void Walk(SuiteSource source, string type, string module, int depth, HashSet<string> seen, Count count)
    {
        if (depth == 0 || !seen.Add(type))
            return;
        foreach (var member in source.AllMembers(type))
        {
            if (member.Phrase?.Contains('{', StringComparison.Ordinal) == true)
                count.ByText++;
            if (member.IsMethod)
                continue;
            var chain = source.Chain(member.Type).ToList();
            var kind = chain.Where(source.Types.ContainsKey).Select(name => source.Types[name].Kind).FirstOrDefault(found => found is { Length: > 0 })
                ?? chain.Select(name => PlainKinds.GetValueOrDefault(name)).FirstOrDefault(found => found is not null);
            if (chain.Contains("CollectionObjectBase", StringComparer.Ordinal))
            {
                var row = member.TypeArguments.FirstOrDefault(argument => source.Chain(argument).Contains("ItemObjectBase", StringComparer.Ordinal));
                if (row is not null && source.Types.TryGetValue(row, out var rowType))
                    count.Rows.Add($"{row} ({rowType.Members.Count(column => !column.IsMethod)})");
                else
                    count.Table = true;
                continue;
            }

            switch (kind)
            {
                case "field":
                    count.Fields++;
                    continue;
                case "button":
                    count.Buttons++;
                    continue;
                case "tab":
                    count.Tabs++;
                    continue;
                case not null:
                    continue;
            }

            if (member.Type.EndsWith("Table", StringComparison.Ordinal) || member.Type.EndsWith("TableControl", StringComparison.Ordinal))
            {
                count.Table = true;
                continue;
            }

            // A part: a class of the page's own module that is neither a page nor a control of a kind.
            if (module.Length > 0 && source.Types.TryGetValue(member.Type, out var part) && Segment(part.File, "Modules") == module
                && !chain.Contains("PageObjectBase", StringComparer.Ordinal))
                Walk(source, member.Type, module, depth - 1, seen, count);
        }
    }

    // The path's part after a folder; with folderOnly, only when that part is a folder itself.
    private static string Segment(string file, string folder, bool folderOnly = false)
    {
        var parts = file.Split('/');
        var at = Array.IndexOf(parts, folder);
        if (at < 0 || at + 1 >= parts.Length)
            return string.Empty;
        return folderOnly && at + 2 >= parts.Length ? string.Empty : parts[at + 1];
    }

    // An address as two spellings of one place compare: without the slash and the hash it ends with.
    private static string Normal(string address)
    {
        var normal = address.TrimEnd('/');
        if (normal.EndsWith('#'))
            normal = normal[..^1].TrimEnd('/');
        return normal.Length == 0 ? "/" : normal;
    }

    private static IEnumerable<(IReadOnlyList<string> Addresses, string Before)> RowsOf(string? map)
    {
        if (map is null)
            yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in map.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('|')))
        {
            var cells = line.Trim('|').Split('|').Select(cell => cell.Trim()).ToList();
            var addresses = cells.Select(cell => Address().Match(cell)).Where(match => match.Success).Select(match => match.Groups[1].Value).ToList();
            if (addresses.Count == 0 || !seen.Add(addresses[0]))
                continue;
            var first = cells.FindIndex(cell => Address().IsMatch(cell));
            yield return (addresses, string.Join(" / ", cells.Take(first).Where(cell => cell.Length > 0)));
        }
    }

    private sealed class Count
    {
        public int Fields { get; set; }

        public int Buttons { get; set; }

        public int Tabs { get; set; }

        public int ByText { get; set; }

        public bool Table { get; set; }

        public List<string> Rows { get; } = [];
    }

    private static readonly Regex WordPattern = new(@"[A-Za-z_]\w*", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AddressPattern = new(@"^`(/[^`]*)`$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static Regex Word() => WordPattern;

    private static Regex Address() => AddressPattern;
}
