using Bruinet.Core.Utilities;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Projects;

// How a library's or an app's parts show themselves is data: the engine itself knows only the platform's roles.
public sealed class PlaceSignsTests
{
    private static PlaceSigns Read(params string[] rows) =>
        PlaceSigns.Read(MarkdownTables.ReadSections("## Place signs\n\n| What | Sign |\n| --- | --- |\n" + string.Join('\n', rows) + "\n")[PlaceSigns.Section].Rows);

    private static ScrapeNode Node(string type, string? role = null, string? classes = null) => new()
    {
        Number = 1,
        Parent = 0,
        Type = type,
        Role = role,
        Attributes = classes is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["class"] = classes },
    };

    [Fact]
    public void Without_signs_only_the_roles_tell_what_a_part_is()
    {
        var none = PlaceSigns.None;

        Assert.True(none.IsTab(Node("div", "tab")));
        Assert.True(none.IsOpenMenu(Node("ul", "listbox")));
        Assert.True(none.IsDialog(Node("dialog")));
        Assert.True(none.IsToolbar(Node("div", "toolbar")));
        Assert.False(none.IsTab(Node("div", classes: "v-tab")));
        Assert.False(none.IsOpenMenu(Node("div", classes: "menuable__content__active")));
        Assert.False(none.IsTitle(Node("div", classes: "v-card__title")));
        Assert.False(none.IsIconName("fa-pen"));
    }

    [Fact]
    public void A_table_s_rows_say_what_a_library_s_classes_are()
    {
        var signs = Read("| tab | `.v-tab` |", "| open menu | .menuable__content__active |", "| dialog | `.v-dialog` |", "| toolbar | `.ck-toolbar`, `.ck-editor` |", "| title | `card__title` |", "| icon | `.v-icon` |");

        Assert.True(signs.IsTab(Node("div", classes: "v-tab v-tab--active")));
        Assert.True(signs.IsOpenMenu(Node("div", classes: "v-menu__content menuable__content__active")));
        Assert.True(signs.IsDialog(Node("div", classes: "v-dialog")));
        Assert.True(signs.IsToolbar(Node("div", classes: "ck ck-editor")));
        // A title is told by the end of its class: Vuetify's card title and another library's.
        Assert.True(signs.IsTitle(Node("div", classes: "v-card__title")));
        Assert.True(signs.IsIcon(Node("span", classes: "v-icon")));
        Assert.False(signs.IsTab(Node("div", classes: "v-tabs")));
    }

    [Fact]
    public void An_icon_s_name_is_a_class_that_starts_as_the_signs_say_and_is_not_how_it_is_drawn()
    {
        var signs = Read("| icon name | `fa-`, `mdi-` |", "| icon style | `solid`, `\\d+x`, `rotate.*` |", "| closes | `fa-xmark` |");

        Assert.True(signs.IsIconName("fa-pen"));
        Assert.True(signs.IsIconName("mdi-chevron-up"));
        Assert.False(signs.IsIconName("fa-solid"));
        Assert.False(signs.IsIconName("fa-2x"));
        Assert.False(signs.IsIconName("fa-rotate-90"));
        Assert.False(signs.IsIconName("btn-primary"));
        Assert.True(signs.IsClose("fa-xmark"));
        Assert.False(signs.IsClose("fa-pen"));
    }

    [Fact]
    public void A_project_s_own_signs_are_added_to_its_libraries()
    {
        var all = Read("| tab | .v-tab |", "| icon name | mdi- |").With(Read("| icon name | fa- |", "| icon style | solid |"));

        Assert.Equal(["v-tab"], all.Tab);
        Assert.Equal(["mdi-", "fa-"], all.IconName);
        Assert.True(all.IsIconName("fa-pen"));
        Assert.False(all.IsIconName("fa-solid"));
    }

    [Fact]
    public void What_is_no_place_sign_or_no_regular_expression_is_said_with_its_line()
    {
        Assert.Contains("'window' is not a place sign", Assert.Throws<FormatException>(() => Read("| window | .x |")).Message, StringComparison.Ordinal);
        Assert.Contains("is not a regular expression", Assert.Throws<FormatException>(() => Read("| icon style | `(` |")).Message, StringComparison.Ordinal);
    }

    // The engine names no UI library, no icon set and no app: what one of them calls its parts is in a signs file.
    [Fact]
    public void The_engine_s_code_names_no_library_and_no_app()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var words = new[] { "v-tab", "v-dialog", "v-icon", "menuable__", "card__title", "card-header", "material-icons", "ck-toolbar", "ck-editor", "ql-toolbar", "tox-toolbar", "\"fa-", "\"mdi-", "Bouw7" };
        var found = new[] { "Bruinet.Scraper", "Bruinet.Scraper.Host" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadLines(file).Select((line, at) => (File: Path.GetFileName(file), Line: at + 1, Text: line)))
            // A comment or a summary may give an example; code may not decide by one.
            .Where(line => !line.Text.TrimStart().StartsWith("//", StringComparison.Ordinal) && words.Any(word => line.Text.Contains(word, StringComparison.Ordinal)))
            .Select(line => $"{line.File}({line.Line}): {line.Text.Trim()}")
            .ToList();

        Assert.True(found.Count == 0, "The engine decides by a library's or an app's own name:\n" + string.Join('\n', found));
    }
}
