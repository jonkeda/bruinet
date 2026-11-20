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
    public void A_line_that_is_no_fact_is_none()
    {
        Assert.Null(FieldFact.Parse("Name (text, VTextField, required)"));
        Assert.Null(FieldFact.Parse("Name: something else 5 (stated, x)"));
    }
}
