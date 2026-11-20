using System.Text.RegularExpressions;
using Bruinet.Core.Utilities;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// How the parts of a page show themselves in one UI library or one app, beyond the roles the platform gives: what
/// is a tab, an open menu, a dialog, a toolbar, the title of a part, an icon, and how an icon is named. Read from
/// <c>## Place signs</c> (columns <c>What</c>, <c>Sign</c>) of a control library's signs file and of the project's
/// config; the engine itself knows only the roles (<c>role=tab</c>, <c>role=dialog</c>).
/// </summary>
/// <remarks>
/// A sign is a class name, with or without its dot; several in one cell are separated by commas. <c>title</c> takes
/// the end of a class name (<c>card__title</c> fits <c>v-card__title</c>); <c>icon name</c> the start of one
/// (<c>fa-</c>); <c>icon style</c> a regular expression for what follows that start and only says how the icon is
/// drawn (<c>solid</c>, <c>\d+x</c>); <c>closes</c> an icon's whole name (<c>fa-xmark</c>).
/// </remarks>
public sealed record PlaceSigns
{
    /// <summary>The section's name in a signs file and in a config.</summary>
    public const string Section = "Place signs";

    /// <summary>No signs: only the platform's roles tell what a part is.</summary>
    public static PlaceSigns None { get; } = new();

    /// <summary>Classes of a tab.</summary>
    public IReadOnlyList<string> Tab { get; init; } = [];

    /// <summary>Classes of a menu, a list or a calendar that is open.</summary>
    public IReadOnlyList<string> OpenMenu { get; init; } = [];

    /// <summary>Classes of a dialog that is open.</summary>
    public IReadOnlyList<string> Dialog { get; init; } = [];

    /// <summary>Classes of a toolbar: its buttons are the toolbar's, not the page's.</summary>
    public IReadOnlyList<string> Toolbar { get; init; } = [];

    /// <summary>Classes of the header of a part of a form that folds: it is pressed like a button, and is the name of a section.</summary>
    public IReadOnlyList<string> SectionHeader { get; init; } = [];

    /// <summary>True when the element is the header of a section that folds.</summary>
    public bool IsSection(ScrapeNode node) => Has(node, SectionHeader);

    /// <summary>Ends of the class of a titled part's title bar.</summary>
    public IReadOnlyList<string> Title { get; init; } = [];

    /// <summary>Classes of an icon written as a word, which is no label.</summary>
    public IReadOnlyList<string> Icon { get; init; } = [];

    /// <summary>Starts of a class that names an icon.</summary>
    public IReadOnlyList<string> IconName { get; init; } = [];

    /// <summary>What may follow an icon name's start and only says how it is drawn, each a regular expression.</summary>
    public IReadOnlyList<string> IconStyle { get; init; } = [];

    /// <summary>The icons that close what is open.</summary>
    public IReadOnlyList<string> Closes { get; init; } = [];

    /// <summary>True when the element is a tab: by its role, or by a sign.</summary>
    public bool IsTab(ScrapeNode node) => node.Role == "tab" || Has(node, Tab);

    /// <summary>True when the element is an open menu, list or calendar: by its role, or by a sign.</summary>
    public bool IsOpenMenu(ScrapeNode node) => node.Role is "menu" or "listbox" || Has(node, OpenMenu);

    /// <summary>True when the element is a dialog: by its role, its tag, or a sign.</summary>
    public bool IsDialog(ScrapeNode node) => node.Role == "dialog" || node.Type == "dialog" || Has(node, Dialog);

    /// <summary>True when the element is a toolbar: by its role, or by a sign.</summary>
    public bool IsToolbar(ScrapeNode node) => node.Role == "toolbar" || Has(node, Toolbar);

    /// <summary>True when the element is the title bar of a titled part.</summary>
    public bool IsTitle(ScrapeNode node) => Classes(node).Any(name => Title.Any(end => name.EndsWith(end, StringComparison.Ordinal)));

    /// <summary>True when the element is an icon written as a word.</summary>
    public bool IsIcon(ScrapeNode node) => node.Type == "i" || Has(node, Icon);

