using Xunit;

namespace Bruinet.Bdd.Tests;

public sealed class BddMarkdownParserTests
{
    [Fact]
    public void Parse_OldHeadings_AreOneErrorEach_AndTheRestOfTheFileIsStillRead()
    {
        var result = BddMarkdownParser.Parse("""
            # UAT: Login

            ## Phrase: a sign-in

            ```gherkin
            When I sign in
            ```

            ## Scenario: Sign in

            ```gherkin
            When a sign-in
            ```
            """);

        Assert.Equal(
            ["'# UAT: Login' is the old form of '# Feature: Login'", "'## Phrase: a sign-in' is the old form of '## Step: a sign-in'"],
            result.Diagnostics.Select(x => x.Message[..x.Message.IndexOf(". Run", StringComparison.Ordinal)]));
        Assert.All(result.Diagnostics, x => Assert.Equal("BDD029", x.Code));
        Assert.Equal("Login", result.Document!.Title);
        Assert.Single(result.Document.Callables);
        Assert.Single(result.Document.Scenarios);
    }

    [Fact]
    public void ParsePhrases_OldHeading_IsAnError()
    {
        var result = BddMarkdownParser.ParsePhrases("# Phrases: Todo\n\n## Step: one\n\n```gherkin\nWhen I sync\n```\n");

        Assert.Equal("BDD029", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("a/b/TOD.01.uat.md", "a/b/TOD.01.feature.md")]
    [InlineData("Todo.phrases.md", "Todo.steps.md")]
    [InlineData("TOD.01.feature.md", null)]
    public void FileFormat_NewFileName(string path, string? expected) =>
        Assert.Equal(expected, BddFileFormat.NewFileName(path));

    [Fact]
    public void FileFormat_RewriteHeadings_KeepsLineEndingsAndOtherLines()
    {
        var (text, count) = BddFileFormat.RewriteHeadings("# UAT: T\r\n\r\n## Phrase for each row: x\r\nWhen # UAT: stays\r\n");

        Assert.Equal(2, count);
        Assert.Equal("# Feature: T\r\n\r\n## Step for each row: x\r\nWhen # UAT: stays\r\n", text);
    }
    [Fact]
    public void Parse_SimpleScenarioWithMetadataAndTable_ReturnsDocument()
    {
        var markdown = """
            # Feature: Login

            ## Metadata

            | Field | Value |
            | --- | --- |
            | App | Example.Maui |
            | Area | Authentication |
            | Target | MAUI |
            | Tags | smoke, login |

            @smoke @login
            ## Scenario: Valid user can sign in

            ```gherkin
            Given I am on the Login page
            When I sign in with credentials
            | Field | Value |
            | --- | --- |
            | User name | ada@example.com |
            | Password | correct-password |
            Then I should see "Welcome Ada"
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown, "login.feature.md");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Document);
        var document = result.Document;
        Assert.Equal("Login", document.Title);
        Assert.Equal("Example.Maui", document.Metadata["App"]);
        var scenario = Assert.Single(document.Scenarios);
        Assert.Equal("Valid user can sign in", scenario.Name);
        Assert.Equal(["smoke", "login"], scenario.Tags);
        Assert.Equal(3, scenario.Steps.Count);
        Assert.Equal(BddStepKeyword.When, scenario.Steps[1].Keyword);
        Assert.Equal(BddEffectiveStepKeyword.When, scenario.Steps[1].EffectiveKeyword);
        Assert.NotNull(scenario.Steps[1].Table);
        Assert.Equal("ada@example.com", scenario.Steps[1].Table!.Rows[0].Cells["Value"]);
    }

    [Fact]
    public void Parse_ScenarioOutline_ExpandsExamplesIntoScenarios()
    {
        var markdown = """
            # Feature: Login

            ## Scenario Outline: Login result is shown

            ```gherkin
            Given I am on the Login page
            When I enter credentials
            | Field | Value |
            | --- | --- |
            | User name | <user> |
            | Password | <password> |
            Then I should see "<result>"
            ```

            ### Examples

            | user | password | result |
            | --- | --- | --- |
            | ada@example.com | correct-password | Dashboard |
            | locked@example.com | correct-password | Account locked |
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Document);
        var document = result.Document;
        Assert.Equal(2, document.Scenarios.Count);
        Assert.Equal("Login result is shown [1]", document.Scenarios[0].Name);
        Assert.Equal("Dashboard", document.Scenarios[0].Steps[2].Text.Split('"')[1]);
        Assert.Equal("locked@example.com", document.Scenarios[1].Steps[1].Table!.Rows[0].Cells["Value"]);
        Assert.Equal("Login result is shown", document.Scenarios[1].OutlineName);
        Assert.Equal(2, document.Scenarios[1].ExampleIndex);
    }

