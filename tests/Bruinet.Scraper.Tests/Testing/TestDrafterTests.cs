using Bruinet.Scraper.Code;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Testing;

public sealed class TestDrafterTests : IDisposable
{
    private const string TestsConfig = "\n## Tests\n\n| Field | Value |\n| --- | --- |\n| Target | both |\n| Base | Sample.UITests.SampleTestBase |\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-scraper-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static RecordingEvent On(string kind, int? node = null, string? value = null) => new()
    {
        Kind = kind,
        Location = "http://localhost:5180/userform",
        Page = "/userform",
        Node = node,
        Value = value,
    };

    // The section 2 recording: a name, Terms, a country, Submit, and an assert no member covers.
    private (ScrapeProject Project, RecordingSession Session) Recorded(string config = TestsConfig)
    {
        var (project, session) = UserForm.Make(_root, config);
        session.AddAction(On(RecordingEvent.InputKind, 4, "Ada \"the first\""));
        session.AddAction(On(RecordingEvent.CheckKind, 7) with { Checked = true });
        session.AddAction(On(RecordingEvent.SelectKind, 8, "Netherlands"));
        session.AddAction(On(RecordingEvent.InputKind, 5) with { Placeholder = "apiKey" });
        session.AddAction(On(RecordingEvent.ClickKind, 11));
        session.AddAction(On(RecordingEvent.AssertKind, 12, "Thanks, Ada") with { Check = AssertCheck.Text });
        session.AddAction(On(RecordingEvent.NavigateKind));
        return (project, session);
    }

    private static TestDraft Draft(ScrapeProject project, RecordingSession session, TestTarget target = TestTarget.Both) =>
        TestDrafter.Draft(project, TestResolver.Resolve(project, session), new RecordedHeader(session.Id, session.Hash()), target);

    [Fact]
    public void The_xunit_draft_opens_the_first_page_calls_a_member_per_step_and_marks_each_gap()
    {
        var (project, session) = Recorded();

        var draft = Draft(project, session, TestTarget.Xunit);

        var code = Assert.Single(draft.Files, f => f.Kind == DraftFileKind.Draft);
        Assert.Equal("Tests/Recorded/UserFormTests.recorded.cs", code.Path);
        Assert.Equal($$"""
            // <recorded session="{{session.Id}}" hash="{{session.Hash()}}" />
            using Sample.UITests.PageObjects;

            namespace Sample.UITests.Tests.Recorded;

            // Drafted from a recording by rules: drafted again over this file, so change the page objects or your part instead.
            public sealed partial class UserFormTests
            {
                [Fact]
                public void User_form()
                {
                    NavigateToPage("/userform");
                    var userForm = new UserFormPage(Context);

                    userForm.FirstName.Enter("Ada \"the first\"");
                    userForm.Terms.Check();
                    userForm.Country.SelectByText("Netherlands");
                    // the secret <apiKey>: set API_KEY, or map it to a setting or test data
                    userForm.Password.Enter(System.Environment.GetEnvironmentVariable("API_KEY") ?? "");
                    userForm.Submit.Click();
                    // gap: {{TestResolver.Resolve(project, session).Gaps[0].Gap!.Message}}
                    userForm.AssertLoaded(true);
                }
            }

            """.Replace("\r\n", "\n"), code.Text);
        Assert.Empty(CodeGate.Syntax(code.Text));
        var mine = Assert.Single(draft.Files, f => f.Kind == DraftFileKind.PersonsPart);
        Assert.Equal("Tests/Recorded/UserFormTests.cs", mine.Path);
        Assert.Contains("public sealed partial class UserFormTests : Sample.UITests.SampleTestBase", mine.Text);
    }

