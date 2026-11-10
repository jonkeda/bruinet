using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Code;

namespace Bruinet.Scraper.Tests.Code;

public sealed class ControlKindTests
{
    private static ScrapeNode N(int n, int p, string type, string? role = null, string? text = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Role = role,
        Text = text,
        Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
    };

    private static ScrapeSnapshot Page(params ScrapeNode[] nodes) => new() { Platform = "html", Location = "https://app.example/", Nodes = nodes };

    [Theory]
    [InlineData("input", null, "type", "text", PartCore.Text)]
    [InlineData("input", null, "type", "checkbox", PartCore.Toggle)]
    [InlineData("input", null, "type", "range", PartCore.Range)]
    [InlineData("input", null, "type", "date", PartCore.Date)]
    [InlineData("input", null, "type", "submit", PartCore.Clickable)]
    [InlineData("select", null, "name", "x", PartCore.Selector)]
    [InlineData("textarea", null, "name", "x", PartCore.Text)]
    [InlineData("div", "switch", "name", "x", PartCore.Toggle)]
    [InlineData("div", "slider", "name", "x", PartCore.Range)]
    [InlineData("div", "button", "aria-haspopup", "listbox", PartCore.Selector)]
    [InlineData("span", null, "aria-checked", "true", PartCore.Toggle)]
    [InlineData("div", null, "contenteditable", "true", PartCore.Text)]
    [InlineData("div", null, "tabindex", "0", PartCore.Clickable)]
    [InlineData("legend", null, "name", "x", PartCore.Readable)]
    public void An_element_is_read_from_its_tag_role_and_attributes(string type, string? role, string attribute, string value, PartCore expected)
    {
        var node = N(1, 0, type, role, null, (attribute, value));

        Assert.Equal(expected, ControlKind.Of(Page(node), node)?.Core);
    }

    [Fact]
    public void A_suggestion_carries_what_a_test_does_with_the_element_even_where_no_Bruinet_control_fits()
    {
        var box = N(2, 1, "div", "button", null, ("aria-haspopup", "listbox"), ("data-testid", "country"));
        var page = Page(N(1, 0, "body"), box);

        var suggestion = Bruinet.Scraper.Html.ControlAdvisor.Suggest(page, ["data-testid"], blazor: false).Single(s => s.Node == 2);

        Assert.Equal("selector (aria-haspopup=listbox on role=button)", suggestion.Core);
    }

    [Fact]
    public void A_readonly_input_in_a_box_that_pops_a_list_up_is_a_selector_and_a_wrapper_or_a_hidden_input_is_nothing()
    {
        var box = N(1, 0, "div", "button", null, ("aria-haspopup", "listbox"));
        var wrapper = N(2, 1, "div");
        var input = N(3, 2, "input", null, null, ("type", "text"), ("readonly", "readonly"));
        var hidden = N(4, 2, "input", null, null, ("type", "hidden"));
        var page = Page(box, wrapper, input, hidden);

        var reading = ControlKind.Of(page, input);

        Assert.Equal(PartCore.Selector, reading?.Core);
        Assert.Contains("aria-haspopup=listbox", reading!.Because);
        Assert.Null(ControlKind.Of(page, wrapper));
        Assert.Null(ControlKind.Of(page, hidden));
        Assert.Equal([1, 3], ControlKind.Below(page, box).Select(b => b.Node.Number));
    }
}
