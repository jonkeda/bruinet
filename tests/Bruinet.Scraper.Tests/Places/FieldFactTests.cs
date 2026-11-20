using Bruinet.Scraper.Places;

namespace Bruinet.Scraper.Tests.Places;

// What is known of the values a field takes, and how sure: a line of the list beside the field's own.
public sealed class FieldFactTests
{
    [Fact]
    public void A_field_s_attributes_state_what_it_takes_and_what_says_nothing_is_left_out()
    {
        var facts = FieldFact.Stated("Name", new Dictionary<string, string>
        {
            ["type"] = "text", ["maxlength"] = "100", ["minlength"] = " 2 ", ["pattern"] = "[A-Z]+", ["autocomplete"] = "off", ["step"] = "",
        });

        Assert.Equal(["Name: max length 100 (stated, maxlength)", "Name: min length 2 (stated, minlength)", "Name: pattern [A-Z]+ (stated, pattern)"], facts.Select(fact => fact.ToText()));
        Assert.Empty(FieldFact.Stated("", new Dictionary<string, string> { ["maxlength"] = "5" }));
    }

    [Theory]
    [InlineData("Name: max length 100 (stated, maxlength)", "Name", "max length", "100", "stated", "maxlength")]
    [InlineData("Start date: Time: max 23:59 (seen, typed 24:00 on 2026-11-21)", "Start date: Time", "max", "23:59", "seen", "typed 24:00 on 2026-11-21")]
    [InlineData("Amount: min 0 (read, the script of invoice-create)", "Amount", "min", "0", "read", "the script of invoice-create")]
    public void A_line_of_the_list_reads_back_as_the_fact_it_was(string line, string field, string name, string value, string level, string source)
    {
        var fact = FieldFact.Parse(line)!;

        Assert.Equal((field, name, value, level, source), (fact.Field, fact.Name, fact.Value, fact.Level, fact.Source));
        Assert.Equal(line, fact.ToText());
    }

    [Fact]
    public void What_a_form_said_when_saved_empty_is_seen_and_a_new_capture_replaces_only_what_the_last_one_stated()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/contact#/create", Kind = PlaceKind.Page, State = PlaceState.FormNotSaved, Fields = ["Name (text, required)"], FieldFacts = ["Name: max length 50 (stated, maxlength)"] });

        // A test saved the form empty and wrote down what it said.
        PlacesSeeds.AddRun(list, """{"place":"/contact#/create","required":["Name: Name is required","nothing with a colon"],"landed":null,"record":null,"note":null}""");
        Assert.Equal(["Name: max length 50 (stated, maxlength)", "Name: message when empty Name is required (seen, saved empty)"], list["/contact#/create"]!.FieldFacts);

