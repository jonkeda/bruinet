using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Service;

/// <summary>What a look says of a page, and which element it clicks for what was asked.</summary>
public sealed class PageFactsTests
{
    private static readonly ScrapeBounds OnScreen = new(0, 0, 100, 20);

    private static ScrapeNode N(int number, int parent, string type, string? text = null, params (string Key, string Value)[] attributes)
        => new()
        {
            Number = number,
            Parent = parent,
            Type = type,
            Text = text,
            Bounds = OnScreen,
            Role = attributes.FirstOrDefault(a => a.Key == "role").Value,
            Attributes = attributes.Where(a => a.Key != "role").ToDictionary(a => a.Key, a => a.Value),
        };

    private static ScrapeSnapshot Page(params ScrapeNode[] nodes)
        => new() { Platform = "html", Location = "https://app.example/contact#/create", Title = "Contacts", Nodes = nodes };

    // A form of two rows (a label beside a field with no name of its own), a combobox around its input, and its buttons.
    private static ScrapeSnapshot Form(params ScrapeNode[] more) => Page(
    [
        N(1, 0, "body"),
        N(2, 1, "h1", "Create contact"),
        N(3, 1, "div", null, ("class", "row")),
        N(4, 3, "span", "Contact Type", ("class", "subtitle-2 required")),
        N(5, 3, "div", null, ("role", "combobox")),
        N(6, 5, "input", null, ("type", "text"), ("placeholder", "Select"), ("id", "input-28")),
        N(7, 1, "div", null, ("class", "row")),
        N(8, 7, "span", "Name", ("class", "subtitle-2 required")),
        N(9, 7, "input", null, ("type", "text"), ("id", "input-30")),
        N(10, 1, "button", "Cancel", ("type", "button")),
        N(11, 1, "button", "Create", ("type", "button"), ("class", "v-btn success")),
        .. more,
    ]);

    [Fact]
    public void A_form_gives_its_heading_buttons_fields_by_the_label_beside_them_and_what_is_marked_required()
    {
        var facts = PageFacts.Of(Form());

        Assert.Equal(["Create contact"], facts.Headings);
        Assert.Equal(["Cancel", "Create"], facts.Buttons);
        Assert.Equal([new PageField("Contact Type", "combobox", false, false), new PageField("Name", "text", false, false)], facts.Fields);
        Assert.Equal(["Contact Type", "Name"], facts.MarkedRequired);
        Assert.Null(facts.Dialog);
    }

    [Fact]
    public void A_validation_message_and_an_open_list_are_facts_too()
    {
        var facts = PageFacts.Of(Form(
            N(12, 3, "div", "Contact Type is required", ("class", "v-messages__message")),
            N(13, 1, "div", "Client", ("role", "option")),
            N(14, 1, "div", "Supplier", ("role", "option"))));

        Assert.Equal(["Contact Type is required"], facts.Messages);
        Assert.Equal(["Client", "Supplier"], facts.Options);
    }

    [Fact]
    public void What_is_not_on_the_screen_is_no_fact()
    {
        var hidden = N(12, 1, "button", "Delete contact") with { Bounds = null };

        Assert.DoesNotContain("Delete contact", PageFacts.Of(Form(hidden)).Buttons);
    }

    [Fact]
    public void An_open_dialog_gives_its_own_facts_not_the_page_s()
    {
        var facts = PageFacts.Of(Form(
            N(12, 1, "div", null, ("role", "dialog")),
            N(13, 12, "div", "Are you sure you want to delete this contact?"),
            N(14, 12, "button", "No"),
            N(15, 12, "button", "Yes")));

        Assert.Equal(["No", "Yes"], facts.Buttons);
        Assert.Contains("Are you sure you want to delete this contact?", facts.Dialog, StringComparison.Ordinal);
        Assert.Empty(facts.Fields);
    }

    [Fact]
    public void An_open_menu_s_links_are_the_app_s_map()
    {
        var facts = PageFacts.Of(Page(
            N(1, 0, "body"),
            N(2, 1, "a", "Overview", ("href", "/contact?_=1a2b3c#/")),
            N(3, 1, "a", "Create contact", ("href", "#/create")),
            N(4, 1, "a", "Accounting", ("href", "https://other.example/portal")),
            N(5, 1, "a", "Sort", ("href", "javascript:void(0)")),
            N(6, 1, "a", "Overview", ("href", "/contact?_=9f8e7d#/"))));

        Assert.Equal(["Overview -> /contact#/", "Create contact -> /contact#/create"], facts.Links);
        Assert.Equal(["Project 001 -> /project/view?id=17790"], PageFacts.Of(Page(N(1, 0, "body"), N(2, 1, "a", "Project 001", ("href", "/project/view?id=17790&_=9f8e")))).Links);
    }

    [Fact]
    public void A_field_with_a_name_of_its_own_keeps_it()
    {
        var named = N(2, 1, "input", null, ("type", "email")) with { Name = "E-mail address" };

        Assert.Equal([new PageField("E-mail address", "email", false, false)], PageFacts.Of(Page(N(1, 0, "body"), named)).Fields);
    }

    [Fact]
    public void A_text_clicks_the_button_that_says_it_and_a_field_label_clicks_the_field()
    {
        var form = Form(N(12, 1, "span", "Create"));

        Assert.Equal(11, LookTarget.Find(form, "create", matched: null)!.Number);
        Assert.Equal(5, LookTarget.Find(form, "field:Contact Type", matched: null)!.Number);
        Assert.Equal(9, LookTarget.Find(form, "css:#whatever", matched: new HashSet<int> { 9 })!.Number);
        Assert.Null(LookTarget.Find(form, "Delete contact", matched: null));
    }

    [Theory]
    [InlineData(Verdict.Safe, ClickPolicy.Safe, true)]
    [InlineData(Verdict.Unknown, ClickPolicy.Safe, true)]
    [InlineData(Verdict.Unsafe, ClickPolicy.NotUnsafe, false)]
    [InlineData(Verdict.Unsafe, ClickPolicy.Safe, false)]
    public void A_look_clicks_what_is_not_unsafe_and_never_what_a_guard_forbids(Verdict verdict, ClickPolicy policy, bool expected)
        => Assert.Equal(expected, LookTarget.MayClick(policy, new Judgement(1, verdict, JudgedAction.Click, "never click (the config)", "Send", null)));

    [Theory]
    [InlineData("https://app.example/", null, "https://app.example/")]
    [InlineData("https://app.example/", "/contact#/create", "https://app.example/contact#/create")]
    [InlineData("https://app.example/", "https://app.example/project", "https://app.example/project")]
    [InlineData("https://app.example/", "contact", null)]
    [InlineData("", "/contact", null)]
    public void An_address_is_full_or_a_path_on_the_start_s_site(string start, string? asked, string? expected)
        => Assert.Equal(expected, ScraperService.LookAddress(start, asked));
}
