using Bruinet.Core.Scraping;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Tests.Service;

public sealed class SnapshotOutlineTests
{
    private static ScrapeNode Node(int n, int p, string type, string? text = null, params (string Key, string Value)[] attributes) => new()
    {
        Number = n,
        Parent = p,
        Type = type,
        Text = text,
        Attributes = attributes.ToDictionary(a => a.Key, a => a.Value),
    };

    private static ScrapeSnapshot Snapshot(params ScrapeNode[] nodes) => new() { Platform = "html", Location = "http://app/", Nodes = nodes };

    [Fact]
    public void Wrappers_with_nothing_of_their_own_are_left_out()
    {
        var snapshot = Snapshot(
            Node(1, 0, "html"),
            Node(2, 1, "body"),
            Node(3, 2, "div"),
            Node(4, 3, "div"),
            Node(5, 4, "button", "Save", ("data-testid", "save"), ("class", "btn primary")));

        var outline = SnapshotOutline.Render(snapshot);

        Assert.Equal("#5 button \"Save\" .btn.primary [data-testid=save]\n", outline);
    }

    [Fact]
    public void A_wrapper_asked_for_is_shown()
    {
        var snapshot = Snapshot(Node(1, 0, "div"), Node(2, 1, "div"), Node(3, 2, "button", "Save"));

        Assert.Equal("#2 div\n  #3 button \"Save\"\n", SnapshotOutline.Render(snapshot, focus: 2));
    }

    [Fact]
    public void Children_are_indented_under_what_is_shown()
    {
        var snapshot = Snapshot(
            Node(1, 0, "form", null, ("id", "login")),
            Node(2, 1, "div"),
            Node(3, 2, "label", "Email"),
            Node(4, 2, "input", null, ("type", "email")),
            new ScrapeNode { Number = 5, Parent = 1, Type = "my-widget" },
            new ScrapeNode { Number = 6, Parent = 5, Kind = ScrapeNodeKind.ShadowRoot });

        var outline = SnapshotOutline.Render(snapshot);

        Assert.Equal(
            "#1 form #login\n  #3 label \"Email\"\n  #4 input [type=email]\n  #5 my-widget\n    #6 (shadow root)\n",
            outline);
    }

    [Fact]
    public void A_subtree_can_be_asked_for_and_the_budget_is_kept()
    {
        var nodes = new List<ScrapeNode> { Node(1, 0, "ul") };
        for (var i = 2; i <= 50; i++)
            nodes.Add(Node(i, 1, "li", $"Item {i}"));
        var snapshot = Snapshot([.. nodes]);

        var outline = SnapshotOutline.Render(snapshot, root: 1, maxLines: 5);

        var lines = outline.TrimEnd('\n').Split('\n');
        Assert.Equal(6, lines.Length);
        Assert.Equal("#1 ul", lines[0]);
        Assert.StartsWith("… more nodes", lines[5]);
    }

    [Fact]
    public void A_signature_ignores_text_ids_and_generated_classes()
    {
        var a = Snapshot(Node(1, 0, "div", "Ada", ("class", "card css-1x2y3z"), ("id", "c1")), Node(2, 1, "img"), Node(3, 1, "h3"));
        var b = Snapshot(Node(1, 0, "div", "Grace", ("class", "css-9q8w7e card"), ("id", "c2")), Node(2, 1, "img"), Node(3, 1, "h3"));
        var c = Snapshot(Node(1, 0, "div", "Ada", ("class", "card")), Node(2, 1, "img"));

        Assert.Equal(NodeSignature.Of(a, a[1]), NodeSignature.Of(b, b[1]));
        Assert.NotEqual(NodeSignature.Of(a, a[1]), NodeSignature.Of(c, c[1]));
    }
}
