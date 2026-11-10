using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Testing;

// The proposal's User Form example (.my/recording/proposal.md, section 2), from files only.
public sealed class TestResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly ScrapeProject _project;
    private readonly RecordingSession _session;

    public TestResolverTests() => (_project, _session) = UserForm.Make(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private RecordingEvent Act(string kind, int? node = null) => _session.AddAction(new RecordingEvent
    {
        Kind = kind,
        Location = "http://localhost:5180/userform",
        Page = "/userform",
        Node = node,
    });

    [Fact]
    public void The_user_form_example_resolves_to_its_members_verbs_and_phrases_and_a_gap()
    {
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.InputKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 4, Value = "Ada" });
        Act(RecordingEvent.CheckKind, 7);
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.SelectKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 8, Value = "Netherlands" });
        Act(RecordingEvent.ClickKind, 11);
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.AssertKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 12, Check = AssertCheck.Text, Value = "Thanks, Ada" });

        var test = TestResolver.Resolve(_project, _session);

        Assert.Equal("/userform", test.StartPage);
        Assert.Equal("UserFormPage", test.StartPageObject);
        Assert.Equal(
            [
                ("FirstName", "TextInputControl", "Enter", "I enter \"Ada\" into First Name"),
                ("Terms", "CheckBoxControl", "Check", "I check Terms"),
                ("Country", "SelectControl", "SelectByText", "I select \"Netherlands\" from Country"),
                ("Submit", "ButtonControl", "Click", "I tap Submit"),
                (null, null, null, null),
            ],
            test.Steps.Select(s => (s.Member, s.Control, s.Verb, s.Phrase)));
        var gap = Assert.Single(test.Gaps);
        Assert.Equal(6, gap.Event);
        Assert.Equal(GapKind.NoMember, gap.Gap!.Kind);
        Assert.Contains("node 12", gap.Gap.Message);
    }

    [Fact]
    public void A_secret_s_placeholder_stands_in_the_phrase_for_its_value()
    {
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.InputKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 5, Placeholder = "password" });

        var step = Assert.Single(TestResolver.Resolve(_project, _session).Steps);

        Assert.Equal("I enter <password> into Password", step.Phrase);
    }

    [Fact]
    public void A_part_of_a_container_resolves_through_the_container()
    {
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.SelectKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 14, Value = "Open" });

        var step = Assert.Single(TestResolver.Resolve(_project, _session).Steps);

        Assert.Equal(("Filters.Status", "SelectByText", "I select \"Open\" from Status"), (step.Member, step.Verb, step.Phrase));
    }

    [Fact]
    public void A_navigation_is_a_page_check_or_a_gap_when_the_page_has_no_page_object()
    {
        Act(RecordingEvent.NavigateKind);
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.NavigateKind, Location = "http://localhost:5180/done", Page = "/done" });

        var steps = TestResolver.Resolve(_project, _session).Steps;

        Assert.Equal("I should be on the User Form page", steps[0].Phrase);
        Assert.Equal(GapKind.NoPageObject, steps[1].Gap!.Kind);
    }

    [Fact]
    public void What_has_no_verb_no_node_or_no_control_verb_is_a_gap_with_its_reason()
    {
        Act(RecordingEvent.ClickKind, 15);
        _session.AddAction(new RecordingEvent
        {
            Kind = RecordingEvent.ClickKind,
            Location = "http://localhost:5180/userform",
            Page = "/userform",
            Path = [new ScrapePathStep { Type = "html" }, new ScrapePathStep { Type = "button", TestId = "toast" }],
        });
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.KeyKind, Location = "http://localhost:5180/userform", Page = "/userform", Value = "Enter" });
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.InputKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 10, Value = "x" });

        var gaps = TestResolver.Resolve(_project, _session).Steps.Select(s => s.Gap!).ToArray();

        Assert.Equal([GapKind.NoVerb, GapKind.NoNode, GapKind.NoVerb, GapKind.NoVerb], gaps.Select(g => g.Kind));
        Assert.Contains("MysteryWidget", gaps[0].Message);
        Assert.Contains("button[toast]", gaps[1].Message);
        Assert.Contains("Enter", gaps[2].Message);
        Assert.Contains("ButtonControl", gaps[3].Message);
    }
}
