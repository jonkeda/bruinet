using Bruinet.Core.Scraping;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Html;

// The recorder in headless Chromium on the test site's user form: the test acts as the person.
[Trait("Category", "Browser")]
public sealed class RecorderTests : IAsyncLifetime
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private readonly TestSiteServer _site = new();
    private readonly PlaywrightScrapeSource _source = new();
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly List<ScrapeAction> _acted = [];
    private ScrapeSnapshot _snapshot = null!;

    private Microsoft.Playwright.IPage Page => _source.Page!;

    public async Task InitializeAsync()
    {
        await _source.StartAsync(new ScrapeStart(_site.Url("pages/form.html"), _profile, Visible: false));
        _source.Acted += (_, action) =>
        {
            lock (_acted)
                _acted.Add(action);
        };
        await _source.StartActionsAsync(new ScrapeActionOptions(["data-testid"], [new ScrapeSecret("api key", "apiKey")]));
        _snapshot = await _source.CaptureAsync();
    }

    public async Task DisposeAsync()
    {
        await _source.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_profile))
            Directory.Delete(_profile, recursive: true);
    }

    // The actions once `count` have been raised.
    private async Task<ScrapeAction[]> ActedAsync(int count)
    {
        var deadline = DateTime.UtcNow + Limit;
        while (DateTime.UtcNow < deadline)
        {
            lock (_acted)
            {
                if (_acted.Count >= count)
                    return [.. _acted];
            }
            await Task.Delay(25); // polling the handler's list, at a named cadence
        }
        lock (_acted)
            throw new TimeoutException($"{_acted.Count} of {count} actions: {string.Join(", ", _acted.Select(a => a.Kind))}");
    }

    private static string? TestIdOf(ScrapeSnapshot snapshot, int? node) => node is { } n ? snapshot[n].Attribute("data-testid") : null;

    private string? TestId(ScrapeAction action) => TestIdOf(action.Capture ?? _snapshot, action.Node);

    [Fact]
    public async Task Each_field_s_final_value_a_check_and_a_choice_name_nodes_of_the_last_capture()
    {
        await Page.FillAsync("[data-testid=first-name]", "Ad");
        await Page.FillAsync("[data-testid=first-name]", "Ada");
        await Page.ClickAsync("[data-testid=terms-label]");
        await Page.SelectOptionAsync("[data-testid=country]", "nl");
        await Page.ClickAsync("[data-testid=submit]");

        var acted = await ActedAsync(4);

        Assert.Equal(
            [("input", "first-name", "Ada"), ("check", "terms", null), ("select", "country", "Netherlands"), ("click", "submit", null)],
            acted.Select(a => (a.Kind, TestId(a), a.Value)));
        Assert.True(acted[1].Checked);
        Assert.All(acted, a => Assert.Equal(_snapshot.CapturedAt, a.CapturedAt));
        Assert.All(acted, a => Assert.Null(a.Capture));
        Assert.Equal("submit", acted[3].Path![^1].TestId);
        Assert.Equal("html", acted[3].Path![0].Type);
    }

    [Fact]
    public async Task A_password_or_a_secret_field_s_value_never_leaves_the_page()
    {
        await Page.FillAsync("[data-testid=password]", "hunter2");
        await Page.FillAsync("[data-testid=key]", "sk-123");
        await Page.FillAsync("[data-testid=iban]", "NL91ABNA0417164300");
        await Page.ClickAsync("[data-testid=submit]");

        var acted = await ActedAsync(4);

        Assert.Equal(
            [("password", null, "password"), ("key", null, "apiKey"), ("iban", "NL91ABNA0417164300", null)],
            acted.Take(3).Select(a => (TestId(a), a.Value, a.Secret)));
    }

    [Fact]
    public async Task An_element_added_after_the_capture_is_captured_before_its_action()
    {
        await Page.ClickAsync("[data-testid=add-note]");
        await Page.ClickAsync("[data-testid=note]");

        var acted = await ActedAsync(2);

        Assert.Null(acted[0].Capture);
        var capture = Assert.IsType<ScrapeSnapshot>(acted[1].Capture);
        Assert.Equal("note", TestIdOf(capture, acted[1].Node));
        Assert.Equal(capture.CapturedAt, acted[1].CapturedAt);
        Assert.True(capture.CapturedAt > _snapshot.CapturedAt);
    }

    [Fact]
    public async Task An_element_gone_before_it_could_be_captured_keeps_only_its_path()
    {
        await Page.ClickAsync("[data-testid=add-toast]");
        await Page.ClickAsync("[data-testid=toast]");

        var acted = await ActedAsync(2);

        Assert.Null(acted[1].Node);
        Assert.Null(acted[1].CapturedAt);
        Assert.Equal(["html", "body", "main", "form", "div", "button"], acted[1].Path!.Select(s => s.Type));
        Assert.Equal("toast", acted[1].Path![^1].TestId);
    }

    [Fact]
    public async Task An_option_of_a_list_box_is_a_choice_on_the_combobox_that_owns_it()
    {
        await Page.ClickAsync("[data-testid=colour]");
        await Page.ClickAsync("[data-testid=colour-blue]");

        var acted = await ActedAsync(2);

        Assert.Equal([("click", "colour", null), ("select", "colour", "Blue")], acted.Select(a => (a.Kind, TestId(a), a.Value)));
    }

    [Fact]
    public async Task A_custom_switch_is_checked_when_its_state_flips_after_the_click()
    {
        await Page.ClickAsync("[data-testid=notify]");

        var acted = await ActedAsync(2);

        Assert.Equal([("click", "notify"), ("check", "notify")], acted.Select(a => (a.Kind, TestId(a))));
        Assert.True(acted[1].Checked);
    }

    [Fact]
    public async Task Enter_comes_after_the_value_it_submits()
    {
        await Page.FillAsync("[data-testid=first-name]", "Ada");
        await Page.PressAsync("[data-testid=first-name]", "Enter");

        var acted = await ActedAsync(2);

        Assert.Equal([("input", "Ada"), ("key", "Enter")], acted.Take(2).Select(a => (a.Kind, a.Value)));
        Assert.Null(acted[1].Node);
    }

    [Fact]
    public async Task A_link_that_leaves_the_page_is_a_click_on_the_old_page_then_a_navigation()
    {
        await Page.ClickAsync("[data-testid=to-account]");

        var acted = await ActedAsync(2);

        Assert.Equal("to-account", TestId(acted[0]));
        Assert.Equal(_site.Url("pages/form.html"), acted[0].Location);
        Assert.Equal(ScrapeActionKind.Navigate, acted[1].Kind);
        Assert.Equal(_site.Url("pages/account.html"), acted[1].Location);
    }

    [Fact]
    public async Task A_dialog_is_recorded_with_its_text_after_the_click_that_opened_it()
    {
        await Page.ClickAsync("[data-testid=leave]");

        var acted = await ActedAsync(2);

        Assert.Equal([ScrapeActionKind.Click, ScrapeActionKind.Dialog], acted.Select(a => a.Kind));
        Assert.Equal("Leave this form?", acted[1].Value);
        Assert.False(acted[1].Accepted);
    }

    [Fact]
    public async Task Nothing_is_raised_once_actions_are_stopped()
    {
        _source.StopActions();

        await Page.ClickAsync("[data-testid=submit]");
        await _source.CaptureAsync(); // after the click's message reached the host

        lock (_acted)
            Assert.Empty(_acted);
    }
}
