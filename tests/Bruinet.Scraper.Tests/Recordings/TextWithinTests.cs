using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Tests.Recordings;

/// <summary>An element's label is its text, or the nearest text below it, a web component's shadow root included (Bouw7 friction #23).</summary>
public sealed class TextWithinTests
{
    private static ScrapeNode N(int n, int p, string type, string? text = null, ScrapeNodeKind kind = ScrapeNodeKind.Element) =>
        new() { Number = n, Parent = p, Type = type, Text = text, Kind = kind };

    private static ScrapeSnapshot Of(params ScrapeNode[] nodes) => new() { Platform = "html", Location = "https://app.example/", Nodes = nodes };

    [Fact]
    public void A_web_component_s_text_in_its_shadow_root_is_its_label()
    {
        var snapshot = Of(N(1, 0, "body"), N(2, 1, "b7-navbar-button"), N(3, 2, "", kind: ScrapeNodeKind.ShadowRoot), N(4, 3, "div", "Contacts"), N(5, 4, "i"));

        Assert.Equal("Contacts", snapshot.TextWithin(snapshot[2]));
    }

    [Fact]
    public void Its_own_text_wins_and_the_nearest_text_comes_next()
    {
        var snapshot = Of(N(1, 0, "body"), N(2, 1, "button"), N(3, 2, "span"), N(4, 3, "b", "Deep"), N(5, 2, "span", "Near"), N(6, 1, "a", "Own"));

        Assert.Equal("Near", snapshot.TextWithin(snapshot[2]));
        Assert.Equal("Own", snapshot.TextWithin(snapshot[6]));
        Assert.Null(snapshot.TextWithin(snapshot[2], depth: 0));
    }
}
