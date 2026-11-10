using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.ArgumentCases;

/// <summary>Typed phrase arguments, records and lists, doc strings and broken phrases (AD-016).</summary>
public sealed class UatArgumentTests
{
    private const string Apps = """
        # UAT Config

        ## Apps

        | Name | User | Role |
        | --- | --- | --- |
        | Eva's PC | Eva | Planner |
        | Bob | | |
        """;

    [Fact]
    public async Task TypedArguments_ArriveAsTheirTypes()
    {
        var fixture = await Run("""
            When I assign "Buy milk" to Eva due 12-May-2026 for 1,250.75 hours as In Progress
            """);

        var assigned = Assert.Single(fixture.State.Assigned);
        Assert.Equal("Buy milk", assigned.Todo);
        Assert.Equal("Eva", assigned.Assignee.Name);
        Assert.Equal("Planner", assigned.Assignee.Role);
        Assert.Equal(new DateOnly(2026, 5, 12), assigned.Due);
        Assert.Equal(1250.75m, assigned.Hours);
        Assert.Equal(Status.InProgress, assigned.Status);
    }

    [Fact]
    public void ABadValue_FailsAtBindTime_AtTheStepsLine()
    {
        var diagnostics = Bind("""
            When I assign "Buy milk" to Eva due 2026-05-12 for 1 hours as Open
            And I assign "Buy milk" to Carol due 12-May-2026 for 1 hours as Open
            """);

        Assert.Collection(
            diagnostics,
            first =>
            {
                Assert.Equal("UATA002", first.Code);
                Assert.Contains("'due' is a date; '2026-05-12' is not written as dd-MMM-yyyy. Write 12-May-2026.", first.Message, StringComparison.Ordinal);
                Assert.Equal(5, first.Location.LineNumber);
            },
            second =>
            {
                Assert.Equal("UATA005", second.Code);
                Assert.Contains("'Carol' is not a user in the Apps table", second.Message, StringComparison.Ordinal);
                Assert.Equal(6, second.Location.LineNumber);
            });
    }

    [Fact]
    public void EachBadValue_OfAStep_IsReported()
    {
        var diagnostics = Bind("""
            When I assign "Buy milk" to Eva due 12-May-2026 for 1,5 hours as Dnoe
            """);

        Assert.Equal(["UATA003", "UATA004"], diagnostics.Select(d => d.Code));
    }

    [Fact]
    public async Task ARelativeDate_UsesTheFixturesClock()
    {
        var fixture = await Run("""
            When I assign "Buy milk" to Bob due +2d for 1 hours as Open
            """);

        Assert.Equal(new DateOnly(2026, 3, 12), Assert.Single(fixture.State.Assigned).Due);
    }

    [Fact]
    public async Task AUserSubject_ArrivesAsAUserRole()
    {
        var fixture = await Run("""
            When Eva takes "Buy milk"
            """);

        Assert.Equal(("Eva", "Buy milk"), Assert.Single(fixture.State.Taken));
    }

    [Fact]
    public async Task AListOfRecords_ComesFromTheTable()
    {
        var fixture = await Run("""
            Given the server has todos
              | Title          | Status      | Due Date    | Hours    |
              | -------------- | ----------- | ----------- | -------- |
              | Buy milk       | Open        | 12-May-2026 | 0.5      |
              | Plan the party | In Progress |             | 1,250.75 |
            """);

        Assert.Equal(
            [
                new TodoRow("Buy milk", Status.Open, new DateOnly(2026, 5, 12), 0.5m),
                new TodoRow("Plan the party", Status.InProgress, null, 1250.75m),
            ],
            fixture.State.Server);
    }

    [Fact]
    public async Task OneRecord_ComesFromAFieldValueTable()
    {
        var fixture = await Run("""
            When I create the todo
              | Field    | Value            |
              | -------- | ---------------- |
              | Title    | Write the report |
              | Status   | In Progress      |
              | Due Date | 12-May-2026      |
            """);

        Assert.Equal(new TodoRow("Write the report", Status.InProgress, new DateOnly(2026, 5, 12)), Assert.Single(fixture.State.Server));
    }

    [Fact]
    public async Task AListOfValues_IsInline_OrAOneColumnTable()
    {
        var fixture = await Run("""
            Then the todos "Buy milk", "Plan the party" and "Pay rent" should be open
            And the todos should be open
              | Title          |
              | -------------- |
              | Buy milk       |
            """);

        Assert.Equal([["Buy milk", "Plan the party", "Pay rent"], ["Buy milk"]], fixture.State.Checked);
    }

