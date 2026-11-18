using Bruinet.Scraper.Places;

namespace Bruinet.Scraper.Tests.Places;

public sealed class PlacesListTests
{
    [Theory]
    [InlineData("/contact", "/contact")]
    [InlineData("/contact#/", "/contact")]
    [InlineData("https://app.example/contact/#/", "/contact")]
    [InlineData("/contact#/view/55313", "/contact#/view/{id}")]
    [InlineData("/property-asset-management#/create", "/property-asset-management#/create")]
    [InlineData("/report-capacity-planning", "/report-capacity-planning")]
    [InlineData("/project/view?id=12", "/project/view")]
    public void Two_spellings_of_one_page_have_one_key(string address, string key)
    {
        Assert.Equal(key, PlacesList.KeyAndPath(address).Key);
    }

    [Fact]
    public void A_known_page_is_on_the_frontier_with_the_address_that_opens_it()
    {
        var list = new PlacesList();

        Assert.True(list.Add(PlacesList.Known("/project/view?id=12", "map")));

        var place = Assert.Single(list.Frontier);
        Assert.Equal("/project/view", place.Key);
        Assert.Equal("/project/view?id=12", place.Address);
    }

    [Fact]
    public void A_route_with_a_parameter_is_known_and_has_no_address_to_open()
    {
        var list = new PlacesList();

        list.Add(PlacesList.Known("/contact#/view/{id}", "code"));

        Assert.Empty(list.Frontier);
        Assert.Null(list["/contact#/view/{id}"]!.Address);
    }

    [Fact]
    public void What_two_sources_know_of_a_place_adds_up()
    {
        var list = new PlacesList();
        list.Add(PlacesList.Known("/contact", "map"));

        var isNew = list.Add(new Place { Key = "/contact", State = PlaceState.Seen, Heading = "Contacts", Columns = ["Name"], Accounts = ["Creator"], Sources = ["walk"], Seen = "2026-11-18" });

        Assert.False(isNew);
        var place = list["/contact"]!;
        Assert.Equal(PlaceState.Seen, place.State);
        Assert.Equal(["map", "walk"], place.Sources);
        Assert.Equal(["Creator"], place.Accounts);
        Assert.Equal("/contact", place.Address);
        Assert.Equal("Contacts", place.Heading);
        Assert.Empty(list.Frontier);
    }

    [Fact]
    public void A_place_that_was_seen_is_not_made_unknown_again()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/contact", State = PlaceState.FormNotSaved, Fields = ["Name (text)"], Sources = ["walk"] });

        list.Add(PlacesList.Known("/contact", "map"));
        list.Add(new Place { Key = "/contact", State = PlaceState.Seen, Accounts = ["Reader"], Sources = ["walk"] });

        Assert.Equal(PlaceState.FormNotSaved, list["/contact"]!.State);
        Assert.Equal(["Name (text)"], list["/contact"]!.Fields);

        list.Add(new Place { Key = "/contact", State = PlaceState.Saved, Sources = ["run"] });
        Assert.Equal(PlaceState.Saved, list["/contact"]!.State);
    }

    [Fact]
    public void A_page_refused_to_one_account_stays_as_another_account_saw_it()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/vat", State = PlaceState.Seen, Heading = "VAT rates", Columns = ["Rate"], Accounts = ["Reader"], Sources = ["walk"] });

        list.Add(new Place { Key = "/vat", State = PlaceState.Refused, RefusedTo = ["Creator"], Note = "the app answered: 403", Sources = ["walk"] });

        var place = list["/vat"]!;
        Assert.Equal(PlaceState.Seen, place.State);
        Assert.Equal("VAT rates", place.Heading);
        Assert.Null(place.Note);
        Assert.Equal(["Reader"], place.Accounts);
        Assert.Equal(["Creator"], place.RefusedTo);
    }

    [Fact]
    public void A_page_refused_to_one_account_and_seen_by_the_next_is_seen()
    {
        var list = new PlacesList();
        list.Add(PlacesList.Known("/vat", "map"));
        list.Add(new Place { Key = "/vat", State = PlaceState.Refused, RefusedTo = ["Creator"], Note = "the app answered: 403", Sources = ["walk"] });

        list.Add(new Place { Key = "/vat", State = PlaceState.Seen, Heading = "VAT rates", Accounts = ["Reader"], Sources = ["walk"] });

        var place = list["/vat"]!;
        Assert.Equal(PlaceState.Seen, place.State);
        Assert.Equal(["Reader"], place.Accounts);
        Assert.Equal(["Creator"], place.RefusedTo);
    }

    [Fact]
    public void A_list_is_read_back_as_it_was_written()
    {
        var file = Path.Combine(Path.GetTempPath(), "bruinet-places-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var list = new PlacesList();
            list.Add(PlacesList.Known("/contact#/create", "map"));
            list.Add(new Place { Key = Place.PartKey("/contact", "+ Contact"), Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Parent = "/contact", Fields = ["Name (text, required)"], Calls = ["POST /api/contact (held)"], Sources = ["walk"] });
            list.Save(file);

            var read = PlacesList.Load(file);

            Assert.Equal(list.ToJson(), read.ToJson());
            Assert.Equal(PlaceKind.Dialog, read["/contact › + Contact"]!.Kind);
            Assert.Single(read.Frontier);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("Activities (3)", "Activities")]
    [InlineData("  Problems\n (1.204) ", "Problems")]
    [InlineData("Costs (excl. VAT)", "Costs (excl. VAT)")]
    public void A_count_in_brackets_is_no_part_of_a_name(string label, string clean)
    {
        Assert.Equal(clean, Place.Clean(label));
    }

    [Fact]
    public void The_document_counts_the_places_and_lists_the_forms_not_saved()
    {
        var list = new PlacesList();
        list.Add(PlacesList.Known("/contact", "map"));
        list.Add(PlacesList.Known("/contact#/view/{id}", "code"));
        list.Add(new Place { Key = "/contact › + Contact", Kind = PlaceKind.Dialog, State = PlaceState.FormNotSaved, Fields = ["Name (text, required)"], Sources = ["walk"] });

        var markdown = PlacesDocument.Markdown(list);

        Assert.Contains("| Places | 3 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Still to open (the frontier) | 1 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Known from the code or a link, with no address that opens them | 1 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| `/contact › + Contact` | Name (text, required) |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", markdown, StringComparison.Ordinal);
    }
}
