using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.ArgumentCases;

/// <summary>Callable phrases: steps written in scenario language, called like a phrase.</summary>
public sealed class UatCallableTests
{
    private const string Apps = """
        # UAT Config

        ## Apps

        | Name | User | Role |
        | --- | --- | --- |
        | Eva's PC | Eva | Planner |
        | Bob | | |
        """;

    private const string Assigned = """
        ## Step: a todo {title} for {who} with {hours} hours

        ```gherkin
        When I assign <title> to <who> due 12-May-2026 for <hours> hours as Open
        And I open the todo <title>
        ```
        """;

    [Fact]
    public async Task ACall_RunsItsSteps_InOrder_WithTheCallsValues()
    {
        var (fixture, scenario) = await Run("""
            Given a todo "Buy milk" for Eva with 1,250.5 hours
            """, Assigned);

        var assigned = Assert.Single(fixture.State.Assigned);
        Assert.Equal(("Buy milk", "Eva", 1250.5m), (assigned.Todo, assigned.Assignee.Name, assigned.Hours));
        Assert.Equal(["Buy milk"], fixture.State.Opened);
        Assert.All(scenario.Invocations, invocation => Assert.Equal("a todo {title} for {who} with {hours} hours", invocation.Call?.Callable.Phrase));
        Assert.Equal(5, scenario.Invocations[0].Call!.Location.LineNumber);
    }

    [Fact]
    public void Types_AreInferredFromWhereTheValuesGo()
    {
        var prepared = Prepare(Assigned);

        Assert.Null(prepared.Problem);
        Assert.Equal(typeof(string), prepared.Types["title"]);
        Assert.Equal(typeof(BddUserRole), prepared.Types["who"]);
        Assert.Equal(typeof(decimal), prepared.Types["hours"]);
        Assert.Equal("a todo {title: text} for {who: user} with {hours: number} hours", prepared.ToString());
    }

