using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Explore;

public sealed class ClickJudgeTests
{
    private static ScrapeNode N(int n, int p, string type, string? text = null, string? name = null, string? role = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Text = text,
        Name = name,
        Role = role,
        Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
    };

    private static ScrapeSnapshot Page(params ScrapeNode[] nodes) => new() { Platform = "html", Location = "http://localhost:5000/orders", Nodes = [N(1, 0, "body"), .. nodes] };

    private static JudgeContext Context(ExploreSettings? settings = null) =>
        new(settings ?? new ExploreSettings(), uri => uri.Host is "localhost" or "127.0.0.1");

    private static Judgement Judge(ScrapeSnapshot page, int node, ExploreSettings? settings = null) =>
        ClickJudge.Judge(page, page[node], Context(settings));

    private static Judgement Button(string text, ExploreSettings? settings = null) =>
        Judge(Page(N(2, 1, "button", text, attributes: ("type", "button"))), 2, settings);

    private static ExploreSettings Off(string category) =>
        new() { DangerWords = new Dictionary<string, bool> { [category] = false } };

    public static TheoryData<string, string> EveryCategoryInFourLanguages()
    {
        var data = new TheoryData<string, string>();
        (string Category, string[] Words)[] words =
        [
            ("sign-out", ["Log out", "Afmelden", "Abmelden", "Se déconnecter"]),
            ("delete", ["Delete", "Verwijderen", "Löschen", "Supprimer"]),
            ("update", ["Save", "Opslaan", "Speichern", "Enregistrer"]),
            ("send", ["Send", "Verzenden", "Senden", "Envoyer"]),
            ("pay", ["Pay now", "Betalen", "Bezahlen", "Payer"]),
            ("approve", ["Approve", "Goedkeuren", "Genehmigen", "Approuver"]),
            ("bulk", ["Import", "Importeren", "Zurücksetzen", "Réinitialiser"]),
        ];
        foreach (var (category, list) in words)
            foreach (var word in list)
                data.Add(category, word);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCategoryInFourLanguages))]
    public void A_danger_word_of_every_category_and_language_is_unsafe(string category, string text)
    {
        var judgement = Button(text);

        Assert.Equal(Verdict.Unsafe, judgement.Verdict);
        Assert.Contains($"({category})", judgement.Reason);
    }

    [Theory]
    [MemberData(nameof(EveryCategoryInFourLanguages))]
    public void A_category_turned_off_lets_its_words_through(string category, string text)
    {
        var judgement = Button(text, Off(category));

        Assert.Equal(Verdict.Unknown, judgement.Verdict);
    }

    [Theory]
    [InlineData("deleteButton")]
    [InlineData("btn-remove")]
    [InlineData("order_delete")]
    public void A_danger_word_in_a_test_id_or_class_is_found_in_camel_case_dashes_and_underscores(string testId)
    {
        var page = Page(N(2, 1, "button", "Go", attributes: [("type", "button"), ("data-testid", testId)]));

        Assert.Equal(Verdict.Unsafe, Judge(page, 2).Verdict);
    }

    [Fact]
    public void A_danger_word_is_a_whole_word_so_a_longer_word_holding_it_is_not()
    {
        Assert.Equal(Verdict.Unknown, Button("Important").Verdict);
        Assert.Equal(Verdict.Unknown, Button("Payroll").Verdict);
    }

    [Theory]
    [InlineData("i", "class", "fa fa-trash")]
    [InlineData("svg", "data-icon", "sign-out")]
    [InlineData("img", "alt", "Delete")]
    public void An_icon_button_is_judged_by_its_icon(string icon, string attribute, string value)
    {
        var page = Page(N(2, 1, "button", attributes: ("type", "button")), N(3, 2, icon, attributes: (attribute, value)));

        Assert.Equal(Verdict.Unsafe, Judge(page, 2).Verdict);
    }

    [Fact]
    public void A_button_s_label_in_a_span_is_what_it_says()
    {
        var page = Page(N(2, 1, "button", attributes: ("type", "button")), N(3, 2, "span", "Uitloggen"));

        Assert.Contains("(sign-out)", Judge(page, 2).Reason);
    }

    [Fact]
    public void A_submit_button_and_a_button_in_a_form_without_a_type_submit()
    {
        var page = Page(
            N(2, 1, "form"),
            N(3, 2, "button", "Search"),
            N(4, 2, "input", attributes: ("type", "submit")),
            N(5, 2, "button", "Show filters", attributes: ("type", "button")),
            N(6, 1, "button", "Go", attributes: ("type", "submit")));

        Assert.Equal("submits a form", Judge(page, 3).Reason);
        Assert.Equal("submits a form", Judge(page, 4).Reason);
        Assert.Equal(Verdict.Unknown, Judge(page, 5).Verdict);
        Assert.Equal("submits a form", Judge(page, 6).Reason);
    }

    [Fact]
    public void A_link_to_a_page_of_the_app_is_a_safe_visit_with_its_url()
    {
        var page = Page(N(2, 1, "a", "Invoices", attributes: ("href", "/invoices?year=2026")));

        var judgement = Judge(page, 2);

        Assert.Equal((Verdict.Safe, JudgedAction.Visit, "http://localhost:5000/invoices?year=2026"), (judgement.Verdict, judgement.Action, judgement.Url));
        Assert.Equal("Invoices", judgement.Label);
    }

    [Theory]
    [InlineData("https://example.com/", "another host (example.com)")]
    [InlineData("mailto:sales@example.com", "a mail or phone link")]
    [InlineData("tel:+31201234567", "a mail or phone link")]
    [InlineData("ftp://localhost/file", "a ftp: link")]
    [InlineData("#top", "an anchor on the same page")]
    [InlineData("/logout", "danger word 'logout' (sign-out)")]
    [InlineData("/orders?action=delete&id=4", "danger word 'delete' (delete)")]
    public void A_link_that_leaves_the_app_goes_nowhere_new_or_holds_a_danger_word_is_unsafe(string href, string reason)
    {
        var page = Page(N(2, 1, "a", "Go", attributes: ("href", href)));

        var judgement = Judge(page, 2);

        Assert.Equal((Verdict.Unsafe, reason), (judgement.Verdict, judgement.Reason));
    }

    [Fact]
    public void A_hash_route_is_a_page_of_the_app()
    {
        var page = Page(N(2, 1, "a", "Orders", attributes: ("href", "#/orders")));

        Assert.Equal(Verdict.Safe, Judge(page, 2).Verdict);
    }

    [Fact]
    public void A_download_link_is_unsafe()
    {
        var page = Page(N(2, 1, "a", "Report", attributes: [("href", "/report.pdf"), ("download", "")]));

        Assert.Equal("a download", Judge(page, 2).Reason);
    }

    [Fact]
    public void A_javascript_link_is_judged_as_the_button_it_is()
    {
        var page = Page(
            N(2, 1, "a", "More", attributes: ("href", "javascript:void(0)")),
            N(3, 1, "a", "X", attributes: ("href", "javascript:deleteRow(3)")));

        Assert.Equal((Verdict.Unknown, JudgedAction.Click), (Judge(page, 2).Verdict, Judge(page, 2).Action));
        Assert.Equal("danger word 'delete' (delete)", Judge(page, 3).Reason);
    }

    [Fact]
    public void Never_visit_covers_a_path_and_below_it()
    {
        var settings = new ExploreSettings { NeverVisit = ["/admin"] };
        var page = Page(
            N(2, 1, "a", "Admin", attributes: ("href", "/admin/users")),
            N(3, 1, "a", "Administration", attributes: ("href", "/about")));

        Assert.Equal("never visit /admin (the config)", Judge(page, 2, settings).Reason);
        Assert.Equal(Verdict.Safe, Judge(page, 3, settings).Verdict);
    }

    [Fact]
    public void Within_leaves_links_outside_it_a_path_or_a_hash_route()
    {
        var page = Page(
            N(2, 1, "a", "Tabs", attributes: ("href", "/components/tabs#api")),
            N(3, 1, "a", "Buttons", attributes: ("href", "/components/buttons")),
            N(4, 1, "a", "Form", attributes: ("href", "/#/en-US/component/form")));

        var paths = new ExploreSettings { Within = "/components/tabs" };
        Assert.Equal(Verdict.Safe, Judge(page, 2, paths).Verdict);
        Assert.Equal("outside Within /components/tabs (the config)", Judge(page, 3, paths).Reason);

        var routes = new ExploreSettings { Within = "/#/en-US/component/tabs" };
        Assert.Equal("outside Within /#/en-US/component/tabs (the config)", Judge(page, 4, routes).Reason);
        Assert.Equal(Verdict.Safe, Judge(page, 4, new ExploreSettings()).Verdict);
    }

    [Theory]
    [InlineData("tab", null, null, "a tab")]
    [InlineData(null, "aria-haspopup", "menu", "opens a menu")]
    [InlineData(null, "aria-haspopup", "dialog", "opens a dialog")]
    [InlineData(null, "aria-expanded", "false", "shows or hides a part")]
    [InlineData(null, "aria-sort", "none", "sorts a table")]
    [InlineData(null, "class", "pagination-next", "a pager")]
    public void What_only_shows_more_is_safe(string? role, string? attribute, string? value, string reason)
    {
        (string, string)[] attributes = attribute is null ? [("type", "button")] : [("type", "button"), (attribute, value!)];
        var page = Page(N(2, 1, "button", "Details", role: role, attributes: attributes));

        var judgement = Judge(page, 2);

        Assert.Equal((Verdict.Safe, JudgedAction.Click, reason), (judgement.Verdict, judgement.Action, judgement.Reason));
    }

    [Theory]
    [InlineData("Close")]
    [InlineData("Sluiten")]
    [InlineData("Fermer")]
    public void A_close_button_named_so_is_safe(string name)
    {
        var page = Page(N(2, 1, "button", "×", name, attributes: ("type", "button")));

        Assert.Equal((Verdict.Safe, "closes"), (Judge(page, 2).Verdict, Judge(page, 2).Reason));
    }

    [Fact]
    public void An_expander_and_a_page_number_in_a_pagination_list_are_safe()
    {
        var page = Page(
            N(2, 1, "details"), N(3, 2, "summary", "More"),
            N(4, 1, "ul", attributes: ("class", "pagination")), N(5, 4, "li"), N(6, 5, "button", "2", attributes: ("type", "button")));

        Assert.Equal("an expander", Judge(page, 3).Reason);
        Assert.Equal("a pager", Judge(page, 6).Reason);
    }

    [Fact]
    public void A_danger_word_wins_over_showing_more()
    {
        var page = Page(N(2, 1, "button", "Delete", attributes: [("type", "button"), ("aria-haspopup", "dialog")]));

        Assert.Equal(Verdict.Unsafe, Judge(page, 2).Verdict);
    }

    [Fact]
    public void Never_click_by_text_matches_whole_words_and_wins_over_always()
    {
        var settings = new ExploreSettings
        {
            NeverClick = [new("text", "Empty cache")],
            AlwaysClick = [new("text", "Empty cache")],
        };
        var page = Page(
            N(2, 1, "button", "Empty cache now", attributes: ("type", "button")),
            N(3, 1, "button", "Empty caches", attributes: ("type", "button")));

        Assert.Equal("never click (the config)", Judge(page, 2, settings).Reason);
        Assert.Equal(Verdict.Unknown, Judge(page, 3, settings).Verdict);
    }

    [Fact]
    public void Never_and_always_click_by_selector_go_by_the_nodes_the_selector_matched()
    {
        var page = Page(
            N(2, 1, "button", "Filters", attributes: ("type", "button")),
            N(3, 1, "button", "Remove filter", attributes: ("type", "button")),
            N(4, 1, "button", "Remove filter", attributes: ("type", "button")));
        var context = Context() with { NeverNodes = new HashSet<int> { 2 }, AlwaysNodes = new HashSet<int> { 3 } };

        Assert.Equal(Verdict.Unsafe, ClickJudge.Judge(page, page[2], context).Verdict);
        Assert.Equal("always click (the config)", ClickJudge.Judge(page, page[3], context).Reason);
        Assert.Equal(Verdict.Unsafe, ClickJudge.Judge(page, page[4], context).Verdict);
    }

    [Fact]
    public void Always_click_lets_a_danger_word_through_but_not_a_never_visit_or_another_host()
    {
        var settings = new ExploreSettings
        {
            AlwaysClick = [new("text", "Remove filters"), new("text", "Admin"), new("text", "Docs")],
            NeverVisit = ["/admin"],
        };
        var page = Page(
            N(2, 1, "a", "Remove filters", attributes: ("href", "/orders?remove=filters")),
            N(3, 1, "a", "Admin", attributes: ("href", "/admin")),
            N(4, 1, "a", "Docs", attributes: ("href", "https://example.com/docs")));

        Assert.Equal((Verdict.Safe, "always click (the config)"), (Judge(page, 2, settings).Verdict, Judge(page, 2, settings).Reason));
        Assert.Equal(Verdict.Unsafe, Judge(page, 3, settings).Verdict);
        Assert.Equal(Verdict.Unsafe, Judge(page, 4, settings).Verdict);
    }

    [Fact]
    public void The_policy_says_what_is_acted_on()
    {
        var page = Page(
            N(2, 1, "a", "Invoices", attributes: ("href", "/invoices")),
            N(3, 1, "button", "Details", role: "tab", attributes: ("type", "button")),
            N(4, 1, "button", "Refresh", attributes: ("type", "button")),
            N(5, 1, "button", "Delete", attributes: ("type", "button")),
            N(6, 1, "a", "Elsewhere", attributes: ("href", "https://example.com/")));
        var judgements = ClickJudge.Judge(page, Context());

        string Acted(ClickPolicy policy) => string.Join(",", judgements.Where(j => ClickJudge.Allows(policy, j)).Select(j => j.Node));

        Assert.Equal("2", Acted(ClickPolicy.Links));
        Assert.Equal("2,3", Acted(ClickPolicy.Safe));
        Assert.Equal("2,3,4", Acted(ClickPolicy.NotUnsafe));
        Assert.Equal("2,3,4,5", Acted(ClickPolicy.All));
    }

    [Fact]
    public void Only_what_can_be_clicked_is_judged_in_document_order()
    {
        var page = Page(
            N(2, 1, "div", "Just text"),
            N(3, 1, "div", "Menu", role: "button"),
            N(4, 1, "span", "Hidden", attributes: ("aria-expanded", "false")),
            N(5, 1, "input", attributes: ("type", "text")),
            N(6, 1, "input", attributes: ("type", "reset")));

        Assert.Equal([3, 4, 6], ClickJudge.Judge(page, Context()).Select(j => j.Node));
    }
}