    /// <summary>True when a class, or a label the walk made from one, is an icon's own name and not how it is drawn.</summary>
    public bool IsIconName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return IconName.FirstOrDefault(start => name.StartsWith(start, StringComparison.Ordinal)) is { } start && !_styles.Value.Any(style => style.IsMatch(name[start.Length..]));
    }

    /// <summary>True when a label is an icon that closes what is open.</summary>
    public bool IsClose(string label) => Closes.Contains(label, StringComparer.Ordinal);

    /// <summary>These signs with another's added: a project's own after its libraries'.</summary>
    public PlaceSigns With(PlaceSigns other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new PlaceSigns
        {
            Tab = [.. Tab, .. other.Tab],
            OpenMenu = [.. OpenMenu, .. other.OpenMenu],
            Dialog = [.. Dialog, .. other.Dialog],
            Toolbar = [.. Toolbar, .. other.Toolbar],
            Title = [.. Title, .. other.Title],
            Icon = [.. Icon, .. other.Icon],
            IconName = [.. IconName, .. other.IconName],
            IconStyle = [.. IconStyle, .. other.IconStyle],
            Closes = [.. Closes, .. other.Closes],
            SectionHeader = [.. SectionHeader, .. other.SectionHeader],
        };
    }

    /// <summary>Reads the rows of a <c>## Place signs</c> table.</summary>
    /// <exception cref="FormatException">A row names something that is not a place sign, or an icon style that is no regular expression.</exception>
    public static PlaceSigns Read(IEnumerable<MarkdownTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var signs = None;
        foreach (var row in rows)
        {
            var what = row.Cells.GetValueOrDefault("What", "").Trim().ToLowerInvariant();
            var values = row.Cells.GetValueOrDefault("Sign", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim('`').TrimStart('.')).Where(value => value.Length > 0).ToArray();
            signs = what switch
            {
                "tab" => signs with { Tab = [.. signs.Tab, .. values] },
                "open menu" => signs with { OpenMenu = [.. signs.OpenMenu, .. values] },
                "dialog" => signs with { Dialog = [.. signs.Dialog, .. values] },
                "toolbar" => signs with { Toolbar = [.. signs.Toolbar, .. values] },
                "title" => signs with { Title = [.. signs.Title, .. values] },
                "section" => signs with { SectionHeader = [.. signs.SectionHeader, .. values] },
                "icon" => signs with { Icon = [.. signs.Icon, .. values] },
                "icon name" => signs with { IconName = [.. signs.IconName, .. values] },
                "icon style" => signs with { IconStyle = [.. signs.IconStyle, .. values.Select(value => Checked(value, row.LineNumber))] },
                "closes" => signs with { Closes = [.. signs.Closes, .. values] },
                _ => throw new FormatException($"Line {row.LineNumber}: '{what}' is not a place sign (tab, open menu, dialog, toolbar, title, section, icon, icon name, icon style, closes)."),
            };
        }

        return signs;
    }

    /// <summary>Reads the <c>## Place signs</c> of a file; none when the file or the section is not there.</summary>
    public static PlaceSigns ReadFile(string path)
    {
        if (!File.Exists(path))
            return None;
        return MarkdownTables.ReadSections(File.ReadAllText(path)).TryGetValue(Section, out var section) ? Read(section.Rows) : None;
    }

    private readonly Lazy<Regex[]> _styles = new(() => []);

    private PlaceSigns(PlaceSigns other)
    {
        Tab = other.Tab;
        SectionHeader = other.SectionHeader;
        OpenMenu = other.OpenMenu;
        Dialog = other.Dialog;
        Toolbar = other.Toolbar;
        Title = other.Title;
        Icon = other.Icon;
        IconName = other.IconName;
        IconStyle = other.IconStyle;
        Closes = other.Closes;
        // The copy reads its own styles: a `with` may have changed them after this constructor ran.
        _styles = new Lazy<Regex[]>(() => [.. IconStyle.Select(style => new Regex($"^(?:{style})$", RegexOptions.CultureInvariant))]);
    }

    /// <summary>No signs.</summary>
    public PlaceSigns()
    {
        _styles = new Lazy<Regex[]>(() => [.. IconStyle.Select(style => new Regex($"^(?:{style})$", RegexOptions.CultureInvariant))]);
    }

    private static string Checked(string style, int line)
    {
        try
        {
            _ = new Regex($"^(?:{style})$");
            return style;
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Line {line}: the icon style '{style}' is not a regular expression: {e.Message}", e);
        }
    }

    private static bool Has(ScrapeNode node, IReadOnlyList<string> signs) => signs.Count > 0 && Classes(node).Any(name => signs.Contains(name, StringComparer.Ordinal));

    private static string[] Classes(ScrapeNode node) => node.Attribute("class")?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
}
