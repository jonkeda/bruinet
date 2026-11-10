using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Html;

// Headless Chromium against the test site: needs Playwright's Chromium, as the Html UI tests do.
[Trait("Category", "Browser")]
public sealed class PlaywrightScrapeSourceTests : IAsyncLifetime
{
    private readonly TestSiteServer _site = new();
    private readonly PlaywrightScrapeSource _source = new();
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_profile))
            Directory.Delete(_profile, recursive: true);
    }

    private Task StartAsync(string path) => _source.StartAsync(new ScrapeStart(_site.Url(path), _profile, Visible: false));

    // The pick's listeners are in the page: before this, a click or key would go to the page.
    private Task PickingAsync() => _source.Page!.WaitForFunctionAsync("() => !!window.__bruinetPickCancel");

    private static ScrapeNode Single(ScrapeSnapshot snapshot, Func<ScrapeNode, bool> match) => Assert.Single(snapshot.Nodes, n => match(n));

    private static bool IsUnder(ScrapeSnapshot snapshot, ScrapeNode node, int ancestor)
    {
        for (var p = node.Parent; p != 0; p = snapshot[p].Parent)
            if (p == ancestor)
                return true;
        return false;
    }

    [Fact]
    public async Task Nodes_are_numbered_in_document_order_and_a_parent_comes_before_its_children()
    {
        await StartAsync("index.html");

        var snapshot = await _source.CaptureAsync();

        Assert.Equal("html", snapshot.Platform);
        Assert.Equal(_site.Url("index.html"), snapshot.Location);
        Assert.Equal(Enumerable.Range(1, snapshot.Nodes.Count), snapshot.Nodes.Select(n => n.Number));
        Assert.All(snapshot.Nodes, n => Assert.True(n.Parent < n.Number));
        Assert.Equal("html", snapshot[1].Type);
        Assert.DoesNotContain(snapshot.Nodes, n => n.Type is "head" or "script" or "style");
    }

    [Fact]
    public async Task A_frame_s_nodes_hang_under_its_iframe()
    {
        await StartAsync("pages/checkout.html");

        var snapshot = await _source.CaptureAsync();

        var iframe = Single(snapshot, n => n.Attribute("data-testid") == "checkout-payment-frame");
        Assert.Equal(_site.Url("frames/payment-frame.html"), iframe.Frame);
        var inner = Assert.Single(snapshot.ChildrenOf(iframe.Number));
        Assert.Equal("html", inner.Type);
        Assert.Contains(snapshot.Nodes, n => n.Type == "input" && IsUnder(snapshot, n, iframe.Number));
    }

    [Fact]
    public async Task A_cross_origin_frame_is_captured_too()
    {
        await StartAsync("pages/checkout.html");
        await _source.Page!.EvaluateAsync(
            "url => new Promise(r => { const f = document.createElement('iframe'); f.src = url; f.setAttribute('data-testid', 'other-origin'); f.onload = r; document.body.appendChild(f); })",
            _site.OtherOriginUrl("frames/profile-widget.html"));

        var snapshot = await _source.CaptureAsync();

        var iframe = Single(snapshot, n => n.Attribute("data-testid") == "other-origin");
        Assert.StartsWith("http://127.0.0.1:", iframe.Frame);
        Assert.Contains(snapshot.Nodes, n => n.Type == "body" && IsUnder(snapshot, n, iframe.Number));
    }

    [Fact]
    public async Task A_frame_in_a_frame_is_captured()
    {
        await StartAsync("pages/support.html");

        var snapshot = await _source.CaptureAsync();

        var outer = Single(snapshot, n => n.Attribute("data-testid") == "support-nested-frame");
        var inner = Single(snapshot, n => n.Attribute("data-testid") == "nested-inner-frame");
        Assert.True(IsUnder(snapshot, inner, outer.Number));
        Assert.Contains(snapshot.Nodes, n => n.Type == "body" && IsUnder(snapshot, n, inner.Number));
    }

    [Fact]
    public async Task A_capture_waits_for_a_page_that_renders_late()
    {
        await StartAsync("pages/spa.html");

        var snapshot = await _source.CaptureAsync();

        Assert.Contains(snapshot.Nodes, n => n.Type == "h1" && n.Text == "Orders");
        Assert.Contains(snapshot.Nodes, n => n.Attribute("data-testid") == "orders");
    }

    [Fact]
    public async Task Hash_and_history_navigation_are_transitions()
    {
        await StartAsync("pages/spa.html");
        var transitions = new List<string>();
        _source.Transitioned += (_, t) => transitions.Add(t.Location);

        await _source.Page!.ClickAsync("[data-testid=nav-order]");
        await _source.Page.WaitForURLAsync("**#/orders/42");
        await _source.Page.ClickAsync("[data-testid=nav-settings]");
        await _source.Page.WaitForURLAsync("**/settings");
        var snapshot = await _source.CaptureAsync();

        Assert.Equal([_site.Url("pages/spa.html#/orders/42"), _site.Url("pages/settings")], transitions);
        Assert.Contains(snapshot.Nodes, n => n.Type == "h1" && n.Text == "Settings");
        Assert.Equal("/pages/settings", PageKey.Of(snapshot.Location));
    }

    [Fact]
    public async Task An_open_shadow_root_is_a_node_with_the_shadow_tree_under_it()
    {
        await StartAsync("pages/spa.html");

        var snapshot = await _source.CaptureAsync();

        var host = Single(snapshot, n => n.Type == "shadow-search");
        var root = Assert.Single(snapshot.ChildrenOf(host.Number));
        Assert.Equal(ScrapeNodeKind.ShadowRoot, root.Kind);
        var input = Single(snapshot, n => n.Type == "input" && n.Parent == root.Number);
        Assert.Equal("Search", input.Name);
        Assert.Equal("Search orders", input.Attribute("placeholder"));
    }

    [Fact]
    public async Task A_dialog_opened_at_the_end_of_body_is_captured()
    {
        await StartAsync("pages/spa.html#/orders/42");
        await _source.Page!.ClickAsync("[data-testid=open-dialog]");

        var snapshot = await _source.CaptureAsync();

        var dialog = Single(snapshot, n => n.Role == "dialog");
        Assert.Equal("Cancel order", dialog.Name);
        Assert.Equal(["Yes", "No"], snapshot.ChildrenOf(dialog.Number).Where(n => n.Type == "button").Select(n => n.Text));
    }

    [Fact]
    public async Task What_the_user_typed_is_not_recorded()
    {
        await StartAsync("pages/spa.html");
        await _source.Page!.FillAsync("shadow-search input", "secret order 7");

        var snapshot = await _source.CaptureAsync();

        Assert.DoesNotContain(snapshot.Nodes, n => n.Attributes.Values.Concat([n.Text, n.Name]).Any(v => v?.Contains("secret", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task A_highlight_outlines_the_node_in_its_frame_and_is_not_captured()
    {
        await StartAsync("pages/checkout.html");
        var snapshot = await _source.CaptureAsync();
        var iframe = Single(snapshot, n => n.Attribute("data-testid") == "checkout-payment-frame");
        var input = snapshot.Nodes.First(n => n.Type == "input" && IsUnder(snapshot, n, iframe.Number));

        await _source.HighlightAsync(input.Number);

        var frame = _source.Page!.Frames.Single(f => f.Url.EndsWith("payment-frame.html", StringComparison.Ordinal));
        Assert.True(await frame.EvaluateAsync<bool>("() => !!document.getElementById('__bruinet_highlight')"));
        Assert.False(await _source.Page.MainFrame.EvaluateAsync<bool>("() => !!document.getElementById('__bruinet_highlight')"));
        var again = await _source.CaptureAsync();
        Assert.Equal(snapshot.Nodes.Count, again.Nodes.Count);

        await _source.HighlightAsync(null);
        Assert.False(await frame.EvaluateAsync<bool>("() => !!document.getElementById('__bruinet_highlight')"));
    }

    [Fact]
    public async Task A_pick_returns_the_clicked_node_and_the_click_does_not_reach_the_page()
    {
        await StartAsync("pages/spa.html#/orders/42");
        var snapshot = await _source.CaptureAsync();
        var button = Single(snapshot, n => n.Attribute("data-testid") == "open-dialog");

        var pick = _source.PickAsync();
        await PickingAsync();
        await _source.Page!.ClickAsync("[data-testid=open-dialog]");
        var picked = await pick;

        Assert.Equal(button.Number, picked);
        Assert.Equal(0, await _source.Page.EvaluateAsync<int>("() => document.querySelectorAll('[role=dialog]').length"));
    }

    [Fact]
    public async Task A_pick_is_cancelled_by_escape()
    {
        await StartAsync("pages/spa.html");
        await _source.CaptureAsync();

        var pick = _source.PickAsync();
        await PickingAsync();
        await _source.Page!.Keyboard.PressAsync("Escape");

        Assert.Null(await pick);
    }

    [Fact]
    public async Task A_capture_and_its_screenshot_are_recorded_into_a_project()
    {
        var folder = Path.Combine(_profile, "Shop.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url("index.html")} |\n| Screenshots | true |\n");
        var project = ScrapeProject.Open(folder);
        await _source.StartAsync(new ScrapeStart(project.Config.StartUrl, Path.Combine(_profile, "browser"), Visible: false));

        var session = project.StartSession("Home", DateTimeOffset.UtcNow);
        var e = session.AddPage(await _source.CaptureAsync(), await _source.ScreenshotAsync());

        Assert.Equal("/index.html", e.Page);
        var png = File.ReadAllBytes(Path.Combine(session.Folder, e.Screenshot!));
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        Assert.Equal(e.Nodes, session.ReadSnapshot(e).Nodes.Count);
    }
}
