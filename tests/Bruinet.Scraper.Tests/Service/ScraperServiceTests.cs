using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.TestSite;

namespace Bruinet.Scraper.Tests.Service;

// A recording of the test site through the service, headless.
[Trait("Category", "Browser")]
public sealed class ScraperServiceTests : IAsyncLifetime
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private readonly TestSiteServer _site = new();
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private ScraperService _service = null!;
    private PlaywrightScrapeSource? _source;

    // The recording browser's page, for the test to act as the user.
    private Microsoft.Playwright.IPage Browser => _source!.Page!;

    public Task InitializeAsync()
    {
        MakeProject("Shop.UITests", _site.Url("pages/spa.html"));
        _service = new ScraperService(_solution, _ => _source = new PlaywrightScrapeSource());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _service.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    private void MakeProject(string name, string startUrl)
    {
        var folder = Path.Combine(_solution, "tests", name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {startUrl} |\n");
    }

    // Completes when the service has captured `count` pages in all.
    private Task<RecordingEventCount> CapturedAsync(int count)
    {
        var done = new TaskCompletionSource<RecordingEventCount>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Changed += (_, change) =>
        {
            if (change.Kind == ScraperChangeKind.Captured && change.Recording?.Captures >= count)
                done.TrySetResult(new(change.Recording.Captures));
            if (change.Kind == ScraperChangeKind.Failed)
                done.TrySetException(new InvalidOperationException(change.Message));
        };
        return done.Task.WaitAsync(Limit);
    }

    private sealed record RecordingEventCount(int Captures);

    [Fact]
    public async Task A_recording_captures_the_start_page_and_each_page_the_app_moves_to()
    {
        var second = CapturedAsync(2);
        var state = await _service.StartRecordingAsync(null, "Orders", visible: false);
        Assert.Equal(1, _service.Active!.Captures);

        await _service.CapturePageAsync(); // unchanged: not captured again
        Assert.Equal(1, _service.Active!.Captures);

        await Browser.ClickAsync("[data-testid=nav-order]");
        await second;
        await _service.StopRecordingAsync();

        Assert.Null(_service.Active);
        Assert.Equal("Shop.UITests", state.Project);
        Assert.Equal(["/pages/spa.html", "/pages/spa.html#/orders/{id}"], _service.Pages(null).Select(p => p.Key));
        Assert.True(Directory.Exists(state.Folder));
    }

    // Completes when the session has `count` events, pages and actions together.
    private Task EventsAsync(int count)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Changed += (_, change) =>
        {
            if (change.Recording?.Captures >= count)
                done.TrySetResult();
            if (change.Kind == ScraperChangeKind.Failed)
                done.TrySetException(new InvalidOperationException(change.Message));
        };
        return done.Task.WaitAsync(Limit);
    }

    [Fact]
    public async Task A_test_recording_keeps_the_actions_in_order_each_in_the_capture_of_its_node()
    {
        var note = EventsAsync(6);
        var all = EventsAsync(9);
        var state = await _service.StartRecordingAsync(null, "User form", _site.Url("pages/form.html"), visible: false, actions: true);
        Assert.True(state.Actions);

        await Browser.FillAsync("[data-testid=first-name]", "Ada");
        await Browser.ClickAsync("[data-testid=colour]");
        await Browser.ClickAsync("[data-testid=colour-red]");
        await Browser.ClickAsync("[data-testid=add-note]");
        await Browser.ClickAsync("[data-testid=note]");
        await note; // a person does not leave the page faster than the note's click is recorded
        await Browser.ClickAsync("[data-testid=to-account]");
        await all;
        await _service.StopRecordingAsync();

        var session = ScrapeProject.Find(Path.Combine(_solution, "tests", "Shop.UITests"))!.Session(state.Session)!;
        Assert.Equal(
            ["page", "input", "select", "click", "page", "click", "click", "navigate", "page"],
            session.Events.Select(e => e.Kind));
        string? TestId(RecordingEvent e) => session.CaptureOf(e) is { } capture ? session.ReadSnapshot(capture)[e.Node!.Value].Attribute("data-testid") : null;
        Assert.Equal(["first-name", "colour", "add-note", "note", "to-account"], session.Events.Where(e => e.Node is not null).Select(TestId));
        Assert.Equal(5, session.CaptureOf(session.Events[5])!.Number); // the note was captured for its click
        Assert.Equal("/pages/account.html", session.Events[^1].Page);
    }

    // The person's moves in the recording browser for an assert: Assert on the toolbar, an element, a check.
    private async Task AssertOnAsync(string element, string check)
    {
        await Browser.ClickAsync("#__bruinet_toolbar [data-command=assert]");
        await Browser.WaitForFunctionAsync("() => !!window.__bruinetPickCancel");
        await Browser.ClickAsync(element);
        await Browser.WaitForFunctionAsync("() => !!window.__bruinetChooseCancel");
        await Browser.ClickAsync($"#__bruinet_toolbar [data-check={check}]");
    }

    private RecordingSession Session(RecordingState state) => ScrapeProject.Find(state.Folder)!.Session(state.Session)!;

    [Fact]
    public async Task An_assert_from_the_toolbar_records_the_check_the_person_chose_on_the_element_they_clicked()
    {
        var asserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var asserts = 0;
        _service.Changed += (_, change) =>
        {
            if (change.Captured?.Kind == RecordingEvent.AssertKind && Interlocked.Increment(ref asserts) == 4)
                asserted.TrySetResult();
            if (change.Kind == ScraperChangeKind.Failed)
                asserted.TrySetException(new InvalidOperationException(change.Message));
        };
        var state = await _service.StartRecordingAsync(null, "Asserts", _site.Url("pages/form.html"), visible: false, actions: true);
        await Browser.FillAsync("[data-testid=first-name]", "Ada");
        await Browser.ClickAsync("[data-testid=submit]");

        await AssertOnAsync("[data-testid=confirmation]", "text");
        await AssertOnAsync("[data-testid=terms]", "checked");
        await AssertOnAsync("[data-testid=first-name]", "value");
        await AssertOnAsync("[data-testid=form-title]", "page");
        await asserted.Task.WaitAsync(Limit);
        await _service.StopRecordingAsync();

        var session = Session(state);
        var recorded = session.Events.Where(e => e.Kind == RecordingEvent.AssertKind).ToArray();
        Assert.Equal(
            [(AssertCheck.Text, "confirmation", "Thanks, Ada", null), (AssertCheck.Checked, "terms", null, false), (AssertCheck.Value, "first-name", "Ada", null), (AssertCheck.Page, null, null, null)],
            recorded.Select(a => (a.Check, a.Node is { } n ? session.ReadSnapshot(session.CaptureOf(a)!)[n].Attribute("data-testid") : null, a.Value, a.Checked)));
        Assert.DoesNotContain(session.Events, e => e.Kind == RecordingEvent.ClickKind && e.Node is { } n && session.ReadSnapshot(session.CaptureOf(e)!)[n].Attribute("data-testid") is "confirmation" or "form-title");
    }

    [Fact]
    public async Task The_toolbar_counts_the_actions_is_not_captured_and_stops_the_recording()
    {
        var two = EventsAsync(3);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.Changed += (_, change) => { if (change.Kind == ScraperChangeKind.Stopped) stopped.TrySetResult(); };
        var state = await _service.StartRecordingAsync(null, "Toolbar", _site.Url("pages/form.html"), visible: false, actions: true);

        await Browser.FillAsync("[data-testid=first-name]", "Ada");
        await Browser.ClickAsync("[data-testid=submit]");
        await two;
        await Browser.WaitForFunctionAsync("() => document.getElementById('__bruinet_toolbar').shadowRoot.querySelector('[data-count]').textContent === '2'");
        await Browser.ClickAsync("#__bruinet_toolbar [data-command=stop]");
        await stopped.Task.WaitAsync(Limit);

        var session = Session(state);
        Assert.Null(_service.Active);
        Assert.Equal(["page", "input", "click"], session.Events.Select(e => e.Kind));
        Assert.DoesNotContain(session.ReadSnapshot(session.Events[0]).Nodes, n => n.Attribute("id") == "__bruinet_toolbar");
    }

    [Fact]
    public async Task Escape_while_choosing_a_check_records_nothing()
    {
        var state = await _service.StartRecordingAsync(null, "Cancelled", _site.Url("pages/form.html"), visible: false, actions: true);
        var assert = _service.AssertAsync();
        await Browser.WaitForFunctionAsync("() => !!window.__bruinetPickCancel");
        await Browser.ClickAsync("[data-testid=form-title]");
        await Browser.WaitForFunctionAsync("() => !!window.__bruinetChooseCancel");
        await Browser.Keyboard.PressAsync("Escape");

        Assert.Null(await assert.WaitAsync(Limit));
        await _service.StopRecordingAsync();
        Assert.DoesNotContain(Session(state).Events, e => e.IsAction);
    }

    [Fact]
    public async Task A_recording_of_a_login_form_holds_no_password_and_no_secret()
    {
        File.AppendAllText(Path.Combine(_solution, "tests", "Shop.UITests", ScraperConfig.FileName), """

            ## Secrets

            | Field |
            | --- |
            | API key |

            ## Masks

            | Pattern | Replacement |
            | --- | --- |
            | `NL\d{2}[A-Z]{4}\d{10}` | `IBAN` |
            """);
        var all = EventsAsync(5);
        var state = await _service.StartRecordingAsync(null, "Login", _site.Url("pages/form.html"), visible: false, actions: true);

        await Browser.FillAsync("[data-testid=password]", "hunter2");
        await Browser.FillAsync("[data-testid=key]", "sk-123");
        await Browser.FillAsync("[data-testid=iban]", "NL91ABNA0417164300");
        await Browser.ClickAsync("[data-testid=submit]");
        await all;
        await _service.StopRecordingAsync();

        var text = string.Concat(Directory.GetFiles(state.Folder).Select(File.ReadAllText));
        Assert.DoesNotContain("hunter2", text);
        Assert.DoesNotContain("sk-123", text);
        Assert.DoesNotContain("NL91ABNA0417164300", text);
        var session = ScrapeProject.Find(state.Folder)!.Session(state.Session)!;
        Assert.Equal(["password", "apiKey", null], session.Events.Skip(1).Take(3).Select(e => e.Placeholder));
        Assert.Equal("IBAN", session.Events[3].Value);
    }

    [Fact]
    public async Task A_page_outside_the_app_is_not_captured()
    {
        var second = CapturedAsync(2);
        await _service.StartRecordingAsync(null, "Away", visible: false);

        await Browser.GotoAsync(_site.OtherOriginUrl("index.html"));
        await Browser.GotoAsync(_site.Url("index.html"));
        await second;

        Assert.DoesNotContain(_service.Pages(null), p => p.Latest.Event.Location.Contains("127.0.0.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_named_capture_is_kept_even_when_nothing_changed()
    {
        await _service.StartRecordingAsync(null, "Named", autoCapture: false, visible: false);

        var first = await _service.CapturePageAsync("Order list");
        var again = await _service.CapturePageAsync("Order list again");

        Assert.Equal("Order list", first!.Name);
        Assert.NotNull(again);
        Assert.Equal(2, _service.Active!.Captures);
    }

    [Fact]
    public async Task Only_one_recording_at_a_time()
    {
        await _service.StartRecordingAsync(null, "One", autoCapture: false, visible: false);

        var e = await Assert.ThrowsAsync<ScraperProblem>(() => _service.StartRecordingAsync(null, "Two", visible: false));

        Assert.Contains("in progress", e.Message);
    }

    [Fact]
    public async Task What_is_recorded_can_be_read_searched_and_compared()
    {
        await _service.StartRecordingAsync(null, "Read", visible: false);
        await _service.StopRecordingAsync();

        var (capture, snapshot) = _service.Snapshot(null, "/pages/spa.html");
        var table = snapshot.Nodes.Single(n => n.Attribute("data-testid") == "orders");
        var rows = snapshot.Nodes.Where(n => n.Type == "tr").ToArray();
        var found = _service.Search(null, "Grace");
        var similar = _service.Similar(null, "/pages/spa.html", rows[1].Number);

        Assert.Equal(1, capture.Event.Number);
        Assert.Contains("[data-testid=orders]", SnapshotOutline.Line(table));
        Assert.Contains(found, f => f.Line.Contains("\"Grace\"", StringComparison.Ordinal));
        Assert.Equal(rows.Skip(1).Select(r => r.Number), similar.Select(s => s.Node)); // the body rows, not the header row
    }

    [Fact]
    public void A_project_is_named_when_there_are_several()
    {
        MakeProject("Admin.UITests", _site.Url("index.html"));

        var e = Assert.Throws<ScraperProblem>(() => _service.Project(null));

        Assert.Contains("Admin.UITests, Shop.UITests", e.Message);
        Assert.Equal("Admin.UITests", _service.Project("admin.uitests").Name);
    }
}