    [Fact]
    public void Parse_Background_PrependsSharedSetupModel()
    {
        var markdown = """
            # Feature: Settings

            ## Background

            ```gherkin
            Given the application is running
            ```

            ## Scenario: Save display name

            ```gherkin
            Given I am on the Settings page
            When I enter "Ada Lovelace" into Display Name
            Then I should see "Settings saved"
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Document);
        var document = result.Document;
        var background = Assert.Single(document.Background);
        Assert.Equal("the application is running", background.Text);
        Assert.Single(document.Scenarios);
    }

    [Fact]
    public void Parse_DefaultNamingExampleText_ParsesInferredAuthoringNames()
    {
        var markdown = """
            # Feature: Settings

            ## Metadata

            | Field | Value |
            | --- | --- |
            | App | Example.Maui |
            | Area | Settings |
            | Target | MAUI |
            | Tags | smoke, settings |

            @smoke @settings
            ## Scenario: Save display name

            ```gherkin
            Given I am on the Settings page
            When I enter "Ada Lovelace" into Display Name
            And I check Email Notifications
            And I tap Save
            Then I should see "Settings saved"
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Document);
        var scenario = Assert.Single(result.Document.Scenarios);
        Assert.Equal(BddEffectiveStepKeyword.When, scenario.Steps[2].EffectiveKeyword);
        Assert.Equal("I check Email Notifications", scenario.Steps[2].Text);
        Assert.Equal("I tap Save", scenario.Steps[3].Text);
    }

    [Fact]
    public void Parse_StandardTemplateMetadataAndTags_ReturnsDocument()
    {
        var markdown = """
            # Feature: Camera Actions

            ## Metadata

            | Field | Value |
            | --- | --- |
            | App | Example.Maui |
            | Area | Camera Actions |
            | Target | MAUI |
            | Tags | smoke, maui, camera |
            | Mode | Automated |
            | Requires | Deterministic |
            | Owner | QA |
            | Priority | Smoke |
            | Evidence | screenshot, transcript |

            @smoke @maui @camera @deterministic @uat-003-6
            ## Scenario: UAT-003.6 Sub-button hides action rows during capture

            ```gherkin
            Given I am on the Main page
            When I tap Look
            Then Overview should be visible
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.NotNull(result.Document);
        Assert.Equal("Automated", result.Document.Metadata[BddMetadataFields.Mode]);
        Assert.Equal("Deterministic", result.Document.Metadata[BddMetadataFields.Requires]);
        Assert.Equal("screenshot, transcript", result.Document.Metadata[BddMetadataFields.Evidence]);
        var scenario = Assert.Single(result.Document.Scenarios);
        Assert.Equal(
            ["smoke", "maui", "camera", "deterministic", "uat-003-6"],
            scenario.Tags);
    }

