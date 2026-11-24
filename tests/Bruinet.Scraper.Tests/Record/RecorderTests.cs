using Bruinet.Scraper.Places;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Record;
using Bruinet.Scraper.Service;
using Bruinet.Scraper.TestSite;
using Microsoft.Playwright;

namespace Bruinet.Scraper.Tests.Record;

// The Recorder on the test site, with Playwright as the person: it clicks and types, the Recorder only listens to
// the watcher in the page. The page is a browser here and a webview in the app; the Recorder sees no difference.
[Trait("Category", "Browser")]
public sealed class RecorderTests : IAsyncLifetime
{
    private readonly TestSiteServer _site = new();
    private readonly string _solution = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private PersonsPage _person = null!;
    private Recorder _recorder = null!;

    private string Folder => Path.Combine(_solution, "tests", "Shop.UITests");

    private string Account => _site.Url("pages/account.html");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Shop.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(Folder, ScraperConfig.FileName), $"## App\n\n| Field | Value |\n| --- | --- |\n| StartUrl | {_site.Url("index.html")} |\n");
        _person = await PersonsPage.OpenAsync(Account);
        _recorder = new Recorder(_person);
    }

    public async Task DisposeAsync()
    {
        await _recorder.DisposeAsync();
        await _person.DisposeAsync();
        _site.Dispose();
        if (Directory.Exists(_solution))
            Directory.Delete(_solution, recursive: true);
    }

    [Fact]
    public async Task A_recording_holds_the_page_it_started_on_and_each_press_with_the_page_after_it()
    {
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);

        await _person.Page.ClickAsync("[data-testid=tab-history]");
        await PagesAsync(2);
        await _person.Page.ClickAsync("[data-testid=order-details]");
        await PagesAsync(3);
        var session = await _recorder.StopAsync();

        Assert.NotNull(session);
        var events = RecordingSession.Open(session.Folder, []).Events;
        Assert.Equal(["page", "press", "page", "press", "page"], events.Select(e => e.Kind));
        var press = events[1];
        Assert.Equal(("History", "tab", "css:[data-testid='tab-history']", "/pages/account.html"), (press.Name, press.Role, press.Target, press.Page));
        // The press points at the tab's own node in the capture before it.
        var pressed = session.ReadSnapshot(events[0])[press.Node];
        Assert.Equal("tab-history", pressed.Attribute("data-testid"));
        Assert.Equal("Order details", events[3].Name);
        Assert.All(events.Where(e => e.Kind == RecordingEvent.PressKind), e => Assert.Null(e.Snapshot));
    }

    [Fact]
    public async Task What_a_person_does_before_start_and_after_stop_is_in_no_file()
    {
        await _person.Page.ClickAsync("[data-testid=tab-history]");
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);
        var session = await _recorder.StopAsync();
        await _person.Page.ClickAsync("[data-testid=order-details]");
        await _person.Page.Locator("[data-testid=order-dialog][open]").WaitForAsync();
        await _recorder.IdleAsync();

        var events = RecordingSession.Open(session!.Folder, []).Events;
        Assert.Equal(["page"], events.Select(e => e.Kind));
        Assert.False(_recorder.Recording);
    }

    [Fact]
    public async Task A_link_to_another_page_is_a_press_and_then_that_page()
    {
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);
        await _person.Page.ClickAsync("[data-testid=nav-catalog]");
        await PagesAsync(2);
        var session = await _recorder.StopAsync();

        var events = session!.Events;
        Assert.Equal(("press", "Catalog", "link"), (events[1].Kind, events[1].Name, events[1].Role));
        Assert.Equal("/pages/catalog.html", events[^1].Page);
    }

    [Fact]
    public async Task A_typed_value_is_in_a_capture_only_with_the_switch_on()
    {
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);
        await _person.Page.FillAsync("[data-testid=profile-name]", "Ada");
        await _person.Page.ClickAsync("[data-testid=tab-history]");
        await PagesAsync(2);

        await _recorder.SetRecordValuesAsync(true);
        await _person.Page.ClickAsync("[data-testid=tab-overview]");
        await PagesAsync(3);
        var session = await _recorder.StopAsync();

        var captures = session!.Events.Where(e => e.Snapshot is not null).Select(session.ReadSnapshot).ToList();
        string? Value(int capture) => captures[capture].Nodes.Single(n => n.Attribute("data-testid") == "profile-name").Attribute("value");
        Assert.Null(Value(0));
        Assert.Null(Value(1));
        Assert.Equal("Ada", Value(2));
        // Typing is no event: only the presses and the pages are.
        Assert.DoesNotContain(session.Events, e => e.Kind is not (RecordingEvent.PageKind or RecordingEvent.PressKind));
    }

    [Fact]
    public async Task The_places_a_person_reached_are_seeded_with_the_press_that_led_there()
    {
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);
        await _person.Page.ClickAsync("[data-testid=tab-history]");
        await PagesAsync(2);
        await _person.Page.ClickAsync("[data-testid=order-details]");
        await PagesAsync(3);
        await _person.Page.ClickAsync("[data-testid=order-close]");
        await PagesAsync(4);
        await _person.Page.ClickAsync("[data-testid=nav-catalog]");
        await PagesAsync(5);
        var session = await _recorder.StopAsync();

        await using var service = new ScraperService(_solution, _ => throw new InvalidOperationException("Seeding opens no browser."));
        var list = new PlacesList();
        service.SeedPlaces(null, list);

        var tab = list["/pages/account.html › History"];
        Assert.NotNull(tab);
        Assert.Equal((PlaceKind.Tab, OpenerKind.Tab, "'History' on /pages/account.html"), (tab.Kind, tab.Opener, tab.Via));
        Assert.StartsWith(session!.Id + "/", tab.Snapshot, StringComparison.Ordinal);
        var dialog = list["/pages/account.html › Order details"];
        Assert.NotNull(dialog);
        Assert.Equal((PlaceKind.Dialog, OpenerKind.Button), (dialog.Kind, dialog.Opener));
        var catalog = list["/pages/catalog.html"];
        Assert.NotNull(catalog);
        Assert.Equal((OpenerKind.Link, "'Catalog' on /pages/account.html"), (catalog.Opener, catalog.Via));
        // A capture a place points at is one `places read` can read again.
        Assert.Equal(3, service.KeptPlaces(null, list).Count);
    }

    // An app that shows its pages in a frame of its own site (Exact Online's MenuPortal): the address stays the
    // portal's, and the press, the dialog and the next page are all inside the frame.
    [Fact]
    public async Task A_press_inside_a_frame_of_the_same_site_is_recorded_with_what_it_showed_and_where_the_frame_went()
    {
        var portal = _site.Url("pages/portal.html");
        await _person.Page.GotoAsync(portal);
        var frame = _person.Page.FrameLocator("[data-testid=portal-main]");
        await frame.Locator("[data-testid=order-details]").WaitForAsync();
        await _recorder.StartAsync(Folder, portal);
        await PagesAsync(1);

        await frame.Locator("[data-testid=order-details]").ClickAsync();
        await PagesAsync(2);
        await frame.Locator("[data-testid=order-close]").ClickAsync();
        await PagesAsync(3);
        await frame.Locator("[data-testid=nav-catalog]").ClickAsync();
        await PagesAsync(4);
        var session = await _recorder.StopAsync();

        var events = session!.Events;
        Assert.Equal(["page", "press", "page", "press", "page", "press", "page"], events.Select(e => e.Kind));
        Assert.Equal(("Order details", "button", "css:[data-testid='order-details']"), (events[1].Name, events[1].Role, events[1].Target));
        // The press points at the button's node, which is one of the frame's nodes in the capture of the whole page.
        var before = session.ReadSnapshot(events[0]);
        Assert.Equal("order-details", before[events[1].Node].Attribute("data-testid"));
        // The dialog the press opened is in the capture after it.
        Assert.Contains(session.ReadSnapshot(events[2]).Nodes, n => n.Type == "dialog" && n.Attributes.ContainsKey("open"));
        // The link took the frame to another page; the portal's own address did not change.
        var last = session.ReadSnapshot(events[^1]);
        Assert.EndsWith("/pages/portal.html", last.Location, StringComparison.Ordinal);
        Assert.EndsWith("/pages/catalog.html", last.Nodes.Single(n => n.Attribute("data-testid") == "portal-main").Frame, StringComparison.Ordinal);

        // Each capture is named after the page the frame shows, with the portal as its shell: a page of its own.
        Assert.Equal(["/pages/account.html", "/pages/account.html", "/pages/account.html", "/pages/account.html", "/pages/account.html", "/pages/account.html", "/pages/catalog.html"], events.Select(e => e.Page));
        Assert.Equal(("Account", "Phase 14 Demo Site - Catalog"), (events[0].Title, events[^1].Title));
        Assert.All(events.Where(e => e.Kind == RecordingEvent.PageKind), e => Assert.EndsWith("/pages/portal.html", e.Shell, StringComparison.Ordinal));

        // So the places are the frame's pages, each reached by its press, and not parts of one portal page.
        await using var service = new ScraperService(_solution, _ => throw new InvalidOperationException("Seeding opens no browser."));
        var list = new PlacesList();
        service.SeedPlaces(null, list);
        Assert.Equal(PlaceKind.Dialog, list["/pages/account.html › Order details"]?.Kind);
        Assert.Equal("'Catalog' on /pages/account.html", list["/pages/catalog.html"]?.Via);
        Assert.Null(list["/pages/portal.html"]);
    }

    [Fact]
    public async Task Waiting_ends_when_the_person_stops()
    {
        var waiting = _recorder.WaitEndedAsync(TimeSpan.FromMinutes(1));
        await _recorder.StartAsync(Folder, Account);
        await PagesAsync(1);
        Assert.False(waiting.IsCompleted);
        await _recorder.StopAsync();
        Assert.True(await waiting);
    }

    // Waits for what the Recorder says it wrote: the watcher posts when the page went still, not when a click returned.
    private async Task PagesAsync(int pages)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(object? sender, EventArgs e)
        {
            if (_recorder.Pages >= pages)
                reached.TrySetResult();
        }

        _recorder.Changed += Check;
        try
        {
            Check(null, EventArgs.Empty);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            Assert.Fail($"The recording has {_recorder.Pages} pages after 30 s, not {pages}. Problem: {_recorder.Problem ?? "none"}.");
        }
        finally
        {
            _recorder.Changed -= Check;
        }
    }

    // A browser a person would sit at: Playwright does the clicking, and hands the watcher's messages on.
    private sealed class PersonsPage : IRecorderPage, IAsyncDisposable
    {
        private IPlaywright _playwright = null!;
        private IBrowser _browser = null!;

        public IPage Page { get; private set; } = null!;

        public event EventHandler<string>? Message;

        public static async Task<PersonsPage> OpenAsync(string address)
        {
            var person = new PersonsPage { _playwright = await Playwright.CreateAsync() };
            person._browser = await person._playwright.Chromium.LaunchAsync(new() { Headless = true });
            var context = await person._browser.NewContextAsync();
            await context.ExposeFunctionAsync("__bruinetPost", (string message) => person.Message?.Invoke(person, message));
            await context.AddInitScriptAsync(RecorderScripts.Watcher("message => window.__bruinetPost(message)"));
            person.Page = await context.NewPageAsync();
            await person.Page.GotoAsync(address);
            return person;
        }

        public async Task RunAsync(string script, CancellationToken cancellationToken = default)
        {
            try
            {
                await Page.EvaluateAsync(script);
            }
            catch (PlaywrightException)
            {
                // The page is navigating: its next document says hello and is told then.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _browser.DisposeAsync();
            _playwright.Dispose();
        }
    }
}
