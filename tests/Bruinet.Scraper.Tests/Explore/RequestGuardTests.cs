using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Explore;

public sealed class RequestGuardTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("put")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void By_default_a_request_that_changes_something_is_blocked_and_kept(string method)
    {
        var guard = new RequestGuard(new ExploreSettings());

        var reason = guard.Check(method, "http://localhost:5000/api/orders/4", navigatesPage: false);

        Assert.Equal($"{method.ToUpperInvariant()} is blocked (BlockMethods)", reason);
        Assert.Equal(new BlockedRequest(method.ToUpperInvariant(), "http://localhost:5000/api/orders/4", reason!), Assert.Single(guard.Blocked));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void A_request_that_only_reads_goes_through(string method)
    {
        var guard = new RequestGuard(new ExploreSettings());

        Assert.Null(guard.Check(method, "http://localhost:5000/api/orders", navigatesPage: false));
        Assert.Empty(guard.Blocked);
    }

    [Fact]
    public void A_method_left_out_of_BlockMethods_goes_through()
    {
        var guard = new RequestGuard(new ExploreSettings { BlockMethods = ["POST", "DELETE"] });

        Assert.Null(guard.Check("PUT", "http://localhost:5000/api/orders/4", navigatesPage: false));
        Assert.NotNull(guard.Check("DELETE", "http://localhost:5000/api/orders/4", navigatesPage: false));
    }

    [Fact]
    public void Allow_requests_lets_one_endpoint_through_by_path_or_url_and_method()
    {
        var guard = new RequestGuard(new ExploreSettings
        {
            AllowRequests = [new("POST", "/graphql"), new("", "http://localhost:5000/api/search")],
        });

        Assert.Null(guard.Check("POST", "http://localhost:5000/graphql?op=orders", navigatesPage: false));
        Assert.NotNull(guard.Check("DELETE", "http://localhost:5000/graphql", navigatesPage: false));
        Assert.Null(guard.Check("PUT", "http://localhost:5000/api/search/orders", navigatesPage: false));
        Assert.NotNull(guard.Check("POST", "http://localhost:5000/api/orders", navigatesPage: false));
    }

    [Fact]
    public void The_page_is_not_taken_to_another_host_but_it_may_load_from_one()
    {
        var guard = new RequestGuard(new ExploreSettings());

        Assert.Equal("the page would go to example.com, not an Explore host", guard.Check("GET", "https://example.com/", navigatesPage: true));
        Assert.Null(guard.Check("GET", "https://cdn.example.com/app.js", navigatesPage: false));
        Assert.Null(guard.Check("GET", "http://127.0.0.1:5000/orders", navigatesPage: true));
    }

    [Fact]
    public void SignalR_s_negotiate_goes_through_so_the_websocket_guard_sees_the_app_for_what_it_is()
    {
        var guard = new RequestGuard(new ExploreSettings());

        Assert.Null(guard.Check("POST", "http://localhost:5000/_blazor/negotiate?negotiateVersion=1", navigatesPage: false));
        Assert.NotNull(guard.Check("POST", "http://localhost:5000/_blazor/disconnect", navigatesPage: false));
        Assert.NotNull(guard.Check("POST", "http://localhost:5000/api/negotiate", navigatesPage: false));
    }

    [Fact]
    public void What_never_leaves_the_browser_is_not_guarded()
    {
        var guard = new RequestGuard(new ExploreSettings());

        Assert.Null(guard.Check("POST", "data:text/plain,x", navigatesPage: false));
        Assert.Null(guard.Check("GET", "about:blank", navigatesPage: true));
    }

    [Fact]
    public void Websockets_are_kept_for_guard_three()
    {
        var guard = new RequestGuard(new ExploreSettings());

        guard.WebSocketOpened("ws://localhost:5000/_blazor");

        Assert.Equal(["ws://localhost:5000/_blazor"], guard.WebSockets);
    }

    [Theory]
    [InlineData("http://localhost:5000/", true)]
    [InlineData("http://127.0.0.1:8080/", true)]
    [InlineData("https://test.exact.nl/", false)]
    public void Explore_runs_only_against_its_hosts(string location, bool allowed)
    {
        var refused = RequestGuard.RefuseHost(new Uri(location), new ExploreSettings());

        Assert.Equal(allowed, refused is null);
        if (!allowed)
            Assert.StartsWith("Explore may not run against test.exact.nl: it is not in the Explore hosts (localhost, 127.0.0.1).", refused);
    }

    [Fact]
    public void An_explore_host_is_named_by_host_or_by_host_and_port()
    {
        var settings = new ExploreSettings { Hosts = ["test.exact.nl", "staging.local:8443"] };

        Assert.Null(RequestGuard.RefuseHost(new Uri("https://test.exact.nl/app"), settings));
        Assert.Null(RequestGuard.RefuseHost(new Uri("https://staging.local:8443/"), settings));
        Assert.NotNull(RequestGuard.RefuseHost(new Uri("https://staging.local:9000/"), settings));
    }
}
