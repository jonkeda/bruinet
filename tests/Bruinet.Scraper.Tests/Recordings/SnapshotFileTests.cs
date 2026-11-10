using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Recordings;

namespace Bruinet.Scraper.Tests.Recordings;

public sealed class SnapshotFileTests
{
    private static ScrapeSnapshot Sample() => new()
    {
        Platform = "html",
        Location = "http://localhost/checkout.html",
        Title = "Checkout",
        CapturedAt = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
        Nodes =
        [
            new() { Number = 1, Parent = 0, Type = "html", Bounds = new(0, 0, 1280, 720) },
            new() { Number = 2, Parent = 1, Type = "button", Name = "Pay", Text = "Pay now", Attributes = new Dictionary<string, string> { ["id"] = "pay", ["class"] = "btn primary", ["data-testid"] = "pay" } },
            new() { Number = 3, Parent = 1, Type = "iframe", Frame = "http://127.0.0.1/frames/payment.html" },
            new() { Number = 4, Parent = 3, Type = "html" },
            new() { Number = 5, Parent = 1, Type = "fluent-button" },
            new() { Number = 6, Parent = 5, Kind = ScrapeNodeKind.ShadowRoot },
            new() { Number = 7, Parent = 6, Type = "button", Text = "Go \"now\" é" },
        ],
    };

    private static string Write(ScrapeSnapshot snapshot)
    {
        using var writer = new StringWriter();
        SnapshotFile.Write(snapshot, writer);
        return writer.ToString();
    }

    [Fact]
    public void A_snapshot_reads_back_as_written()
    {
        var snapshot = Sample();

        var read = SnapshotFile.Read(new StringReader(Write(snapshot)));

        Assert.Equal(snapshot.Platform, read.Platform);
        Assert.Equal(snapshot.Location, read.Location);
        Assert.Equal(snapshot.Title, read.Title);
        Assert.Equal(snapshot.CapturedAt, read.CapturedAt);
        Assert.Equal(snapshot.Nodes.Count, read.Nodes.Count);
        for (var i = 0; i < snapshot.Nodes.Count; i++)
        {
            var (a, b) = (snapshot.Nodes[i], read.Nodes[i]);
            Assert.Equal((a.Number, a.Parent, a.Kind, a.Type, a.Role, a.Name, a.Text, a.Frame, a.Bounds), (b.Number, b.Parent, b.Kind, b.Type, b.Role, b.Name, b.Text, b.Frame, b.Bounds));
            Assert.Equal(a.Attributes.OrderBy(x => x.Key), b.Attributes.OrderBy(x => x.Key));
        }
    }

    [Fact]
    public void Each_node_is_one_line_after_the_header()
    {
        var lines = Write(Sample()).TrimEnd('\n').Split('\n');

        Assert.Equal(1 + 7, lines.Length);
        Assert.StartsWith("{\"platform\":\"html\"", lines[0]);
        Assert.Equal("{\"n\":1,\"p\":0,\"type\":\"html\",\"box\":[0,0,1280,720]}", lines[1]);
    }

    [Fact]
    public void Attributes_are_written_in_name_order_whatever_order_they_came_in()
    {
        var line = Write(Sample()).Split('\n')[2];

        Assert.Equal("{\"n\":2,\"p\":1,\"type\":\"button\",\"name\":\"Pay\",\"text\":\"Pay now\",\"attr\":{\"class\":\"btn primary\",\"data-testid\":\"pay\",\"id\":\"pay\"}}", line);
    }

    [Fact]
    public void Empty_fields_are_left_out_and_a_shadow_root_says_its_kind()
    {
        var line = Write(Sample()).Split('\n')[6];

        Assert.Equal("{\"n\":6,\"p\":5,\"kind\":\"shadow-root\"}", line);
    }

    [Fact]
    public void Text_is_written_readably()
    {
        var line = Write(Sample()).Split('\n')[7];

        Assert.Contains("Go \\\"now\\\" é", line);
    }

    [Fact]
    public void Nodes_out_of_order_are_refused_with_the_line()
    {
        var text = "{\"platform\":\"html\"}\n{\"n\":1,\"p\":0}\n{\"n\":3,\"p\":1}\n";

        var e = Assert.Throws<FormatException>(() => SnapshotFile.Read(new StringReader(text)));

        Assert.Contains("Line 3", e.Message);
    }

    [Fact]
    public void A_line_that_is_not_json_is_refused_with_the_line()
    {
        var e = Assert.Throws<FormatException>(() => SnapshotFile.Read(new StringReader("{\"platform\":\"html\"}\nnot json\n")));

        Assert.Contains("Line 2", e.Message);
    }

    [Fact]
    public void Children_and_indexing_follow_the_numbers()
    {
        var snapshot = Sample();

        Assert.Equal("button", snapshot[2].Type);
        Assert.Equal([2, 3, 5], snapshot.ChildrenOf(1).Select(n => n.Number));
    }

    [Theory]
    [InlineData("id", true)]
    [InlineData("data-anything", true)]
    [InlineData("aria-expanded", true)]
    [InlineData("aria-label", false)] // it is the node's Name
    [InlineData("data-v-09f01a64", false)] // Vue's scoped style
    [InlineData("value", false)]
    [InlineData("style", false)]
    public void Html_keeps_the_attributes_a_locator_or_a_pattern_needs(string name, bool kept)
    {
        Assert.Equal(kept, HtmlAttributes.IsKept(name));
    }
}
