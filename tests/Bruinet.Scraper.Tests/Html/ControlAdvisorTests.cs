using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Html;

namespace Bruinet.Scraper.Tests.Html;

public sealed class ControlAdvisorTests
{
    private static readonly string[] TestIds = ["data-testid", "data-test"];

    private static ScrapeNode N(int n, int p, string type, string? text = null, string? name = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Text = text,
        Name = name,
        Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
    };

    private static ScrapeSnapshot Page(params ScrapeNode[] nodes) => new() { Platform = "html", Location = "https://x/", Nodes = nodes };

    private static ControlSuggestion Of(ScrapeSnapshot page, int node, bool blazor = false) => ControlAdvisor.Suggest(page, page[node], TestIds, blazor);

    [Fact]
    public void A_test_id_is_the_first_choice_and_names_the_member()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "button", "Save", null, ("data-testid", "save-order"), ("id", "b1")));

        var suggestion = Of(page, 2);

        Assert.Equal(("ButtonControl", "Bruinet.Html.Controls.Buttons"), (suggestion.Control, suggestion.Namespace));
        Assert.Equal("Locator.ByDataTestId(\"save-order\")", suggestion.Locator);
        Assert.Equal("SaveOrder", suggestion.Name);
        Assert.True(suggestion.Stable);
    }

    [Fact]
    public void Another_test_id_attribute_from_the_config_is_used_as_css()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "input", null, null, ("data-test", "username"), ("type", "text"), ("placeholder", "Enter your name")));

        var suggestion = Of(page, 2);

        Assert.Equal("TextInputControl", suggestion.Control);
        Assert.Equal("Username", suggestion.Name);
        Assert.Equal("Locator.ByCss(\"[data-test='username']\")", suggestion.Locator);
    }

    [Fact]
    public void A_generated_id_is_passed_over_for_the_accessible_name()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "input", null, "Search", ("id", "react-12345"), ("type", "search")));

        var suggestion = Of(page, 2);

        Assert.Equal("Locator.ByCss(\"input[aria-label='Search']\")", suggestion.Locator);
    }

    [Fact]
    public void A_test_id_that_repeats_is_not_unique_and_the_next_choice_is_taken()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "a", "Home", null, ("data-testid", "nav"), ("href", "/")),
            N(3, 1, "a", "Orders", null, ("data-testid", "nav"), ("href", "/orders")));

        var suggestion = Of(page, 3);

        Assert.Equal("LinkControl", suggestion.Control);
        Assert.Equal("Locator.ByLinkText(\"Orders\")", suggestion.Locator);
    }

    [Fact]
    public void A_button_s_text_in_a_child_names_and_finds_it()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "button", null, null, ("class", "el-button el-button--primary")), N(3, 2, "span", "Login"));

        var suggestion = Of(page, 2);

        Assert.Equal("Login", suggestion.Name);
        Assert.Equal("Locator.ByCss(\"button:has-text('Login')\")", suggestion.Locator);
    }

    [Fact]
    public void With_nothing_stable_the_position_is_given_and_said_to_be_unstable()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "div"), N(3, 2, "button", "Go"), N(4, 2, "button", "Go"));

        var suggestion = Of(page, 4);

        Assert.False(suggestion.Stable);
        Assert.Equal("Locator.ByCss(\"div > button:nth-of-type(2)\")", suggestion.Locator);
        Assert.Contains("test id", suggestion.Why);
    }

    [Fact]
    public void A_position_starts_at_the_nearest_ancestor_with_an_id_of_its_own()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "div"), N(3, 2, "nav", null, null, ("id", "side")), N(4, 3, "ul"),
            N(5, 4, "li"), N(6, 5, "a", "Go", null, ("href", "/a")), N(7, 4, "li"), N(8, 7, "a", "Go", null, ("href", "/b")),
            N(9, 1, "input", null, null, ("id", "mudinputfmqznq5c")));

        Assert.Equal("Locator.ByCss(\"#side > ul > li:nth-of-type(2) > a\")", Of(page, 8).Locator);
        Assert.False(Of(page, 9).Stable); // a UI library's numbered id is not an id to find it by
    }

    [Fact]
    public void Names_are_unique_on_a_page_and_a_symbol_s_button_is_named_by_its_data_attribute()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "button", "-", null, ("data-action", "minus")), N(3, 1, "button", "+", null, ("data-action", "plus")),
            N(4, 1, "button", "Go"), N(5, 1, "button", "Go"));

        var names = ControlAdvisor.Suggest(page, TestIds, blazor: false).Select(s => s.Name);

        Assert.Equal(["Minus", "Plus", "Go", "Go2"], names);
    }

    [Fact]
    public void A_test_id_s_camel_case_words_stay_words_in_the_name()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "input", null, null, ("data-testid", "FirstNameInput")), N(3, 1, "button", "Send it", null, ("data-testid", "save-draft_button")));

        var names = ControlAdvisor.Suggest(page, TestIds, blazor: false).Select(s => s.Name);

        Assert.Equal(["FirstNameInput", "SaveDraftButton"], names);
    }

    [Fact]
    public void A_text_is_unique_only_when_no_other_text_has_it_in_part_as_has_text_matches()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "a", "Links", null, ("href", "/links")), N(3, 1, "a", "Broken Links - Images", null, ("href", "/broken")),
            N(4, 1, "button", "Save"), N(5, 1, "button", "Save draft"), N(6, 1, "h2", "Orders"), N(7, 1, "h2", "Open orders"));

        Assert.False(Of(page, 2).Stable); // "Links" finds "Broken Links - Images" too
        Assert.Equal("Locator.ByLinkText(\"Broken Links - Images\")", Of(page, 3).Locator);
        Assert.False(Of(page, 4).Stable);
        Assert.Equal("Locator.ByCss(\"button:has-text('Save draft')\")", Of(page, 5).Locator);
        Assert.False(Of(page, 6).Stable);
    }

    [Fact]
    public void A_link_s_long_text_is_content_not_a_label_to_find_it_by()
    {
        var ad = "Organise tournaments in CS2, Minecraft and more. With anti-DDoS, no lag.";
        var page = Page(N(1, 0, "body"), N(2, 1, "a", ad, null, ("href", "https://ads.example/")));

        Assert.False(Of(page, 2).Stable);
    }

    [Fact]
    public void A_node_in_a_frame_is_declared_on_the_frame_s_member()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "iframe", null, null, ("data-testid", "profile-frame")) with { Frame = "https://x/profile" },
            N(3, 2, "html"), N(4, 3, "input", null, null, ("data-testid", "profile-id")));

        var input = ControlAdvisor.Suggest(page, TestIds, blazor: false).Single(s => s.Node == 4);

        Assert.Equal((2, "ProfileFrame"), (input.Frame, input.Scope));
    }

    [Fact]
    public void Uniqueness_is_counted_per_frame_document()
    {
        var page = Page(N(1, 0, "body"),
            N(2, 1, "button", "Pay", null, ("data-testid", "pay")),
            N(3, 1, "iframe") with { Frame = "https://pay/" },
            N(4, 3, "html"),
            N(5, 4, "button", "Pay", null, ("data-testid", "pay")));

        Assert.Equal("Locator.ByDataTestId(\"pay\")", Of(page, 5).Locator);
        Assert.Equal("FrameScope", Of(page, 3).Control);
    }

    [Fact]
    public void A_blazor_project_gets_the_blazor_controls_where_they_exist()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "input", null, null, ("type", "date"), ("id", "due")), N(3, 1, "h1", "Orders"));

        Assert.Equal(("DateInputControl", "Bruinet.Blazor.Controls"), (Of(page, 2, blazor: true).Control, Of(page, 2, blazor: true).Namespace));
        Assert.Equal(("LabelControl", "Bruinet.Html.Controls.Display"), (Of(page, 3, blazor: true).Control, Of(page, 3, blazor: true).Namespace));
    }

    [Fact]
    public void A_page_suggests_what_is_acted_on_read_or_marked_and_leaves_wrappers_out()
    {
        var page = Page(N(1, 0, "body"), N(2, 1, "div"), N(3, 2, "h1", "Orders"), N(4, 2, "p", "Some text"),
            N(5, 2, "button", "New"), N(6, 2, "span", "3", null, ("data-testid", "count")),
            N(7, 2, "select", null, null, ("data-testid", "country")), N(8, 7, "option", "NL", null, ("data-testid", "nl")));

        var suggestions = ControlAdvisor.Suggest(page, TestIds, blazor: false);

        Assert.Equal([3, 5, 6, 7], suggestions.Select(s => s.Node));
    }
}
