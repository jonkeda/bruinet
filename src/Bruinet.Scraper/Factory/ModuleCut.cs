using System.Globalization;
using System.Text;
using Bruinet.Core.Utilities;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Factory;

/// <summary>What a person says of a module in <c>modules.md</c>.</summary>
public enum ModuleDo
{
    /// <summary>It runs, in its order.</summary>
    Yes,

    /// <summary>It waits: after everything that says yes.</summary>
    Later,

    /// <summary>It is left out.</summary>
    No,
}

/// <summary>One module of an app: a part with a subject of its own, cut into the groups the factory takes one at a time.</summary>
/// <param name="Name">The module's name.</param>
/// <param name="Order">Where it comes: 1 first.</param>
/// <param name="Do">Whether it runs.</param>
/// <param name="Groups">Its groups, by the first part of their pages' addresses.</param>
/// <param name="Pages">How many pages are known in it.</param>
/// <param name="Kinds">The kinds of its pages and dialogs, each once.</param>
/// <param name="Owed">How many pages, dialog forms and record pages still have no class.</param>
public sealed record AppModule(string Name, int Order, ModuleDo Do, IReadOnlyList<string> Groups, int Pages, IReadOnlyList<PlaceGroupKind> Kinds, int Owed);

/// <summary>
/// Cuts an app into modules from what is known of it without walking it: the list of places as the map, the app's
/// routes and the suite filled it, and the project's <c>## Modules</c>. A person may reorder the modules and leave
/// some out (<c>modules.md</c>); what they wrote is kept when the cut is made again.
/// </summary>
public static class ModuleCut
{
    /// <summary>The file's name, in the app's own Scraper place.</summary>
    public const string FileName = "modules.md";

    private const string Section = "Modules";

