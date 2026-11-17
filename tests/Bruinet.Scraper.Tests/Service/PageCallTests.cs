using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Service;

/// <summary>A call a page made, as a look reports it: one line, cut when long, said once.</summary>
public sealed class PageCallTests
{
    [Fact]
    public void A_reading_call_is_its_method_address_and_status()
        => Assert.Equal("GET https://api.test/list/contacts -> 200", PageCall.Of("GET", "https://api.test/list/contacts", 200, null).ToText());

    [Fact]
    public void A_writing_call_says_what_it_sent()
        => Assert.Equal("DELETE https://api.test/contact -> 204  sent: {\"id\":7}", PageCall.Of("DELETE", "https://api.test/contact", 204, "{\"id\":7}").ToText());

    [Fact]
    public void A_long_address_and_body_are_cut()
    {
        var call = PageCall.Of("POST", "https://api.test/?q=" + new string('a', 600), 200, new string('b', 600));

        Assert.Equal(PageCall.Kept + 1, call.Url.Length);
        Assert.Equal(PageCall.Kept + 1, call.Body!.Length);
        Assert.EndsWith("…", call.Url, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_call_twice_is_said_once()
    {
        var calls = PageCall.Distinct([PageCall.Of("GET", "https://api.test/a", 200, null), PageCall.Of("GET", "https://api.test/b", 200, null), PageCall.Of("GET", "https://api.test/a", 200, null)]);

        Assert.Equal(["https://api.test/a", "https://api.test/b"], calls.Select(call => call.Url));
    }

    [Fact]
    public void An_empty_body_is_no_body()
        => Assert.Null(PageCall.Of("POST", "https://api.test/a", 200, string.Empty).Body);
}