    [Fact]
    public void The_scenario_draft_says_the_steps_in_catalog_phrases_with_the_gaps_as_prose_between_blocks()
    {
        var (project, session) = Recorded();

        var draft = Draft(project, session, TestTarget.Scenario);

        var scenario = Assert.Single(draft.Files);
        Assert.Equal("Features/Recorded/user-form.feature.md", scenario.Path);
        var gap = TestResolver.Resolve(project, session).Gaps[0].Gap!.Message;
        Assert.Equal($$"""
            # Feature: User form

            ## Metadata

            | Field | Value |
            | --- | --- |
            | Recorded | {{session.Id}} {{session.Hash()}} |

            ## Scenario: User form

            ```gherkin
            Given I am on the User Form page
            When I enter "Ada "the first"" into First Name
            And I check Terms
            And I select "Netherlands" from Country
            ```

            Gap (event 5): the secret <apiKey> goes here, mapped to a setting or test data: I enter <apiKey> into Password

            ```gherkin
            When I tap Submit
            ```

            Gap (event 7): {{gap}}

            ```gherkin
            Then I should be on the User Form page
            ```

            """.Replace("\r\n", "\n"), scenario.Text);
        Assert.Equal(new RecordedHeader(session.Id, session.Hash()), RecordedHeader.Find(scenario.Text));
    }

    [Fact]
    public void An_xunit_draft_needs_the_test_base_and_says_where_to_set_it()
    {
        var (project, session) = Recorded(config: "");

        var draft = Draft(project, session, TestTarget.Both);

        Assert.Single(draft.Files); // the scenario
        Assert.Contains("## Tests → Base", Assert.Single(draft.Problems));
    }

    [Fact]
    public void A_draft_is_written_over_its_earlier_draft_but_never_over_a_person_s_file_or_part()
    {
        var (project, session) = Recorded();
        var draft = Draft(project, session);
        var mine = Path.Combine(project.Folder, "Tests", "Recorded", "UserFormTests.cs");
        var feature = Path.Combine(project.Folder, "Features", "Recorded", "user-form.feature.md");

        Assert.All(TestDrafter.Write(project, draft), w => Assert.True(w.Written, w.Note));
        File.WriteAllText(mine, "// mine\n");
        var again = TestDrafter.Write(project, draft);

        Assert.Equal([true, false, true], again.Select(w => w.Written));
        Assert.Equal("// mine\n", File.ReadAllText(mine));
        File.WriteAllText(feature, "# Feature: a person's\n");
        var over = TestDrafter.Write(project, draft).Single(w => w.Path.EndsWith(".feature.md", StringComparison.Ordinal));
        Assert.False(over.Written);
        Assert.Equal("# Feature: a person's\n", File.ReadAllText(feature));
    }

    [Theory]
    [InlineData("", false, TestTarget.Xunit)]
    [InlineData("", true, TestTarget.Scenario)]
    [InlineData("\n## Tests\n\n| Field | Value |\n| --- | --- |\n| Target | both |\n", true, TestTarget.Both)]
    public void The_default_target_is_the_setting_else_a_scenario_in_a_uat_suite_else_xunit(string config, bool uat, TestTarget expected)
    {
        var (project, _) = UserForm.Make(_root, config);
        if (uat)
            File.WriteAllText(Path.Combine(project.Folder, ScrapeProject.UatConfigFileName), "# UAT Config\n");

        Assert.Equal(expected, ScrapeProject.Open(project.Folder).TestTarget);
    }

    [Fact]
    public void A_wrong_target_is_a_config_error_with_its_line()
    {
        var e = Assert.Throws<FormatException>(() => ScraperConfig.Parse("## Tests\n\n| Field | Value |\n| --- | --- |\n| Target | nunit |\n"));

        Assert.Contains("xunit, scenario or both", e.Message);
    }

    [Fact]
    public void A_new_config_says_its_target()
    {
        var config = ScraperConfig.Parse(ScraperConfigWriter.New("http://localhost:5180/", target: TestTarget.Scenario));

        Assert.Equal(TestTarget.Scenario, config.Tests.Target);
    }
}
