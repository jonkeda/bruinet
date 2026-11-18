using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Places;

public sealed class PlacesSeedsTests
{
    [Fact]
    public void The_addresses_a_map_names_in_backticks_are_known_pages()
    {
        const string map = "| Module | Item | Menu address |\n| --- | --- | --- |\n| Contacts | Overview | `/contact#/` |\n| Contacts | Create | `/contact#/create` |\nNot `an address`.";

        var places = PlacesSeeds.FromMarkdown(map).ToList();

        Assert.Equal(["/contact", "/contact#/create"], places.Select(place => place.Key));
        Assert.All(places, place => Assert.Equal(["map"], place.Sources));
    }

    // A route table as a bundler leaves it: the paths as they are, the component's name in the part it loads.
    private const string Script =
        "routes:[{path:\"/\",component:function(){return Promise.all([t.e(\"vendors\"),t.e(\"frontend_vue_pages_contact_components_Index_vue\")]).then(t.bind(t,1))}}," +
        "{path:\"/view/:id\",component:function(){return t.e(\"frontend_vue_pages_contact_components_Detail_vue\").then(t.bind(t,2))},props:!0}," +
        "{path:\"/create/:projectId?\",component:function(){return t.e(\"frontend_vue_pages_contact_components_Create_vue\").then(t.bind(t,3))}}," +
        "{path:\"/old\",redirect:\"/\"}],columns:[{path:\"contact.name\",name:\"x\"}]";

    [Fact]
    public void The_route_table_of_a_script_gives_the_pages_under_the_page_that_loads_it()
    {
        var places = PlacesSeeds.FromScript("https://app.example/contact", Script).ToList();

        Assert.Equal(["/contact", "/contact#/view/{id}", "/contact#/create"], places.Select(place => place.Key));
        Assert.Equal("component contact/components/Detail", places[1].Note);
        Assert.All(places, place => Assert.Equal(["code"], place.Sources));
    }

    [Fact]
    public void A_route_with_a_parameter_cannot_be_opened_and_one_without_can()
    {
        var places = PlacesSeeds.FromScript("/contact#/", Script).ToList();

        Assert.Null(places.Single(place => place.Key == "/contact#/view/{id}").Address);
        Assert.Equal("/contact#/create", places.Single(place => place.Key == "/contact#/create").Address);
    }

    [Fact]
    public void A_form_a_test_saved_is_saved_in_the_list_and_its_record_is_the_walk_s_own()
    {
        var list = new PlacesList();
        list.Add(new Place { Key = "/department#/create", State = PlaceState.FormNotSaved, Sources = ["walk"] });
        const string run = "{\"place\":\"/department#/create\",\"required\":[\"Name is required\"],\"landed\":\"/department#/update/812\",\"record\":\"/department#/update/812\",\"note\":\"deleted through its own page\"}\n\n";

        var owned = PlacesSeeds.AddRun(list, run);

        Assert.Equal(["/department#/update/812"], owned);
        var form = list["/department#/create"]!;
        Assert.Equal(PlaceState.Saved, form.State);
        Assert.Contains("saved empty it says: Name is required", form.Note, StringComparison.Ordinal);
        Assert.Contains("a save lands on /department#/update/{id}", form.Note, StringComparison.Ordinal);
        Assert.Equal("/department#/update/812", list["/department#/update/{id}"]!.Address);
        Assert.Single(list.Frontier);
    }

    // While the walk holds writes back: what would write is stopped, what reads goes on.
    private static RequestGuard Holding() => new(new ExploreSettings { BlockMethods = [] }) { HoldWrites = true };

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("POST")]
    public void A_held_write_is_stopped_and_kept(string method)
    {
        var guard = Holding();

        var reason = guard.Check(method, "https://app.example/api/contact/12", navigatesPage: false);

        Assert.StartsWith(RequestGuard.HeldReason, reason, StringComparison.Ordinal);
        Assert.Single(guard.Blocked);
    }

    [Fact]
    public void Without_the_hold_the_same_write_goes_through()
    {
        var guard = new RequestGuard(new ExploreSettings { BlockMethods = [] });

        Assert.Null(guard.Check("DELETE", "https://app.example/api/contact/12", navigatesPage: false));
    }

    [Fact]
    public void A_POST_a_page_made_while_it_opened_is_a_read_and_goes_through_later()
    {
        var guard = Holding();
        guard.Learning = true;
        Assert.Null(guard.Check("POST", "https://app.example/api/contact/search?page=1", navigatesPage: false));
        guard.Learning = false;

        Assert.Null(guard.Check("POST", "https://app.example/api/contact/search?page=2", navigatesPage: false));
        Assert.NotNull(guard.Check("POST", "https://app.example/api/contact", navigatesPage: false));
    }

    [Theory]
    [InlineData("{\"query\":\"query Contacts { contacts { id } }\"}", false)]
    [InlineData("{\"operationName\":\"x\",\"query\":\"\\n  mutation Save($id: ID!) { save(id: $id) }\"}", true)]
    [InlineData("{\"query\":\"{ me { id } }\"}", false)]
    public void What_a_GraphQL_call_sends_says_whether_it_writes(string body, bool held)
    {
        var guard = Holding();

        var reason = guard.Check("POST", "https://app.example/graphql", navigatesPage: false, body);

        Assert.Equal(held, reason is not null);
    }

    [Theory]
    [InlineData("http://localhost/invoice/delete?id=4", true)]
    [InlineData("http://localhost/invoice/send-reminder?id=4", true)]
    [InlineData("http://localhost/invoice/update?id=4", false)]
    [InlineData("http://localhost/invoice/view?id=4", false)]
    [InlineData("http://localhost/purchase-invoice", false)]
    [InlineData("http://localhost/checkout-document", false)]
    [InlineData("http://localhost/project/import", false)]
    [InlineData("http://localhost/project/duplicate?id=4", true)]
    [InlineData("http://localhost/logout", true)]
    public void A_GET_whose_address_says_it_changes_something_is_held(string url, bool held)
    {
        var guard = Holding();

        Assert.Equal(held, guard.Check("GET", url, navigatesPage: true) is not null);
    }

    [Theory]
    [InlineData("https://app/property-asset-management#/create", "/property-asset-management#/create")]
    [InlineData("https://app/report-capacity-planning", "/report-capacity-planning")]
    [InlineData("https://app/files/9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c", "/files/{id}")]
    [InlineData("https://app/t/AbCdEfGhIjKlMnOpQrStUvWx-12", "/t/{id}")]
    public void A_long_name_of_words_is_a_page_and_a_token_is_an_id(string location, string key)
    {
        Assert.Equal(key, PageKey.Of(location));
    }
}