    [Fact]
    public void Parse_MissingUatHeading_ReturnsDiagnostic()
    {
        var markdown = """
            ## Scenario: Missing heading

            ```gherkin
            Given I am on the Login page
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, x => x.Code == "BDD000");
    }

    [Fact]
    public void Parse_MalformedStepTable_ReturnsDiagnostic()
    {
        var markdown = """
            # Feature: Broken

            ## Scenario: Bad table

            ```gherkin
            Given I am on the Login page
            When I enter credentials
            | Field | Value |
            | --- | --- |
            | User name |
            Then I should see "Welcome"
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, x => x.Code == "BDD018");
    }

    [Fact]
    public void Parse_OutlineWithMissingExampleColumn_ReturnsDiagnostic()
    {
        var markdown = """
            # Feature: Broken Outline

            ## Scenario Outline: Missing column

            ```gherkin
            Given I am on the <page> page
            ```

            ### Examples

            | user |
            | --- |
            | ada |
            """;

        var result = BddMarkdownParser.Parse(markdown);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, x => x.Code == "BDD015");
    }

    private const string TwoBlocks = """
        # Feature: Titles

        @create
        ## Scenario Outline: A title is refused

        ```gherkin
        When I set Title to "<title>"
        Then Title Error should show "<error>"
        ```

        @maui @web
        ### Examples: empty and blank

        | title | error |
        | --- | --- |
        | | A title is required |
        | "  " | A title is required |

        @web
        ### Examples: refused by the server

        | title | error |
        | --- | --- |
        | Already taken | A todo with this title exists |

        @smoke
        ## Scenario: Next

        ```gherkin
        Given I am on the Todo List page
        ```
        """;

    [Fact]
    public void Parse_SeveralExamplesBlocks_NumberTheRowsAcrossBlocks_AndKeepEachBlocksName()
    {
        var result = BddMarkdownParser.Parse(TwoBlocks);

        Assert.True(result.Success, FormatDiagnostics(result));
        var rows = result.Document!.Scenarios.Where(x => x.OutlineName is not null).ToList();
        Assert.Equal(["A title is refused [1]", "A title is refused [2]", "A title is refused [3]"], rows.Select(x => x.Name));
        Assert.Equal([1, 2, 3], rows.Select(x => x.ExampleIndex!.Value));
        Assert.Equal(["empty and blank", "empty and blank", "refused by the server"], rows.Select(x => x.ExamplesName));
        Assert.Equal("I set Title to \"Already taken\"", rows[2].Steps[0].Text);
    }

    [Fact]
    public void Parse_TagsAboveAnExamplesBlock_JoinTheOutlinesTags_ForThatBlocksRowsOnly()
    {
        var scenarios = BddMarkdownParser.Parse(TwoBlocks).Document!.Scenarios;

        Assert.Equal(["create", "maui", "web"], scenarios[0].Tags);
        Assert.Equal(["create", "web"], scenarios[2].Tags);
        Assert.Equal(["smoke"], scenarios[3].Tags);
    }

    [Fact]
    public void Parse_AnExamplesHeadingWithoutAName_StillReads_AndHasNoName()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Titles

            ## Scenario Outline: A title

            ```gherkin
            When I set Title to "<title>"
            ```

            ### Examples:

            | title |
            | --- |
            | Buy milk |
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Null(Assert.Single(result.Document!.Scenarios).ExamplesName);
    }

    [Fact]
    public void Parse_AnExamplesBlockMissingAColumn_IsUat015_NamingTheBlock()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Titles

            ## Scenario Outline: A title is refused

            ```gherkin
            Then Title Error should show "<error>"
            ```

            ### Examples: first

            | error |
            | --- |
            | A title is required |

            ### Examples: second

            | title |
            | --- |
            | Already taken |
            """);

        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal("BDD015", problem.Code);
        Assert.Contains("'second'", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_TwoExamplesBlocksWithOneName_IsUat032()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Titles

            ## Scenario Outline: A title

            ```gherkin
            When I set Title to "<title>"
            ```

            ### Examples: valid

            | title |
            | --- |
            | Buy milk |

            ### Examples: valid

            | title |
            | --- |
            | Buy bread |
            """);

        var problem = Assert.Single(result.Diagnostics);
        Assert.Equal("BDD032", problem.Code);
        Assert.Equal(15, problem.Location.LineNumber);
    }

    [Fact]
    public void Parse_AnExamplesBlockWithoutATable_IsUat017_AndTheNextBlockStillReads()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Titles

            ## Scenario Outline: A title

            ```gherkin
            When I set Title to "<title>"
            ```

            ### Examples: none yet

            ### Examples: valid

            | title |
            | --- |
            | Buy milk |
            """);

        Assert.Equal("BDD017", Assert.Single(result.Diagnostics).Code);
        Assert.Equal("valid", Assert.Single(result.Document!.Scenarios).ExamplesName);
    }

    [Fact]
    public void Parse_TagsAboveAnExamplesHeading_OutsideAnOutline_AreUat020()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Titles

            ## Scenario: A title

            ```gherkin
            When I set Title to "Buy milk"
            ```

            @web
            ### Examples

            | title |
            | --- |
            | Buy milk |
            """);

        Assert.Contains(result.Diagnostics, x => x.Code == "BDD020");
    }

    [Theory]
    [InlineData("```gherkin", "```")]
    [InlineData("~~~gherkin", "~~~")]
    [InlineData("```Gherkin", "```")]
    [InlineData("````gherkin", "````")]
    public void Parse_AGherkinBlock_IsReadWithAnyFenceAndCase(string open, string close)
    {
        var markdown = $"""
            # Feature: Login

            ## Scenario: Valid user can sign in

            {open}
            Given I am on the Login page
            When I tap Sign in
            Then I should see "Welcome Ada"
            {close}
            """;

        var result = BddMarkdownParser.Parse(markdown, "login.feature.md");

        Assert.True(result.Success, FormatDiagnostics(result));
        var scenario = Assert.Single(result.Document!.Scenarios);
        Assert.Equal(3, scenario.Steps.Count);
        Assert.Equal("I am on the Login page", scenario.Steps[0].Text);
        Assert.Equal(BddEffectiveStepKeyword.Then, scenario.Steps[2].EffectiveKeyword);
    }

    // AD-023: a step is read only inside a gherkin block; every other Markdown form is prose, and
    // the file parses to the same document as without it.
    [Theory]
    [InlineData("A paragraph that explains the scenario, with *emphasis* and a [link](https://example.com).")]
    [InlineData("- a list item\n- Given a list item that reads like a step")]
    [InlineData("1. first\n2. When an ordered item reads like a step")]
    [InlineData("> Given a quote that reads like a step")]
    [InlineData("![an image](image.png)")]
    [InlineData("<div>\nGiven some HTML\n</div>")]
    [InlineData("---")]
    [InlineData("Setext heading\n--------------")]
    [InlineData("    Given an indented code block")]
    [InlineData("```json\n{ \"Given\": 1 }\nGiven a json block\n```")]
    [InlineData("```\nGiven a fence without a language\n```")]
    [InlineData("Given a paragraph that reads like a step")]
    [InlineData("### Why")]
    [InlineData("<!-- a comment -->")]
    public void Parse_Prose_AnywhereIsLeftOut(string prose)
    {
        var bare = Parse(WithProse(string.Empty));
        var result = BddMarkdownParser.Parse(WithProse(prose), "login.feature.md");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.DoesNotContain(result.Diagnostics, x => x.Severity == BddDiagnosticSeverity.Warning);
        Assert.Equal(Shape(bare), Shape(result.Document!));
    }