        // A later walk states another length: that replaces the old one, and what was seen stays.
        list.Add(new Place { Key = "/contact#/create", Kind = PlaceKind.Page, State = PlaceState.FormNotSaved, FieldFacts = ["Name: max length 100 (stated, maxlength)"] });
        Assert.Equal(["Name: message when empty Name is required (seen, saved empty)", "Name: max length 100 (stated, maxlength)"], list["/contact#/create"]!.FieldFacts);
    }

    [Fact]
    public void A_place_s_fields_are_a_spec_s_table_with_a_question_mark_for_what_nobody_knows()
    {
        var place = new Place
        {
            Key = "/contact#/create",
            Fields = ["Name (text, required)", "Amount (number)", "Street (text)", "Select (text, (a table's own))"],
            FieldFacts =
            [
                "Name: max length 100 (stated, maxlength)", "Name field: message when empty Name is required (seen, saved empty)",
                "Amount: min 0 (stated, min)", "Amount: max 999 (read, the script of contact-create)",
            ],
        };

        var table = FieldFact.Table(place).Split('\n');

        Assert.Equal("| Field | Kind | Required | Min | Max | Message | From |", table[0]);
        Assert.Equal("| Name | text | ☑ | ? | 100 | Name is required | max length: stated, maxlength; message when empty: seen, saved empty |", table[2]);
        // A limit only read from a script is a question, not a cell.
        Assert.Equal("| Amount | number | ☐ | 0 | 999? |  | min: stated, min; max: read, the script of contact-create |", table[3]);
        Assert.Equal("| Street | text | ☐ | ? | ? |  |  |", table[4]);
    }

    [Fact]
    public void The_rights_matrix_says_what_each_account_that_walked_saw_and_was_refused()
    {
        Place[] places =
        [
            new() { Key = "/contact", Kind = PlaceKind.Page, Accounts = ["Creator", "Reader"] },
            new() { Key = "/contact#/create", Kind = PlaceKind.Page, Accounts = ["Creator"], RefusedTo = ["Reader"] },
            new() { Key = "/right", Kind = PlaceKind.Page, Accounts = ["Creator"] },
            new() { Key = "/contact › + Contact", Kind = PlaceKind.Dialog, Accounts = ["Creator"] },
            new() { Key = "/never", Kind = PlaceKind.Page },
        ];

        var all = Bruinet.Scraper.Factory.FactoryReview.Rights(places, []);
        var contacts = Bruinet.Scraper.Factory.FactoryReview.Rights(places, ["/contact"]);

        Assert.Equal("| Page | Creator | Reader |\n| --- | --- | --- |\n| `/contact` | sees | sees |\n| `/contact#/create` | sees | refused |\n| `/right` | sees |  |\n", all);
        Assert.DoesNotContain("/right", contacts, StringComparison.Ordinal);
        Assert.Contains("| `/contact#/create` | sees | refused |", contacts, StringComparison.Ordinal);
    }

    [Fact]
    public void What_a_test_saw_when_it_typed_is_added_and_the_place_is_as_it_was()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/contact#/create", Kind = PlaceKind.Page, State = PlaceState.FormNotSaved, Fields = ["Postcode (text)", "E-mail (text)"], FieldFacts = ["Postcode: max length 20 (stated, maxlength)"] });

        PlacesSeeds.AddRun(list, """{"place":"/contact#/create","facts":["Postcode: max length 20 (seen, typed 300 characters)","E-mail: message when wrong not a valid email address (seen, typed x)","Postcode: max length 9 (read, a script)","no fact"]}""");
        PlacesSeeds.AddRun(list, """{"place":"/nowhere","facts":["Name: max length 5 (seen, typed 300 characters)"]}""");

        var place = list["/contact#/create"]!;
        Assert.Equal(PlaceState.FormNotSaved, place.State);
        Assert.Null(list["/nowhere"]);
        Assert.Equal(["Postcode: max length 20 (stated, maxlength)", "Postcode: max length 20 (seen, typed 300 characters)", "E-mail: message when wrong not a valid email address (seen, typed x)"], place.FieldFacts);

        // What was seen counts for more than what is stated, and a wrong value's message stands in the Message cell.
        var table = FieldFact.Table(place).Split('\n');
        Assert.Equal("| Postcode | text | ☐ | ? | 20 |  | max length: seen, typed 300 characters |", table[2]);
        Assert.Equal("| E-mail | text | ☐ | ? | ? | wrong value: not a valid email address | message when wrong: seen, typed x |", table[3]);

        // Seen again with another length: the old one goes.
        PlacesSeeds.AddRun(list, """{"place":"/contact#/create","facts":["Postcode: max length 10 (seen, typed 300 characters)"]}""");
        Assert.Equal(["Postcode: max length 20 (stated, maxlength)", "E-mail: message when wrong not a valid email address (seen, typed x)", "Postcode: max length 10 (seen, typed 300 characters)"], list["/contact#/create"]!.FieldFacts);
    }

    [Fact]
    public void A_screen_s_field_sections_hold_facts_dashes_and_open_questions_and_no_question_mark()
    {
        var list = new PlacesList();
        list.Add(new Place
        {
            Key = "/contact#/create", Kind = PlaceKind.Page, State = PlaceState.FormNotSaved,
            Fields = ["Kind (text, VSelect, required)", "Name (text, VTextField, required)", "E-mail (text, VTextField)", "Remark (text)", "Notes (textarea)", "Amount (number)", "Code (text)", "Search (text, in a table)"],
            FieldFacts = ["Name: max length 255 (stated, maxlength)", "Code: pattern [A-Z]+ (stated, pattern)", "Code: max length 9 (read, the script of contact-create)", "Amount: max 500 (stated, max)"],
        });
        // A save test saved it empty, then with 300 characters where the browser holds no limit.
        PlacesSeeds.AddRun(list, """{"place":"/contact#/create","required":["Name: Name is required"],"landed":"/contact","record":null,"note":null,"facts":["Remark: takes 300 (seen, saved 300 characters)","Notes: message when too long At most 2000 characters (seen, saved 300 characters)","E-mail: message when wrong not a valid email address (seen, typed x)"]}""");

        var text = FieldSpec.Of(list["/contact#/create"]!);
        var rows = text.Split('\n');

        Assert.DoesNotContain("?", text.Replace("?\"", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("| Kind | select | ☑ | – | – | open |", rows);
        Assert.Contains("| Name | text | ☑ | 1 | 255 | Name is required |", rows);
        Assert.Contains("| E-mail | text | ☐ | – | open |  |", rows);
        Assert.Contains("| Remark | text | ☐ | – | 300 or more |  |", rows);
        Assert.Contains("| Amount | number | ☐ | open | 500 |  |", rows);
        // What was only read is no cell: it is a question with the value as the proposal.
        Assert.Contains("| Code | text | ☐ | – | open |  |", rows);
        Assert.Contains("- Code: its longest. the script of contact-create says 9; nobody tried it on the app.", rows);
        Assert.Contains("| Code | A value shall match `[A-Z]+` (stated, pattern). |", rows);
        Assert.Contains("| E-mail | A value that does not fit is answered with \"not a valid email address\" (seen, typed x). |", rows);
        Assert.Contains("| Notes | A value that is too long is answered with \"At most 2000 characters\" (seen, saved 300 characters). |", rows);
        Assert.Contains("- Kind: what the form says when it is left empty. No test saved the form without it.", rows);
        Assert.Contains("- Remark: its longest. A save with 300 characters was taken (saved 300 characters); where it ends was not tried.", rows);
        Assert.DoesNotContain(rows, row => row.StartsWith("| Search", StringComparison.Ordinal));
    }

    [Fact]
    public void A_line_that_is_no_fact_is_none()
    {
        Assert.Null(FieldFact.Parse("Name (text, VTextField, required)"));
        Assert.Null(FieldFact.Parse("Name: something else 5 (stated, x)"));
    }
}
