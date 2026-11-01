using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Testing;

public sealed class TestBriefTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));
    private readonly ScrapeProject _project;
    private readonly RecordingSession _session;

    public TestBriefTests()
    {
        (_project, _session) = UserForm.Make(_root);
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.ClickKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 11 });
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.AssertKind, Location = "http://localhost:5180/userform", Page = "/userform", Node = 12, Check = AssertCheck.Text, Value = "Thanks, Ada" });
        _session.AddAction(new RecordingEvent { Kind = RecordingEvent.KeyKind, Location = "http://localhost:5180/userform", Page = "/userform", Value = "Enter" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_missing_member_s_brief_shows_the_element_the_file_and_the_member_the_rules_would_write()
    {
        var test = TestResolver.Resolve(_project, _session);

        var brief = TestBrief.Write(_project, _session, test, 3);

        Assert.Contains("# Gap: event 3 of User form", brief);
        Assert.Contains("assert, check text, value \"Thanks, Ada\"", brief);
        Assert.Contains("`UserFormPage` (PageObjects/UserFormPage.cs)", brief);
        Assert.Contains("Node 12 of", brief);
        Assert.Contains("public LabelControl<UserFormPage>", brief);
        Assert.Contains("assert its text (ITextControlObject)", brief);
        Assert.Contains("`run_test` once", brief);
    }

    [Fact]
    public void A_gap_no_control_verb_closes_says_so_and_asks_for_no_member()
    {
        var test = TestResolver.Resolve(_project, _session);

        var brief = TestBrief.Write(_project, _session, test, 4);

        Assert.Contains("No control verb does this", brief);
        Assert.DoesNotContain("The rules would write", brief);
    }

    [Fact]
    public void Only_a_gap_has_a_brief_and_the_first_ones_go_in_one_prompt()
    {
        var test = TestResolver.Resolve(_project, _session);

        Assert.Throws<ArgumentException>(() => TestBrief.Write(_project, _session, test, 2));
        Assert.Equal(2, TestBrief.WriteFirst(_project, _session, test).Count);
    }
}