    [Fact]
    public void ABadCell_IsReportedAtItsRow_AndAMissingTable_AtTheStep()
    {
        var diagnostics = Bind("""
            Given the server has todos
              | Title    | Status | Due Date   |
              | -------- | ------ | ---------- |
              | Buy milk | Opne   | 2026-05-12 |
            And the server has todos
            And the server has todos
              | Titel |
              | ----- |
              | x     |
            """);

        Assert.Equal(["UATA004", "UATA002", "UATA007", "UATA006"], diagnostics.Select(d => d.Code));
        Assert.Equal(8, diagnostics[0].Location.LineNumber);
        Assert.Contains("column 'Due Date'", diagnostics[1].Message, StringComparison.Ordinal);
        Assert.Contains("needs a table under it for 'todos' (a row per TodoRow)", diagnostics[2].Message, StringComparison.Ordinal);
        Assert.Contains("Did you mean 'Title'?", diagnostics[3].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADocString_IsText_OrJsonForARecord()
    {
        var fixture = await Run(""""
            When I write the note
              """
              Dear Eva,
                please buy milk.
              """
            And I create the todo
              """
              { "title": "From JSON", "status": "Done" }
              """
            """");

        Assert.Equal("Dear Eva,\n  please buy milk.", fixture.State.Note);
        Assert.Equal(new TodoRow("From JSON", Status.Done), Assert.Single(fixture.State.Server));
    }

    [Fact]
    public async Task InferredPhrases_ComeFromTheMethodName()
    {
        var fixture = new ArgumentFixture();
        var phrases = Runtime(fixture).CreateCommandCatalog().Patterns.Select(p => $"{p.Keyword} {p.Phrase}").ToHashSet();

        Assert.Contains("When I restart the app", phrases);
        Assert.Contains("Then the todo {title} should be synced", phrases);
        Assert.Contains("Given the device is offline", phrases);
        Assert.Contains("When the device is offline", phrases);
        Assert.Contains("When I open the todo {title}", phrases);
        Assert.Contains("When {user} opens the todo {title}", phrases);
        Assert.Contains("Then I export the PDF", phrases);
        Assert.DoesNotContain(phrases, p => p.Contains("whenever", StringComparison.OrdinalIgnoreCase));

        await Run("""
            When Bob opens the todo "Buy milk"
            """, fixture);
        Assert.Equal("Buy milk", Assert.Single(fixture.State.Opened));
    }

    [Fact]
    public void BrokenPhrases_LoadWithTheRest_AndFailTheStepThatUsesThem()
    {
        var catalog = Runtime(new ArgumentFixture()).CreateCommandCatalog();
        var broken = catalog.Patterns.Where(p => p.Problem is not null).ToDictionary(p => $"{p.Keyword} {p.Phrase}", p => p.Problem!.Code);

        Assert.Equal("UATA010", broken["When I forget the due"]);
        Assert.Equal("UATA011", broken["When I number I"]);
        Assert.Equal("UATA011", broken["When I use TITLESTATUS"]);
        Assert.Equal("UATA012", broken["Given two tables"]);
        Assert.Equal("UATA013", broken["Then I restart the app"]);
        Assert.Equal("UATA013", broken["When I twice"]);

        var diagnostics = Bind("""
            When I forget the due
            """);
        var error = Assert.Single(diagnostics);
        Assert.Equal("UATA010", error.Code);
        Assert.Contains("BrokenPhrases.When_I_forget_the_due does not name parameter 'due'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyAttribute_OnAControlVerb_IsUnmatchable()
    {
        var catalog = UatReflectionRuntime.FromRoot(new FrobFixture()).CreateCommandCatalog();

        var declaration = Assert.Single(catalog.Unmatchable);
        Assert.Equal("UATA013", declaration.Problem.Code);
        Assert.EndsWith("FrobControl.Frob", declaration.Problem.Declaration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConverter_ReadsAnInlineValue()
    {
        var fixture = await Run("""
            When I pay 1,250.00 EUR
            """);

        Assert.Equal(new Money(1250m, "EUR"), fixture.State.Paid);
    }

    private static UatReflectionRuntime Runtime(ArgumentFixture fixture)
        => UatReflectionRuntime.FromComposition(fixture, fixture.Composition);

    private static BddCatalogSet CatalogSet(ArgumentFixture fixture)
        => new(Runtime(fixture).CreateCommandCatalog(), BddConfigParser.Parse(Apps).Apps, suiteHead: null, new Dictionary<string, BddCommandCatalog>(), []);

    private static IReadOnlyList<BddDiagnostic> Bind(string steps)
    {
        var parse = BddMarkdownParser.Parse(Document(steps));
        Assert.True(parse.Success, string.Join(Environment.NewLine, parse.Diagnostics.Select(d => d.Message)));
        return BddBinder.Bind(parse.Document!, CatalogSet(new ArgumentFixture())).Diagnostics;
    }

    private static async Task<ArgumentFixture> Run(string steps, ArgumentFixture? fixture = null)
    {
        fixture ??= new ArgumentFixture();
        var parse = BddMarkdownParser.Parse(Document(steps));
        Assert.True(parse.Success, string.Join(Environment.NewLine, parse.Diagnostics.Select(d => d.Message)));
        var bind = BddBinder.Bind(parse.Document!, CatalogSet(fixture));
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(d => $"{d.Code} {d.Message}")));

        using var scope = fixture.Composition.CreateScope();
        var context = new BddExecutionContext { Now = fixture.Now, Apps = BddConfigParser.Parse(Apps).Apps, SessionHost = fixture };
        UatReflectionRuntime.SetServiceProvider(context, scope.ServiceProvider);
        var result = await new BddScenarioRunner(context).RunAsync(Assert.Single(bind.Document!.Scenarios));
        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Steps.Select(s => $"{s.Status}: {s.Invocation.Step.Text} {s.Message}")));
        return fixture;
    }

    private static string Document(string steps) => $"""
        # Feature: Arguments

        ## Scenario: Arguments
        ```gherkin
        {steps}
        ```
        """;
}

public enum Status
{
    Open,
    InProgress,
    Done,
}

public sealed record TodoRow(string Title, Status Status = Status.Open, DateOnly? DueDate = null, decimal Hours = 0);

[BddArgumentType]
public sealed record Money(decimal Amount, string Currency)
{
    public static Money Parse(string text)
    {
        var parts = text.Split(' ');
        return parts.Length == 2
            ? new Money((decimal)BddValues.Read(parts[0], typeof(decimal), BddValueContext.Default)!, parts[1])
            : throw new FormatException("write an amount and a currency, e.g. 1,250.00 EUR");
    }
}

public sealed class ArgumentState
{
    public List<(string Todo, BddUserRole Assignee, DateOnly Due, decimal Hours, Status Status)> Assigned { get; } = [];

    public List<(string User, string Todo)> Taken { get; } = [];

    public List<TodoRow> Server { get; } = [];

    public List<string[]> Checked { get; } = [];

    public List<string> Opened { get; } = [];

    public string? Note { get; set; }

    public Money? Paid { get; set; }
}

[TestModuleScan(typeof(ArgumentFixture), NamespacePrefix = "Bruinet.Uat.Tests.ArgumentCases")]
[BddIgnore]
public sealed class ArgumentFixture : IBddClock, IBddSessionHost
{
    public ArgumentFixture()
    {
        Composition = TestComposition.ForFixture(this, services => services.AddSingleton(State));
    }

    public TestComposition Composition { get; }

    public ArgumentState State { get; } = new();

    public DateTimeOffset Now { get; } = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    public IServiceProvider UseSession(string name) => Composition.CreateScope().ServiceProvider;
}

[BddStepDefinitions]
[BddIgnore]
public sealed class ArgumentPhrases(ArgumentState state) : BddStepDefinitionsBase
{
    [BddWhen("I assign {todo} to {assignee} due {due} for {hours} hours as {status}")]
    public void Assign(string todo, BddUserRole assignee, DateOnly due, decimal hours, Status status)
        => state.Assigned.Add((todo, assignee, due, hours, status));

    [BddWhen("{user} takes {todo}")]
    public void Take(BddUserRole user, string todo) => state.Taken.Add((user.Name, todo));

    [BddGiven("the server has todos")]
    public void ServerHas(IReadOnlyList<TodoRow> todos) => state.Server.AddRange(todos);

    [BddWhen("I create the todo")]
    public void Create(TodoRow todo) => state.Server.Add(todo);

    [BddThen]
    public void The_todos_TITLES_should_be_open(string[] titles) => state.Checked.Add(titles);

    [BddThen("the todos should be open")]
    public void TodosShouldBeOpen(List<string> titles) => state.Checked.Add([.. titles]);

    [BddWhen("I write the note")]
    public void Write(string note) => state.Note = note;

    [BddWhen("I pay {amount}")]
    public void Pay(Money amount) => state.Paid = amount;

    [BddWhen]
    [BddWhen("{user} opens the todo {title}")]
    public void When_I_open_the_todo_TITLE(string title) => state.Opened.Add(title);

    [BddWhen]
    public void IRestartTheApp()
    {
    }

    public void ThenTheTodoTITLEShouldBeSynced(string title)
    {
    }

    [BddGiven]
    [BddWhen]
    public void The_device_is_offline()
    {
    }

    [BddThen]
    public void Then_I_export_the_PDF()
    {
    }

    public void Whenever_nothing()
    {
    }
}

[BddStepDefinitions]
[BddIgnore]
public sealed class BrokenPhrases : BddStepDefinitionsBase
{
    [BddWhen]
    public void When_I_forget_the_due(DateOnly due)
    {
    }

    [BddWhen]
    public void When_I_number_I(int i)
    {
    }

    [BddWhen]
    public void When_I_use_TITLESTATUS(string title, string status)
    {
    }

    [BddGiven("two tables")]
    public void TwoTables(TodoRow[] first, TodoRow[] second)
    {
    }

    [BddThen]
    public void When_I_restart_the_app()
    {
    }

    [BddWhen]
    [BddWhen]
    public void When_I_twice()
    {
    }
}

public sealed class FrobFixture
{
    public FrobPage FrobPage { get; } = new();
}

public sealed class FrobPage
{
    public FrobControl FrobButton { get; } = new();
}

public sealed class FrobControl
{
    [BddWhen]
    public void Frob()
    {
    }
}
