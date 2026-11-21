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
        // A label the page marks as required says it for its field.
        Assert.Equal([new PageField("Contact Type", "combobox", true, false), new PageField("Name", "text", true, false)], facts.Fields);
        Assert.Equal(["Contact Type", "Name"], facts.MarkedRequired);
        Assert.Null(facts.Dialog);
    }

    [Fact]
    public void A_field_is_the_control_whose_sign_it_or_an_element_around_it_shows_and_the_first_sign_decides()
    {
        var signs = ControlSign.Read(Bruinet.Core.Utilities.MarkdownTables.ReadSections(
            "## Signs\n\n| Control | Sign | State |\n| --- | --- | --- |\n| `VSelect` | `.v-select` | made |\n| `VFileInput` | `input[type=file]` | to make |\n| `VDateField` | `.v-text-field:has(i:text(event))` | made |\n| `VTextField` | `.v-text-field` | made |\n")["Signs"].Rows, "Vuetify");
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "div", null, ("class", "v-input v-text-field v-select")),
            N(3, 2, "label", "Country"),
            N(4, 2, "input", null, ("type", "text")),
            N(5, 1, "div", null, ("class", "v-input v-text-field")),
            N(6, 5, "label", "City"),
            N(7, 5, "input", null, ("type", "text")),
            N(8, 1, "div", null, ("class", "upload")),
            N(9, 8, "label", "Logo"),
            N(10, 8, "input", null, ("type", "file")),
            N(11, 1, "div", null, ("class", "plain")),
            N(12, 11, "label", "Note"),
            N(13, 11, "textarea"),
            // A text field with the calendar icon in it is a date.
            N(14, 1, "div", null, ("class", "v-input v-text-field")),
            N(15, 14, "label", "Start"),
            N(16, 14, "input", null, ("type", "text")),
            N(17, 14, "div"),
            N(18, 17, "i", "event", ("class", "v-icon")),
            // A select that has a value shows it before its input: that is the value, and the label is outside.
            N(19, 1, "div", null, ("class", "row")),
            N(20, 19, "span", "Status"),
            N(21, 19, "div", null, ("class", "v-input v-text-field v-select")),
            N(22, 21, "div", "To send", ("class", "v-select__selection")),
            N(23, 21, "input", null, ("type", "text")));

        var fields = PageFacts.Of(page, signs).Fields;

        Assert.Equal(["Country (text, VSelect)", "City (text, VTextField)", "Logo (file, VFileInput?)", "Note (textarea)", "Start (text, VDateField)", "Status (text, VSelect)"], fields.Select(field => field.ToListText()));
        // Without signs a field has no control, as before.
        Assert.All(PageFacts.Of(page).Fields, field => Assert.Null(field.Control));
    }

    // A select's input lies many elements under its row, and a date's row holds a second input for its time.
    [Fact]
    public void A_field_inside_its_own_label_is_called_what_that_label_says_and_a_tab_drawn_as_a_button_is_no_button()
    {
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "label", "Email"),
            N(3, 2, "input", null, ("type", "email")),
            // The label before it is another field's: this one's own label is around it.
            N(4, 1, "label", "Description"),
            N(5, 4, "textarea", null, ("placeholder", "Describe your issue")),
            N(6, 1, "div", null, ("role", "tablist")),
            N(7, 6, "button", "Open (3)", ("role", "tab")),
            N(8, 1, "button", "Refresh"));

        var facts = PageFacts.Of(page);

        Assert.Equal(["Email", "Description"], facts.Fields.Select(field => field.Label));
        Assert.Equal(["Refresh"], facts.Buttons);
        Assert.Equal(["Open (3)"], facts.Tabs);
    }

    [Fact]
    public void A_field_a_label_names_by_its_id_is_called_what_that_label_says()
    {
        // The label stands after its field and under another element: no label is around the field or before it.
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "input", null, ("id", "code"), ("name", "c"), ("maxlength", "6")),
            N(3, 1, "footer"),
            N(4, 3, "label", "Code", ("for", "code")),
            N(5, 3, "label", "Nothing's", ("for", "none")));

        var facts = PageFacts.Of(page);

        Assert.Equal(["Code"], facts.Fields.Select(field => field.Label));
        Assert.Equal(["Code: max length 6 (stated, maxlength)"], facts.FieldFacts.Select(fact => fact.ToText()));
    }

    [Fact]
    public void A_table_s_field_right_under_a_column_s_header_filters_that_column()
    {
        ScrapeNode At(ScrapeNode node, int x, int y, int width = 100) => node with { Bounds = new ScrapeBounds(x, y, width, 20) };
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "table"),
            N(3, 2, "thead"),
            At(N(4, 3, "th", "Name"), 0, 100),
            At(N(5, 3, "th", "City"), 100, 100),
            At(N(6, 3, "th", "Kind"), 200, 100),
            N(7, 2, "tbody"),
            N(8, 7, "tr"),
            N(9, 8, "td"),
            At(N(10, 9, "input", null, ("placeholder", "Search")), 10, 122, 80),
            N(11, 8, "td"),
            // A field far below the header is a row's own cell, not the column's filter.
            At(N(12, 11, "input", null, ("placeholder", "Search")), 110, 400, 80),
            N(13, 8, "td"),
            At(N(14, 13, "select", null, ("name", "kind")), 210, 122, 80));

        var facts = PageFacts.Of(page);

        Assert.Equal(["Name: filter text (stated, a field under the column)", "Kind: filter select (stated, a field under the column)"],
            facts.FieldFacts.Where(fact => fact.Name == Bruinet.Scraper.Places.FieldFact.Filter).Select(fact => fact.ToText()));
    }

    [Fact]
    public void A_field_states_what_it_takes_by_its_attributes()
    {
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "label", "Name"),
            N(3, 2, "input", null, ("type", "text"), ("maxlength", "100")),
            N(4, 1, "label", "Amount"),
            N(5, 4, "input", null, ("type", "number"), ("min", "0"), ("step", "0.01")));

        var facts = PageFacts.Of(page);

        Assert.Equal(["Name: max length 100 (stated, maxlength)", "Amount: min 0 (stated, min)", "Amount: step 0.01 (stated, step)"], facts.FieldFacts.Select(fact => fact.ToText()));
        // The field's own line does not change with what is known of it.
        Assert.Equal("Name (text)", facts.Fields[0].ToListText());
    }

    [Fact]
    public void A_button_that_shows_only_an_icon_s_word_and_the_header_of_a_section_are_marked()
    {
        var places = new PlaceSigns { Icon = ["material-icons"], SectionHeader = ["panel-header"] };
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "button"),
            N(3, 2, "i", "arrow_back", ("class", "material-icons")),
            N(4, 1, "button", "Credentials", ("class", "panel-header")),
            N(5, 1, "button", "Save"));

        Assert.Equal(["arrow_back (icon)", "Credentials (section)", "Save"], PageFacts.Of(page, null, places).Buttons);
    }

    [Fact]
    public void A_field_deep_under_its_row_takes_the_row_s_label_and_a_second_field_of_the_row_is_named_after_it()
    {
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "div", null, ("class", "row")),
            N(3, 2, "div"), N(4, 3, "div"), N(5, 4, "span", "Employee"),
            N(6, 2, "div"), N(7, 6, "div"), N(8, 7, "div"), N(9, 8, "div"), N(10, 9, "div"), N(11, 10, "div"), N(12, 11, "div"),
            N(13, 12, "input", null, ("type", "text"), ("placeholder", "Select")),
            N(14, 1, "div", null, ("class", "row")),
            N(15, 14, "span", "Start date"),
            // The two inputs share a wrapper; the label is one element further up.
            N(16, 14, "div"),
            N(17, 16, "input", null, ("type", "text")),
            N(18, 16, "i", "event", ("class", "v-icon material-icons")),
            N(19, 16, "div"),
            N(20, 19, "input", null, ("type", "text"), ("placeholder", "Time")));

        Assert.Equal(["Employee", "Start date", "Start date: Time"], PageFacts.Of(page).Fields.Select(field => field.Label));
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
    public void A_field_says_what_holds_it_when_the_project_names_its_holders()
    {
        var signs = ControlSign.Read(Bruinet.Core.Utilities.MarkdownTables.ReadSections(
            "## Field holders\n\n| Holder | Sign |\n| --- | --- |\n| FormRow | .form-row |\n| Card | .v-card |\n")["Field holders"].Rows, "the suite");
        var page = Page(
            N(1, 0, "body"),
            N(2, 1, "div", null, ("class", "v-card")),
            N(3, 2, "div", null, ("class", "form-row")),
            N(4, 3, "span", "Name"),
            N(5, 3, "input", null, ("type", "text")),
            N(6, 1, "div", null, ("class", "v-card")),
            N(7, 6, "span", "Landing page"),
            N(8, 6, "input", null, ("type", "text")),
            N(9, 1, "div"),
            N(10, 9, "span", "Loose"),
            N(11, 9, "input", null, ("type", "text")));

        // The nearest holder is the field's: a row in a card is a row.
        Assert.Equal(["Name (text, in a FormRow)", "Landing page (text, in a Card)", "Loose (text)"], PageFacts.Of(page, signs).Fields.Select(field => field.ToListText()));
    }

    [Fact]
    public void A_text_editor_s_own_buttons_are_not_the_page_s()
    {
        var facts = PageFacts.Of(Form(
            N(12, 1, "div", null, ("class", "ck ck-toolbar")),
            N(13, 12, "button", "Bold"),
            N(14, 1, "div", null, ("role", "toolbar")),
            N(15, 14, "button", "Italic")), places: new PlaceSigns { Toolbar = ["ck-toolbar"] });

        Assert.Equal(["Cancel", "Create"], facts.Buttons);
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
    public void A_list_or_a_calendar_a_dialog_opened_beside_it_is_the_dialog_s_too()
    {
        var facts = PageFacts.Of(Form(
            N(12, 1, "div", null, ("role", "dialog")),
            N(13, 12, "button", "Cancel"),
            N(14, 1, "div", null, ("role", "listbox")),
            N(15, 14, "div", "Esther Voorbeeld", ("role", "option")),
            N(16, 1, "div", null, ("class", "v-menu__content menuable__content__active")),
            N(17, 16, "button", "November 2026")), places: new PlaceSigns { OpenMenu = ["menuable__content__active"] });

        Assert.Equal(["Cancel", "November 2026"], facts.Buttons);
        Assert.Equal(["Esther Voorbeeld"], facts.Options);
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
    public void A_table_s_rows_are_counted_and_the_first_are_read_by_their_cells()
    {
        var facts = PageFacts.Of(Page(
            N(1, 0, "body"),
            N(2, 1, "tr"), N(3, 2, "th", "Date"), N(4, 2, "th", "Employee"),
            N(5, 1, "tr"), N(6, 5, "td", "14/11/2026"), N(7, 5, "td", "Arend"),
            N(8, 1, "tr"), N(9, 8, "td", "15/11/2026"), N(10, 8, "td", "Daan")));

        Assert.Equal(["Date", "Employee"], facts.Columns);
        Assert.Equal(["2 rows", "14/11/2026 · Arend", "15/11/2026 · Daan"], facts.Rows);
        Assert.Contains("Rows: 2 rows | 14/11/2026 · Arend", facts.ToText(), StringComparison.Ordinal);
        Assert.Empty(PageFacts.Of(Form()).Rows);
    }

    [Fact]
    public void A_look_as_an_account_starts_from_that_account_s_saved_sign_in_beside_the_project_s()
    {
        var folder = Directory.CreateTempSubdirectory("bruinet-look-as").FullName;
        try
        {
            var creator = Path.Combine(folder, "Creator.json");
            File.WriteAllText(creator, "{}");
            File.WriteAllText(Path.Combine(folder, "Reader.json"), "{}");

            Assert.Equal(creator, ScraperService.SignInFor(creator, null));
            Assert.Equal(Path.Combine(folder, "Reader.json"), ScraperService.SignInFor(creator, "Reader"));
            Assert.Contains("there: Creator, Reader", Assert.Throws<ScraperProblem>(() => ScraperService.SignInFor(creator, "Admin")).Message, StringComparison.Ordinal);
            Assert.Throws<ScraperProblem>(() => ScraperService.SignInFor(creator, "../Reader"));
            Assert.Throws<ScraperProblem>(() => ScraperService.SignInFor(null, "Reader"));
            Assert.Equal(Path.Combine(folder, "profile"), ScraperService.ProfileFor(Path.Combine(folder, "profile"), null));
            Assert.Equal(Path.Combine(folder, "profile-Reader"), ScraperService.ProfileFor(Path.Combine(folder, "profile"), "Reader"));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_command_that_renews_a_sign_in_is_the_config_s_with_the_account_in_it()
    {
        var config = ScraperConfig.Parse(string.Join('\n',
            "# Scraper", "", "## App", "", "| Field | Value |", "| --- | --- |", "| Platform | html |", "| StartUrl | https://app.example |", "",
            "## Explore", "", "| Field | Value |", "| --- | --- |", "| RenewSignIn | dotnet test --filter Category=SignIn -e SIGN_IN_AS={account} |", ""));

        Assert.Equal("dotnet test --filter Category=SignIn -e SIGN_IN_AS={account}", config.Explore.RenewSignIn);
        Assert.Equal("dotnet test --filter Category=SignIn -e SIGN_IN_AS=Reader", ScraperService.RenewLine(config.Explore.RenewSignIn, "Reader"));
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