    [Fact]
    public void Parse_AProseSection_RunsToTheNextLevel2Heading()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ## Notes

            Some notes.

            ```gherkin
            Given a block in a prose section is prose
            ```

            | Field | Value |
            | --- | --- |
            | Not | metadata |

            ## Scenario: Sign in

            ```gherkin
            Given I am on the Login page
            ```
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Empty(result.Document!.Metadata);
        Assert.Equal("I am on the Login page", Assert.Single(Assert.Single(result.Document.Scenarios).Steps).Text);
    }

    [Fact]
    public void Parse_SeveralBlocks_AreOneListOfSteps_AndAndContinuesAcrossThem()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ## Scenario: Sign in

            ```gherkin
            Given I am on the Login page
            When I tap Sign in
            ```

            Prose between two blocks of one scenario.

            ```gherkin
            # a comment is not a heading
            And I wait
            Then I should see "Welcome"
            ```
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        var steps = Assert.Single(result.Document!.Scenarios).Steps;
        Assert.Equal(["I am on the Login page", "I tap Sign in", "I wait", "I should see \"Welcome\""], steps.Select(x => x.Text));
        Assert.Equal(BddEffectiveStepKeyword.When, steps[2].EffectiveKeyword);
        Assert.Equal(14, steps[2].Source.LineNumber);
    }

    [Fact]
    public void Parse_ProseBetweenAHeadingAndItsTable_IsAllowed()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ## Metadata

            What the scenarios need.

            ### Where it comes from

            | Field | Value |
            | --- | --- |
            | App | Example.Maui |

            ## Scenario Outline: Sign in as <user>

            ```gherkin
            Given I sign in as <user>
            ```

            ### Examples

            One row per user.

            | user |
            | --- |
            | ada |
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("Example.Maui", result.Document!.Metadata["App"]);
        Assert.Equal("I sign in as ada", Assert.Single(Assert.Single(result.Document.Scenarios).Steps).Text);
    }

    [Fact]
    public void Parse_BareSteps_AreProse_AndTheNoStepsErrorSaysWhereTheyGo()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ## Scenario: Sign in

            Given I am on the Login page
            When I tap Sign in
            """);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("BDD007", diagnostic.Code);
        Assert.EndsWith(BddFileFormat.FenceHint, diagnostic.Message);
    }

    [Fact]
    public void Parse_AStepLikeParagraph_BesideABlock_IsProse()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ## Scenario: Sign in

            Given that the user has an account, this signs in.

            ```gherkin
            Given I am on the Login page
            ```
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("## scenario: lower case")]
    [InlineData("## Scenario without a colon")]
    [InlineData("### Scenario: too deep")]
    [InlineData("## Steps: in a feature file")]
    [InlineData("### Examples")]
    public void Parse_AHeadingThatLooksLikeASection_IsAWarning(string heading)
    {
        var result = BddMarkdownParser.Parse($"""
            # Feature: Login

            {heading}

            ## Scenario: Sign in

            ```gherkin
            Given I am on the Login page
            ```
            """);

        Assert.True(result.Success, FormatDiagnostics(result));
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal(("BDD031", BddDiagnosticSeverity.Warning), (warning.Code, warning.Severity));
    }

    [Fact]
    public void Parse_AGherkinBlockOutsideASection_IsAnError()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            ```gherkin
            Given I am on the Login page
            ```

            ## Scenario: Sign in

            ```gherkin
            Given I am on the Login page
            ```
            """);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(("BDD021", 3), (diagnostic.Code, diagnostic.Location.LineNumber));
    }

    [Fact]
    public void Parse_TagsAboveProse_AreNotAScenariosTags()
    {
        var result = BddMarkdownParser.Parse("""
            # Feature: Login

            @smoke

            Some prose.

            ## Scenario: Sign in

            ```gherkin
            Given I am on the Login page
            ```
            """);

        Assert.Equal("BDD020", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void FileFormat_FenceSteps_WrapsBareSteps_AndKeepsTagsParametersAndFencedSections()
    {
        var (text, count) = BddFileFormat.FenceSteps(
            "# Feature: T\r\n\r\n## Scenario: A\r\n\r\nGiven a\r\n| x |\r\n| --- |\r\n| 1 |\r\n\r\n@smoke\r\n## Scenario: B\r\n\r\n```gherkin\r\nGiven b\r\n```\r\n\r\n" +
            "## Step: c {n}\r\n\r\n| Parameter | Type |\r\n| --- | --- |\r\n| n | number |\r\n\r\nWhen c\r\n  <table>");

        Assert.Equal(2, count);
        Assert.Equal(
            "# Feature: T\r\n\r\n## Scenario: A\r\n\r\n```gherkin\r\nGiven a\r\n| x |\r\n| --- |\r\n| 1 |\r\n```\r\n\r\n@smoke\r\n## Scenario: B\r\n\r\n```gherkin\r\nGiven b\r\n```\r\n\r\n" +
            "## Step: c {n}\r\n\r\n| Parameter | Type |\r\n| --- | --- |\r\n| n | number |\r\n\r\n```gherkin\r\nWhen c\r\n  <table>\r\n```",
            text);
        Assert.True(BddMarkdownParser.Parse(text).Success, FormatDiagnostics(BddMarkdownParser.Parse(text)));
    }

    private static string WithProse(string prose) => $"""
        {prose}

        # Feature: Login

        {prose}

        ## Metadata

        {prose}

        | Field | Value |
        | --- | --- |
        | App | Example.Maui |

        {prose}

        ## Background

        {prose}

        ```gherkin
        Given the application is running
        ```

        {prose}

        @smoke
        ## Scenario: Sign in

        {prose}

        ```gherkin
        Given I am on the Login page
        ```

        {prose}

        ```gherkin
        When I tap Sign in
        ```

        {prose}

        ## Scenario Outline: Sign in as <user>

        {prose}

        ```gherkin
        Given I sign in as <user>
        ```

        {prose}

        ### Examples

        {prose}

        | user |
        | --- |
        | ada |

        {prose}

        ## Step: I wait

        {prose}

        ```gherkin
        When I tap Wait
        ```

        {prose}
        """;

    private static BddDocument Parse(string markdown)
    {
        var result = BddMarkdownParser.Parse(markdown);
        Assert.True(result.Success, FormatDiagnostics(result));
        return result.Document!;
    }

    private static string Shape(BddDocument document) => string.Join('\n', [
        document.Title,
        .. document.Metadata.Select(pair => $"{pair.Key}={pair.Value}"),
        .. document.Background.Select(step => $"background {step.Keyword} {step.Text}"),
        .. document.Scenarios.Select(scenario => $"{scenario.Name} [{string.Join(',', scenario.Tags)}]: {string.Join("; ", scenario.Steps.Select(step => $"{step.EffectiveKeyword} {step.Text}"))}"),
        .. document.Callables.Select(callable => $"{callable.Phrase}: {string.Join("; ", callable.Steps.Select(step => step.Text))}"),
    ]);

    [Fact]
    public void Parse_FencedStepsWithTable_KeepsTheTable()
    {
        var markdown = """
            # Feature: Login

            ## Scenario: Valid user can sign in

            ```gherkin
            Given I am on the Login page
            When I sign in with credentials
            | Field | Value |
            | --- | --- |
            | User name | ada@example.com |
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown, "login.feature.md");

        Assert.True(result.Success, FormatDiagnostics(result));
        var scenario = Assert.Single(result.Document!.Scenarios);
        Assert.Equal(2, scenario.Steps.Count);
        Assert.Equal("ada@example.com", scenario.Steps[1].Table!.Rows[0].Cells["Value"]);
    }

    [Fact]
    public void Parse_FencedSteps_ReportDiagnosticsAtTheAuthoredLineNumber()
    {
        // The fence occupies line 5, so the bad step is on line 7 of the file as written.
        var markdown = """
            # Feature: Login

            ## Scenario: Valid user can sign in

            ```gherkin
            Given I am on the Login page
            Sign in without a keyword
            ```
            """;

        var result = BddMarkdownParser.Parse(markdown, "login.feature.md");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics, x => x.Code == "BDD011");
        Assert.Equal(7, diagnostic.Location.LineNumber);
    }

    private static string FormatDiagnostics(BddParseResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Diagnostics.Select(x => $"{x.Location}: {x.Code} {x.Message}"));
    }
}