    [Fact]
    public void ABadValueInTheCall_IsReportedAtTheCall_WithTheWayToWriteIt()
    {
        var error = Assert.Single(Bind("""
            Given a todo "Buy milk" for Eva with 8,5 hours
            """, Assigned));

        Assert.Equal("UATA003", error.Code);
        Assert.Equal(5, error.Location.LineNumber);
        Assert.Contains("'hours'", error.Message, StringComparison.Ordinal);
        Assert.Contains("8.5", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStepOfTheBodyThatBindsToNothing_IsReportedAtTheCall_WithWhereItIs()
    {
        var error = Assert.Single(Bind("""
            Given a misspelled todo "Buy milk"
            """, """
            ## Step: a misspelled todo {title}

            ```gherkin
            When I opn the todo <title>
            ```
            """));

        Assert.Equal("UATB001", error.Code);
        Assert.Equal(5, error.Location.LineNumber);
        Assert.Equal([11], error.Within.Select(inner => inner.LineNumber));
        Assert.Contains("I opn the todo \"Buy milk\"", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ABrokenCallable_LoadsWithTheRest_AndFailsOnlyItsCalls()
    {
        const string Conflict = """
            ## Step: a clash {x}

            ```gherkin
            When I assign "T" to Eva due <x> for <x> hours as Open
            ```
            """;

        Assert.Empty(Bind("When I open the todo \"Tea\"", Conflict));

        var error = Assert.Single(Bind("Given a clash 3", Conflict));
        Assert.Equal("UATC002", error.Code);
        Assert.Contains("no value is both date and number", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnusedOrUnknownPlaceholder_IsBroken()
    {
        Assert.Equal("UATC001", Prepare("""
            ## Step: a todo {title} and {spare}

            ```gherkin
            When I open the todo <title>
            ```
            """).Problem?.Code);
        Assert.Equal("UATC001", Prepare("""
            ## Step: a todo

            ```gherkin
            When I open the todo <title>
            ```
            """).Problem?.Code);
    }

    [Fact]
    public void ALiteralValueInTheBody_ThatDoesNotFit_IsBroken()
    {
        var prepared = Prepare("""
            ## Step: a wrong todo {title}

            ```gherkin
            When I assign <title> to Eva due 12-May-2026 for 8,5 hours as Open
            ```
            """);

        Assert.Equal("UATC008", prepared.Problem?.Code);
        Assert.Contains("UATA003", prepared.Problem!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADeclaredType_ChecksAValueInferenceCannotSee_AndMustAgreeWithUse()
    {
        const string Declared = """
            ## Step: the report of {due}

            | Parameter | Type | Example     |
            | --------- | ---- | ----------- |
            | due       | date | 12-May-2026 |

            ```gherkin
            When I open the todo "Report <due>"
            ```
            """;

        Assert.Equal(typeof(DateOnly), Prepare(Declared).Types["due"]);
        var error = Assert.Single(Bind("Given the report of 2026-05-12", Declared));
        Assert.Equal("UATA002", error.Code);

        Assert.Equal("UATC002", Prepare("""
            ## Step: a todo {title} for {hours} hours

            | Parameter | Type |
            | --------- | ---- |
            | hours     | date |

            ```gherkin
            When I assign <title> to Eva due 12-May-2026 for <hours> hours as Open
            ```
            """).Problem?.Code);

        Assert.Equal("UATC003", Prepare("""
            ## Step: the todo {title}

            | Parameter | Type    |
            | --------- | ------- |
            | title     | colour  |

            ```gherkin
            When I open the todo <title>
            ```
            """).Problem?.Code);
    }

    [Fact]
    public async Task ATable_IsPassedWhole_AndABadCellIsReportedAtItsRowOfTheCall()
    {
        const string Server = """
            ## Step: the server knows

            ```gherkin
            Given the server has todos
              <table>
            ```
            """;

        var (fixture, _) = await Run("""
            Given the server knows
              | Title    | Status      |
              | -------- | ----------- |
              | Buy milk | Open        |
              | Tea      | In Progress |
            """, Server);
        Assert.Equal(["Buy milk", "Tea"], fixture.State.Server.Select(todo => todo.Title));
        Assert.Equal(typeof(IReadOnlyList<TodoRow>), Prepare(Server).BlockType);

        var error = Assert.Single(Bind("""
            Given the server knows
              | Title    | Status  |
              | -------- | ------- |
              | Buy milk | Open    |
              | Tea      | Waiting |
            """, Server));
        Assert.Equal("UATA004", error.Code);
        Assert.Equal(9, error.Location.LineNumber);

        Assert.Equal("UATA007", Assert.Single(Bind("Given the server knows", Server)).Code);
        Assert.Equal("UATB004", Assert.Single(Bind("""
            Given a todo "Tea" for Eva with 1 hours
              | Title |
              | ----- |
              | Tea   |
            """, Assigned)).Code);
    }

    [Fact]
    public async Task APerRowCallable_RunsItsBodyOncePerRow()
    {
        const string PerRow = """
            ## Step for each row: todos for {who}

            ```gherkin
            When I assign <title> to <who> due <due> for 1 hours as Open
            ```
            """;

        var (fixture, scenario) = await Run("""
            Given todos for Eva
              | Title    | Due         |
              | -------- | ----------- |
              | Buy milk | 12-May-2026 |
              | Tea      | 13-May-2026 |
            """, PerRow);

        Assert.Equal([("Buy milk", new DateOnly(2026, 5, 12)), ("Tea", new DateOnly(2026, 5, 13))], fixture.State.Assigned.Select(a => (a.Todo, a.Due)));
        Assert.Equal([1, 2], scenario.Invocations.Select(invocation => invocation.Call!.Row!.Value));
        Assert.Equal([8, 9], scenario.Invocations.Select(invocation => invocation.Call!.Location.LineNumber));

        var badCell = Assert.Single(Bind("""
            Given todos for Eva
              | Title    | Due         |
              | -------- | ----------- |
              | Buy milk | 12-May-2026 |
              | Tea      | 2026-05-13  |
            """, PerRow));
        Assert.Equal(("UATA002", 9), (badCell.Code, badCell.Location.LineNumber));
        Assert.NotNull(badCell.Fix);

        var unknown = Bind("""
            Given todos for Eva
              | Title    | When        |
              | -------- | ----------- |
              | Buy milk | 12-May-2026 |
            """, PerRow);
        Assert.Equal(["UATA006", "UATA006"], unknown.Select(d => d.Code));
        Assert.Equal("UATA007", Assert.Single(Bind("Given todos for Eva", PerRow)).Code);
    }

    /// <summary>
    /// A block the phrase does not take, or one it needs and does not get, is reported with what the phrase passes on,
    /// at the block's line when there is one (review 08, finding 12: these messages and lines had no test).
    /// </summary>
    [Fact]
    public void ABlockThePhraseDoesNotTake_OrNeeds_IsReportedWithWhatItPassesOn()
    {
        const string Server = """
            ## Step: the server knows

            ```gherkin
            Given the server has todos
              <table>
            ```
            """;
        const string PerRow = """
            ## Step for each row: todos for {who}

            ```gherkin
            When I assign <title> to <who> due <due> for 1 hours as Open
            ```
            """;

        var needsTable = Assert.Single(Bind("Given the server knows", Server));
        Assert.Equal("UATA007", needsTable.Code);
        Assert.Contains("needs a table under it, which 'the server knows' passes on", needsTable.Message, StringComparison.Ordinal);

        var needsRows = Assert.Single(Bind("Given todos for Eva", PerRow));
        Assert.Contains("runs once per row, with the columns", needsRows.Message, StringComparison.Ordinal);

        var tableNotTaken = Assert.Single(Bind("""
            Given a todo "Tea" for Eva with 1 hours
              | Title |
              | ----- |
              | Tea   |
            """, Assigned));
        Assert.Equal("UATB004", tableNotTaken.Code);
        Assert.Contains("does not accept a table", tableNotTaken.Message, StringComparison.Ordinal);
        Assert.Equal(6, tableNotTaken.Location.LineNumber);

        var textNotTaken = Assert.Single(Bind(""""
            Given a todo "Tea" for Eva with 1 hours
              """
              some text
              """
            """", Assigned));
        Assert.Equal("UATB004", textNotTaken.Code);
        Assert.Contains("does not accept a doc string", textNotTaken.Message, StringComparison.Ordinal);
        Assert.Equal(5, textNotTaken.Location.LineNumber);
    }

    [Fact]
    public async Task ACallableCanCallAnother_ButNotItself_AndNotTooDeep()
    {
        var (fixture, _) = await Run("Given two todos", $$"""
            ## Step: two todos

            ```gherkin
            Given a todo "One" for Eva with 1 hours
            And a todo "Two" for Bob with 2 hours
            ```

            {{Assigned}}
            """);
        Assert.Equal(["One", "Two"], fixture.State.Assigned.Select(a => a.Todo));

        var cycle = Assert.Single(Bind("Given ping", """
            ## Step: ping

            ```gherkin
            Given pong
            ```

            ## Step: pong

            ```gherkin
            Given ping
            ```
            """));
        Assert.Equal("UATC004", cycle.Code);

        var deep = Assert.Single(Bind("Given level 1", """
            ## Step: level 1

            ```gherkin
            Given level 2
            ```

            ## Step: level 2

            ```gherkin
            Given level 3
            ```

            ## Step: level 3

            ```gherkin
            Given level 4
            ```

            ## Step: level 4

            ```gherkin
            When I open the todo "Deep"
            ```
            """));
        Assert.Equal("UATC005", deep.Code);
        Assert.Equal(5, deep.Location.LineNumber);
        Assert.Equal(3, deep.Within.Count);
    }

    [Fact]
    public void ACallableThatNamesAPage_EndsOnOne_AndARowEndsWhereItStarted()
    {
        var unsaid = Assert.Single(Bind("Given a detour", """
            ## Step: a detour

            ```gherkin
            Given I am on the Todo page
            When I open the todo "Tea"
            ```
            """));
        Assert.Equal("UATC006", unsaid.Code);
        Assert.Contains("does not end on one", unsaid.Message, StringComparison.Ordinal);

        Assert.Empty(Bind("Given a round trip", """
            ## Step: a round trip

            ```gherkin
            Given I am on the Todo page
            When I open the todo "Tea"
            Then I should be on the Detail page
            ```
            """));

        var wandering = Assert.Single(Bind("""
            Given each todo opened
              | Title |
              | ----- |
              | Tea   |
            """, """
            ## Step for each row: each todo opened

            ```gherkin
            Given I am on the Todo page
            When I open the todo <title>
            Then I should be on the Detail page
            ```
            """));
        Assert.Equal(("UATC006", 8), (wandering.Code, wandering.Location.LineNumber));
    }

    [Fact]
    public async Task ACallableWithAUserFirst_IsASubject_AndIIsTheCurrentUser()
    {
        var (fixture, _) = await Run("""
            When Eva grabs "Milk"
            And the todo "Tea" goes to I
            """, """
            ## Step: {user} grabs {todo}

            ```gherkin
            When <user> takes <todo>
            ```

            ## Step: the todo {todo} goes to {user}

            ```gherkin
            When <user> takes <todo>
            ```
            """);

        Assert.Equal([("Eva", "Milk"), ("Eva", "Tea")], fixture.State.Taken);
    }

    [Fact]
    public void ACallableWithTheTextOfACSharpPhrase_IsAmbiguous()
    {
        var error = Assert.Single(Bind("When I pay 3.00 EUR", """
            ## Step: I pay {amount}

            ```gherkin
            When I open the todo <amount>
            ```
            """));

        Assert.Equal("UATB002", error.Code);
    }

    [Fact]
    public void TheTrace_IndentsACallsSteps_UnderTheCallAndItsRows()
    {
        var parse = BddMarkdownParser.Parse(Document("""
            Given todos for Eva
              | Title | Due         |
              | ----- | ----------- |
              | Milk  | 12-May-2026 |
              | Tea   | 13-May-2026 |
            """, """
            ## Step for each row: todos for {who}

            ```gherkin
            When I assign <title> to <who> due <due> for 1 hours as Open
            And I open the todo <title>
            ```
            """));
        var bind = BddBinder.Bind(parse.Document!, CatalogSet(new ArgumentFixture()));
        var results = Assert.Single(bind.Document!.Scenarios).Invocations.Select(invocation => BddStepResult.Passed(invocation));

        var lines = BddDiagnosticsFormatter.FormatSteps(results).ToArray();

        Assert.Equal(7, lines.Length);
        Assert.StartsWith("Call: 5: Given todos for Eva", lines[0], StringComparison.Ordinal);
        Assert.Equal("  row 1 (Milk, 12-May-2026)", lines[1]);
        Assert.StartsWith("    Passed: 15: ", lines[2], StringComparison.Ordinal);
        Assert.Equal("  row 2 (Tea, 13-May-2026)", lines[4]);
    }

    [Fact]
    public void PhraseFiles_AreSeenFromTheScenarioRoot_AndFromTheFilesOwnFolder_NotFromAnotherFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "bruinet-callables-" + Guid.NewGuid().ToString("N"), "Features");
        Directory.CreateDirectory(Path.Combine(root, "Todo"));
        Directory.CreateDirectory(Path.Combine(root, "Users"));
        try
        {
            File.WriteAllText(Path.Combine(root, "Todo.steps.md"), "# Steps: Todo\n\n## Step: a global one\n\n```gherkin\nWhen I open the todo \"Tea\"\n```\n");
            File.WriteAllText(Path.Combine(root, "Todo", "Later.steps.md"), "# Steps: Module\n\n## Step: a module one\n\n```gherkin\nWhen I open the todo \"Tea\"\n```\n");
            File.WriteAllText(Path.Combine(root, "Users", "Users.steps.md"), "# Steps: Users\n\n## Step: a users one\n\n```gherkin\nWhen I open the todo \"Tea\"\n```\n");
            var scenario = Path.Combine(root, "Todo", "TOD.feature.md");
            File.WriteAllText(scenario, Document("Given a global one\nAnd a module one", string.Empty));

            var file = BddScenarioFiles.Load(scenario);

            Assert.True(file.Success, BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
            Assert.Equal(["Todo.steps.md", "Later.steps.md"], BddScenarioFiles.PhraseFilesFor(scenario).Select(Path.GetFileName));
            Assert.True(file.Bind(CatalogSet(new ArgumentFixture())).Success);

            File.WriteAllText(scenario, Document("Given a users one", string.Empty));
            Assert.Equal("UATB010", Assert.Single(BddScenarioFiles.Load(scenario).Bind(CatalogSet(new ArgumentFixture())).Diagnostics).Code);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Theory]
    [InlineData("When I open the todo \"Tea\"\n  <table>", "UAT024")]
    [InlineData("When I open the todo \"Tea\"\n```\n\n## Step:\n\n```gherkin\nWhen I open the todo \"Tea\"", "UAT025")]
    [InlineData("When I open the todo \"Tea\"\n```\n\n## Step: nothing\n\n```gherkin", "UAT026")]
    [InlineData("When I open the todo \"Tea\"\n```\n\n## Step: bad {x}\n\n| Parameter | Kind |\n| --- | --- |\n| x | text |\n\n```gherkin\nWhen I open the todo <x>", "UAT027")]
    public void AWrongPhraseSection_IsAParseError(string steps, string code)
    {
        var parse = BddMarkdownParser.Parse(Document(steps, string.Empty));

        Assert.Contains(parse.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    [Fact]
    public void APhraseFile_HoldsOnlyPhraseSections()
    {
        var parse = BddMarkdownParser.ParsePhrases("""
            # Steps: Todo

            ## Scenario: Not here

            ```gherkin
            When I open the todo "Tea"
            ```

            ## Step: a good one

            ```gherkin
            When I open the todo "Tea"
            ```
            """);

        var error = Assert.Single(parse.Diagnostics);
        Assert.Equal(("UAT028", 3), (error.Code, error.Location.LineNumber));
        Assert.Equal("a good one", Assert.Single(parse.Document!.Callables).Phrase);
        Assert.Equal("UAT028", Assert.Single(BddMarkdownParser.ParsePhrases("## Step: headless\n\n```gherkin\nWhen I open the todo \"Tea\"\n```").Diagnostics).Code);
    }

    private static BddCatalogSet CatalogSet(ArgumentFixture fixture)
        => new(UatReflectionRuntime.FromComposition(fixture, fixture.Composition).CreateCommandCatalog(), BddConfigParser.Parse(Apps).Apps, suiteHead: null, new Dictionary<string, BddCommandCatalog>(), []);

    private static BddPreparedCallable Prepare(string phrases)
    {
        var parse = BddMarkdownParser.Parse(Document("When I open the todo \"Tea\"", phrases));
        Assert.True(parse.Success, BddDiagnosticsFormatter.FormatDiagnostics(parse.Diagnostics));
        return new BddCallableSet(parse.Document!.Callables).Prepare(CatalogSet(new ArgumentFixture())).All[0];
    }

    private static IReadOnlyList<BddDiagnostic> Bind(string steps, string phrases)
    {
        var parse = BddMarkdownParser.Parse(Document(steps, phrases));
        Assert.True(parse.Success, BddDiagnosticsFormatter.FormatDiagnostics(parse.Diagnostics));
        return BddBinder.Bind(parse.Document!, CatalogSet(new ArgumentFixture())).Diagnostics;
    }

    private static async Task<(ArgumentFixture Fixture, BddBoundScenario Scenario)> Run(string steps, string phrases)
    {
        var fixture = new ArgumentFixture();
        var parse = BddMarkdownParser.Parse(Document(steps, phrases));
        Assert.True(parse.Success, BddDiagnosticsFormatter.FormatDiagnostics(parse.Diagnostics));
        var bind = BddBinder.Bind(parse.Document!, CatalogSet(fixture));
        Assert.True(bind.Success, BddDiagnosticsFormatter.FormatDiagnostics(bind.Diagnostics));

        using var scope = fixture.Composition.CreateScope();
        var context = new BddExecutionContext { Now = fixture.Now, Apps = BddConfigParser.Parse(Apps).Apps, SessionHost = fixture };
        UatReflectionRuntime.SetServiceProvider(context, scope.ServiceProvider);
        var scenario = Assert.Single(bind.Document!.Scenarios);
        var result = await new BddScenarioRunner(context).RunAsync(scenario);
        Assert.True(result.Passed, string.Join(Environment.NewLine, BddDiagnosticsFormatter.FormatSteps(result.Steps)));
        return (fixture, scenario);
    }

    // The scenario's steps start on line 5; its phrases follow them.
    private static string Document(string steps, string phrases) => $"""
        # Feature: Callables

        ## Scenario: Callables
        ```gherkin
        {steps}
        ```

        {phrases}
        """;
}