    /// <summary>The modules of a list, in the tool's own order: plain kinds before hard ones, small before large.</summary>
    /// <param name="list">The places.</param>
    /// <param name="config">The project's config: its <c>## Modules</c> name a group's module.</param>
    /// <param name="owed">What the groups still owe.</param>
    public static IReadOnlyList<AppModule> Of(PlacesList list, ScraperConfig config, OwedReport owed)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(owed);
        var modules = owed.Groups.GroupBy(group => NameOf(group.Group, config), StringComparer.Ordinal)
            .Select(module => (
                Name: module.Key,
                Groups: module.Select(group => group.Group).Order(StringComparer.Ordinal).ToList(),
                Pages: module.Sum(group => group.Pages),
                Kinds: module.SelectMany(group => group.Kinds).Distinct().Order().ToList(),
                Owed: module.Sum(group => group.Count + group.RecordPages.Count)))
            .OrderBy(module => module.Kinds.Count == 0 ? 0 : module.Kinds.Max(Hardness)).ThenBy(module => module.Pages).ThenBy(module => module.Name, StringComparer.Ordinal)
            .ToList();
        return [.. modules.Select((module, at) => new AppModule(module.Name, at + 1, ModuleDo.Yes, module.Groups, module.Pages, module.Kinds, module.Owed))];
    }

    /// <summary>
    /// A fresh cut with what a person wrote before kept: a module's order and whether it runs. A module that is new
    /// comes after those the person ordered.
    /// </summary>
    public static IReadOnlyList<AppModule> Keep(IReadOnlyList<AppModule> fresh, IReadOnlyList<AppModule> written)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(written);
        var before = written.ToDictionary(module => module.Name, StringComparer.Ordinal);
        var last = written.Count == 0 ? 0 : written.Max(module => module.Order);
        var ordered = fresh.Select(module => before.TryGetValue(module.Name, out var was) ? module with { Order = was.Order, Do = was.Do } : module with { Order = last + module.Order })
            .OrderBy(module => module.Order).ThenBy(module => module.Name, StringComparer.Ordinal).ToList();
        return [.. ordered.Select((module, at) => module with { Order = at + 1 })];
    }

    /// <summary>The modules as <c>modules.md</c>.</summary>
    public static string ToMarkdown(IReadOnlyList<AppModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var text = new StringBuilder();
        text.Append("# Modules\n\nThe app cut into modules, from the list of places and without walking it. `Order` and `Do` are a person's to\nchange: `yes` runs in its order, `later` after everything that says yes, `no` is left out. The other columns are\nwritten again by `bruinet scrape scraper modules --write`, which keeps `Order` and `Do`.\n\n## Modules\n\n");
        text.Append("| Order | Module | Do | Groups | Pages known | Kinds | Owed |\n| --- | --- | --- | --- | --- | --- | --- |\n");
        foreach (var module in modules.OrderBy(module => module.Order))
        {
            text.Append(CultureInfo.InvariantCulture, $"| {module.Order} | {module.Name} | {module.Do.ToString().ToLowerInvariant()} | {string.Join(", ", module.Groups.Select(group => $"`{group}`"))} | {module.Pages} | {string.Join(", ", module.Kinds)} | {module.Owed} |\n");
        }

        return text.ToString();
    }

    /// <summary>What a person wrote in a <c>modules.md</c>: each module's order and whether it runs. The other columns are not read.</summary>
    /// <exception cref="FormatException">An order is not a number, or a Do not yes, later or no.</exception>
    public static IReadOnlyList<AppModule> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (!MarkdownTables.ReadSections(markdown).TryGetValue(Section, out var section))
            return [];
        var modules = new List<AppModule>();
        foreach (var row in section.Rows)
        {
            var name = row.Cells.GetValueOrDefault("Module", "").Trim();
            if (name.Length == 0)
                continue;
            if (!int.TryParse(row.Cells.GetValueOrDefault("Order", "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var order))
                throw new FormatException($"Line {row.LineNumber}: Order is a number, not '{row.Cells.GetValueOrDefault("Order", "")}'.");
            var said = row.Cells.GetValueOrDefault("Do", "yes").Trim().ToLowerInvariant();
            var does = said switch
            {
                "yes" or "" => ModuleDo.Yes,
                "later" => ModuleDo.Later,
                "no" => ModuleDo.No,
                _ => throw new FormatException($"Line {row.LineNumber}: Do is yes, later or no, not '{said}'."),
            };
            modules.Add(new AppModule(name, order, does, [], 0, [], 0));
        }

        return modules;
    }

    /// <summary>The modules that run, in the order they run: those that say yes by their order, then those that say later.</summary>
    public static IReadOnlyList<AppModule> ToRun(IReadOnlyList<AppModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return [.. modules.Where(module => module.Do != ModuleDo.No).OrderBy(module => module.Do).ThenBy(module => module.Order)];
    }

    /// <summary>
    /// The module a group is of: the one the project's <c>## Modules</c> draft its pages into (<c>Modules/Invoices/Pages</c>
    /// is Invoices), else the group's own name (<c>/day-off</c> is Day off).
    /// </summary>
    public static string NameOf(string group, ScraperConfig config)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(config);
        if (config.Modules.FirstOrDefault(module => group.StartsWith(module.Path, StringComparison.OrdinalIgnoreCase) || module.Path.StartsWith(group + "/", StringComparison.OrdinalIgnoreCase) || module.Path.StartsWith(group + "#", StringComparison.OrdinalIgnoreCase)) is { } named)
        {
            var folders = named.Pages.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            // The folder above the pages' folder is the module's: Modules/Invoices/Pages, and Modules/MasterData/Pages/Materials.
            var at = Array.FindIndex(folders, folder => string.Equals(folder, "Modules", StringComparison.OrdinalIgnoreCase));
            if (at >= 0 && at + 1 < folders.Length)
                return folders[at + 1];
            if (folders.Length > 0)
                return folders[0];
        }

        var words = group.TrimStart('/').Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? "Start" : char.ToUpperInvariant(words[0]) + words[1..];
    }

    // How much a kind asks of the factory: what needs a record or a row comes after what opens by itself.
    private static int Hardness(PlaceGroupKind kind) => kind switch
    {
        PlaceGroupKind.Nothing or PlaceGroupKind.Part => 0,
        PlaceGroupKind.List => 1,
        PlaceGroupKind.ListWithDialog or PlaceGroupKind.Dialog => 2,
        PlaceGroupKind.FormPage => 3,
        PlaceGroupKind.SettingsPage => 4,
        PlaceGroupKind.TabbedPage => 5,
        PlaceGroupKind.RowDialog => 6,
        _ => 7,
    };
}
