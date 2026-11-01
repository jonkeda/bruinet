using Bruinet.Core.Scraping;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Projects;

public sealed class RecordingEventTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private ScrapeProject MakeProject()
    {
        var folder = Path.Combine(_root, "MyApp.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "MyApp.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), """
            # Scraper Config

            ## App

            | Field | Value |
            | --- | --- |
            | Platform | blazor |
            | StartUrl | https://app.example.nl/ |
            """);
        return ScrapeProject.Open(folder);
    }

    private static ScrapeSnapshot Page(string location, string title) => new()
    {
        Platform = "html",
        Location = location,
        Title = title,
        CapturedAt = DateTimeOffset.UnixEpoch,
        Nodes = [new() { Number = 1, Parent = 0, Type = "html" }],
    };

    private static RecordingEvent Action(string kind, int? node = null) => new()
    {
        Kind = kind,
        At = DateTimeOffset.UnixEpoch,
        Location = "https://app.example.nl/userform",
        Page = "/userform",
        Node = node,
    };

    [Fact]
    public void Actions_are_written_after_their_page_and_read_back_unchanged()
    {
        var project = MakeProject();
        var session = project.StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://app.example.nl/userform", "User form"));

        var input = session.AddAction(Action(RecordingEvent.InputKind, 41) with { Value = "Ada" });
        var check = session.AddAction(Action(RecordingEvent.CheckKind, 77) with { Checked = true });
        var select = session.AddAction(Action(RecordingEvent.SelectKind, 95) with { Value = "Netherlands" });
        var click = session.AddAction(Action(RecordingEvent.ClickKind) with
        {
            Path = [new() { Type = "html" }, new() { Type = "button", TestId = "submit", Role = "button", Name = "Submit", Classes = "btn primary" }],
        });
        var assert = session.AddAction(Action(RecordingEvent.AssertKind, 141) with { Check = AssertCheck.Text, Value = "Thanks, Ada" });
        var dialog = session.AddAction(Action(RecordingEvent.DialogKind) with { Value = "Leave?", Accepted = false });

        Assert.Equal([2, 3, 4, 5, 6, 7], new[] { input, check, select, click, assert, dialog }.Select(e => e.Number));
        var reopened = project.Session(session.Id)!;
        Assert.Equal(session.Events.Select(e => e with { Path = null }), reopened.Events.Select(e => e with { Path = null }));
        Assert.Equal(click.Path!, reopened.Events[4].Path!);
        Assert.All(reopened.Events.Skip(1), e => Assert.True(e.IsAction));
        Assert.False(reopened.Events[0].IsAction);
    }

    [Fact]
    public void An_action_is_written_without_the_page_fields_it_does_not_have()
    {
        var project = MakeProject();
        var session = project.StartSession("User form", DateTimeOffset.UnixEpoch);

        session.AddAction(Action(RecordingEvent.ClickKind, 5));

        var json = File.ReadAllText(Path.Combine(session.Folder, RecordingSession.FileName));
        Assert.DoesNotContain("\"nodes\"", json);
        Assert.DoesNotContain("\"snapshot\"", json);
        Assert.DoesNotContain("\"displayName\"", json);
        Assert.DoesNotContain("\"isAction\"", json);
        Assert.Contains("\"node\": 5", json);
    }

    [Fact]
    public void A_recording_written_before_actions_still_reads()
    {
        var folder = Path.Combine(_root, "old");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, RecordingSession.FileName), """
            {
              "name": "explore",
              "events": [
                {
                  "number": 1,
                  "kind": "page",
                  "at": "2026-10-01T10:07:37.2045593+00:00",
                  "location": "https://mudblazor.com/components/tabs#api",
                  "page": "/components/tabs",
                  "title": "Tabs - MudBlazor",
                  "snapshot": "001-tabs-mudblazor.nodes.jsonl",
                  "nodes": 5732,
                  "displayName": "Tabs - MudBlazor"
                }
              ]
            }
            """);

        var e = Assert.Single(RecordingSession.Open(folder, []).Events);

        Assert.Equal(RecordingEvent.PageKind, e.Kind);
        Assert.Equal(5732, e.Nodes);
        Assert.Null(e.Node);
        Assert.False(e.IsAction);
    }

    [Fact]
    public void An_action_points_into_the_latest_capture_of_its_page_before_it()
    {
        var project = MakeProject();
        var session = project.StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://app.example.nl/userform", "User form"));
        session.AddPage(Page("https://app.example.nl/other", "Other"));
        var second = session.AddPage(Page("https://app.example.nl/userform", "User form"));
        var click = session.AddAction(Action(RecordingEvent.ClickKind, 1));
        session.AddPage(Page("https://app.example.nl/userform", "User form"));

        Assert.Equal(second, session.CaptureOf(click));
        Assert.Null(session.CaptureOf(session.AddAction(Action(RecordingEvent.ClickKind, 1) with { Page = "/never" })));
    }

    [Fact]
    public void Actions_are_not_pages_in_the_index()
    {
        var project = MakeProject();
        var session = project.StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://app.example.nl/userform", "User form"));
        session.AddAction(Action(RecordingEvent.ClickKind, 1));

        Assert.Single(Assert.Single(project.Index().Pages).Captures);
    }

    [Theory]
    [InlineData(RecordingEvent.PageKind, null, null)]
    [InlineData("hover", null, null)]
    [InlineData(RecordingEvent.ClickKind, null, null)]
    [InlineData(RecordingEvent.AssertKind, null, 1)]
    [InlineData(RecordingEvent.AssertKind, "colour", 1)]
    [InlineData(RecordingEvent.AssertKind, AssertCheck.Text, null)]
    public void What_is_not_a_well_formed_action_is_refused(string kind, string? check, int? node)
    {
        var session = MakeProject().StartSession("User form", DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(() => session.AddAction(Action(kind, node) with { Check = check }));
        Assert.Empty(session.Events);
    }

    [Fact]
    public void A_click_then_the_value_or_choice_on_the_same_node_is_the_value_or_choice()
    {
        var session = MakeProject().StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://app.example.nl/userform", "User form"));

        session.AddAction(Action(RecordingEvent.ClickKind, 41));
        session.AddAction(Action(RecordingEvent.InputKind, 41) with { Value = "Ad" });
        var input = session.AddAction(Action(RecordingEvent.InputKind, 41) with { Value = "Ada" });
        session.AddAction(Action(RecordingEvent.ClickKind, 95));
        var select = session.AddAction(Action(RecordingEvent.SelectKind, 95) with { Value = "Netherlands" });
        session.AddAction(Action(RecordingEvent.ClickKind, 77));
        var check = session.AddAction(Action(RecordingEvent.CheckKind, 77) with { Checked = true });

        Assert.Equal([1, 2, 3, 4], session.Events.Select(e => e.Number));
        Assert.Equal([input, select, check], session.Events.Skip(1));
        Assert.Equal("Ada", session.Events[1].Value);
    }

    [Fact]
    public void Actions_on_other_nodes_or_with_a_capture_between_them_are_kept_apart()
    {
        var session = MakeProject().StartSession("User form", DateTimeOffset.UnixEpoch);
        session.AddPage(Page("https://app.example.nl/userform", "User form"));

        session.AddAction(Action(RecordingEvent.ClickKind, 41));
        session.AddAction(Action(RecordingEvent.InputKind, 42) with { Value = "Ada" });
        session.AddAction(Action(RecordingEvent.ClickKind, 43));
        session.AddPage(Page("https://app.example.nl/userform", "User form"));
        session.AddAction(Action(RecordingEvent.InputKind, 43) with { Value = "Ada" });
        session.AddAction(Action(RecordingEvent.ClickKind, 44));
        session.AddAction(Action(RecordingEvent.ClickKind, 44));

        Assert.Equal(["page", "click", "input", "click", "page", "input", "click", "click"], session.Events.Select(e => e.Kind));
    }

    [Fact]
    public void An_action_s_own_capture_comes_before_the_latest_capture_of_its_page()
    {
        var session = MakeProject().StartSession("User form", DateTimeOffset.UnixEpoch);
        var first = session.AddPage(Page("https://app.example.nl/userform", "User form"));
        session.AddPage(Page("https://app.example.nl/userform", "User form"));

        var click = session.AddAction(Action(RecordingEvent.ClickKind, 1) with { Capture = first.Number });

        Assert.Equal(first, session.CaptureOf(click));
    }

    [Fact]
    public void An_action_s_value_location_and_path_are_masked_and_a_secret_keeps_only_its_placeholder()
    {
        var folder = Path.Combine(_root, "Bank.UITests");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Bank.UITests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(folder, ScraperConfig.FileName), """
            ## Masks

            | Pattern | Replacement |
            | --- | --- |
            | `NL\d{2}[A-Z]{4}\d{10}` | `IBAN` |
            """);
        var session = ScrapeProject.Open(folder).StartSession("Bank", DateTimeOffset.UnixEpoch);

        var input = session.AddAction(Action(RecordingEvent.InputKind, 4) with
        {
            Location = "https://app.example.nl/accounts/NL91ABNA0417164300",
            Value = "Pay NL91ABNA0417164300",
            Path = [new() { Type = "input", Name = "Account NL91ABNA0417164300" }],
        });
        var secret = session.AddAction(Action(RecordingEvent.InputKind, 5) with { Value = "hunter2", Placeholder = "password" });

        Assert.Equal("Pay IBAN", input.Value);
        Assert.Equal("https://app.example.nl/accounts/IBAN", input.Location);
        Assert.Equal("Account IBAN", input.Path![0].Name);
        Assert.Null(secret.Value);
        Assert.Equal("password", secret.Placeholder);
        var json = File.ReadAllText(Path.Combine(session.Folder, RecordingSession.FileName));
        Assert.DoesNotContain("NL91ABNA0417164300", json);
        Assert.DoesNotContain("hunter2", json);
    }

    [Fact]
    public void A_page_assert_and_actions_that_are_not_on_an_element_need_no_node()
    {
        var session = MakeProject().StartSession("User form", DateTimeOffset.UnixEpoch);

        session.AddAction(Action(RecordingEvent.AssertKind) with { Check = AssertCheck.Page });
        session.AddAction(Action(RecordingEvent.KeyKind) with { Value = "Enter" });
        session.AddAction(Action(RecordingEvent.NavigateKind));

        Assert.Equal(3, session.Events.Count);
    }
}
