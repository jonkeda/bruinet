using Bruinet.Bdd;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests;

public sealed class UatCatalogParityTests
{
    [Fact]
    public void EveryCoreIVerb_HasANamedTwin_WithTheSameCommand()
    {
        // The built-in pairs cannot drift: "I tap {control}" has "{user} taps {control}" (AD-015).
        var patterns = UatSpecCommandCatalog.CreateDefault().Patterns;
        var twins = patterns
            .Where(pattern => pattern.Phrase.StartsWith("{user} ", StringComparison.Ordinal))
            .Select(pattern => (pattern.Keyword, pattern.CommandId))
            .ToHashSet();

        var missing = patterns
            .Where(pattern => pattern.Phrase.StartsWith("I ", StringComparison.Ordinal))
            .Where(pattern => !twins.Contains((pattern.Keyword, pattern.CommandId)))
            .Select(pattern => pattern.Phrase)
            .ToList();

        Assert.True(missing.Count == 0, "These built-in phrases have no {user} form: " + string.Join(", ", missing));
    }

    [Fact]
    public async Task ANamedTwin_PassesTheValue_NotTheUser()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();
        var apps = BddConfigParser.Parse("""
            # UAT Config

            ## Apps

            | Name |
            | --- |
            | Eva |
            """).Apps;
        var parse = BddMarkdownParser.Parse("""
            # Feature: Gadget

            ## Scenario: A named step with a value

            ```gherkin
            Given Eva is on the Gadget page
            When Eva sets the level of Gadget to "7"
            ```
            """);
        var bind = BddBinder.Bind(parse.Document!, new BddCatalogSet(catalog, apps, suiteHead: null, new Dictionary<string, BddCommandCatalog>(), []));
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => $"{x.Code} {x.Message}")));

        var runner = new BddScenarioRunner(new BddExecutionContext { SessionHost = new NoServices() });
        var result = await runner.RunAsync(Assert.Single(bind.Document!.Scenarios));

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("7", fixture.GadgetPage.GadgetButton.Level);
    }

    [Fact]
    public void SpecCatalog_ControlVerbs_ComeFromCoreStepAttributes()
    {
        // Core's controls declare the built-in verbs; the attributes themselves live in Bruinet.Bdd.Abstractions.
        var expected = typeof(Bruinet.Core.Interfaces.IClickableControlObject<>).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<BddStepAttribute>(inherit: false))
            .Select(attribute => (attribute.Keyword, attribute.Phrase))
            .ToHashSet();

        var catalogPhrases = ControlVerbs(UatSpecCommandCatalog.CreateDefault())
            .Select(pattern => (pattern.Keyword, pattern.Phrase))
            .ToHashSet();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, catalogPhrases);
    }

    [Fact]
    public void RuntimeAndSpecProjections_AgreeOnControlVerbs()
    {
        var runtime = ControlVerbs(UatReflectionRuntime.FromRoot(new Fixture()).CreateCommandCatalog())
            .Select(pattern => (pattern.Keyword, pattern.Phrase, Suffix: StripPrefix(pattern.CommandId)))
            .ToHashSet();

        var spec = ControlVerbs(UatSpecCommandCatalog.CreateDefault())
            .Select(pattern => (pattern.Keyword, pattern.Phrase, Suffix: StripPrefix(pattern.CommandId)))
            .ToHashSet();

        Assert.NotEmpty(spec);
        Assert.Equal(spec, runtime);
    }

    [Fact]
    public void CustomControlVerb_OnAppControl_IsDiscovered()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        Assert.Contains(catalog.Patterns, pattern =>
            pattern.Keyword == BddEffectiveStepKeyword.When &&
            pattern.Phrase == "I frobnicate {control}");
    }

    [Fact]
    public async Task BddWhen_OnAppControl_BindsAControlVerbWithItsValueArgument()
    {
        var fixture = new GadgetFixture();
        var runtime = UatReflectionRuntime.FromRoot(fixture);
        var catalog = runtime.CreateCommandCatalog();

        var pattern = Assert.Single(catalog.Patterns, pattern =>
            pattern.Phrase == "I set the level of {control} to {level}");
        Assert.Equal(BddEffectiveStepKeyword.When, pattern.Keyword);
        Assert.Equal("Builtin.SetLevel", pattern.CommandId);

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I set the level of Gadget to "7"
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("7", fixture.GadgetPage.GadgetButton.Level);
    }

    [Fact]
    public async Task BddThen_OnAppControl_CarriesItsLiteral()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var pattern = Assert.Single(catalog.Patterns, pattern =>
            pattern.Phrase == "{control} should not be frobnicated");
        Assert.Equal(BddEffectiveStepKeyword.Then, pattern.Keyword);

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            Then Gadget should not be frobnicated
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.False(fixture.GadgetPage.GadgetButton.LastExpected);
    }

    [Fact]
    public void BddThen_OnControlMethod_IsNotRegisteredAsAPhrase()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var patterns = catalog.Patterns
            .Where(pattern => pattern.Phrase == "{control} should not be frobnicated")
            .ToArray();

        var pattern = Assert.Single(patterns);
        Assert.StartsWith("Builtin.", pattern.CommandId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVerbOnABaseClass_IsFoundThroughTheControlThePageDeclares()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            Then Dial should show the Warm state
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("Warm", fixture.GadgetPage.Dial.LastState);
    }

    [Fact]
    public async Task AControlName_MayTakeThe()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            Then the dial should show the Cold state
            When I set the level of the gadget to "3"
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("Cold", fixture.GadgetPage.Dial.LastState);
        Assert.Equal("3", fixture.GadgetPage.GadgetButton.Level);
    }

    [Fact]
    public async Task APropertyWhoseTypeDeclaresVerbs_IsAControl_WhateverItIsCalled()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I choose "Keep" in the popup
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("Keep", fixture.GadgetPage.Popup.Chosen);
    }

    [Fact]
    public async Task AnItemVerb_PassesTheItemFirst_ThenTheValue()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var pattern = Assert.Single(catalog.Patterns, pattern => pattern.Phrase == "the book {item} should be by {value}");
        Assert.Equal(["item", "value"], pattern.Parameters.Select(parameter => parameter.Name));

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I open the book "Dune"
            Then the book "Dune" should be by "Herbert"
            And the book "Emma" should be lent
            And the book "Dune" should be in
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal(
            ["open Dune", "Dune by Herbert", "Emma lent True", "Dune lent False"],
            fixture.GadgetPage.Books.Calls);
    }

    [Fact]
    public async Task AVerbWithoutControl_TakesATypedValue()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            Then the shelf should hold "Dune", "Emma" and "Ulysses"
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal(["holds Dune|Emma|Ulysses"], fixture.GadgetPage.Books.Calls);
    }

    [Fact]
    public void AVerbWithoutControl_FailsBindingOnAPageWhereNoControlHasIt()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Plain page
            When I open the book "Dune"
            """);

        Assert.Equal("UATB009", error.Code);
        Assert.Contains("no control on the Plain page has 'I open the book {item}'", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AVerbWithoutControl_FailsBindingWhenTwoControlsOfThePageHaveIt()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Library page
            When I open the book "Dune"
            """);

        Assert.Equal("UATB009", error.Code);
        Assert.Contains("is on several controls of the Library page (Fiction, Poetry)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APagePhrase_RecordsItsPage_AndThePageItReturns()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var leave = Assert.Single(catalog.Patterns, pattern => pattern.Phrase == "I leave for the plain page");
        Assert.Equal("Gadget", leave.Page);
        Assert.Equal("Plain", leave.PageAfter);

        var wind = Assert.Single(catalog.Patterns, pattern => pattern.Phrase == "I wind the gadget {turns} times");
        Assert.Equal("Gadget", wind.Page);
        Assert.Null(wind.PageAfter);
    }

    [Fact]
    public async Task APagePhrase_RunsOnItsPage_AndLeavesTheReturnedPageCurrent()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I wind the gadget 3 times
            And I leave for the plain page
            And I choose "Stay" in the popup
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal(3, fixture.GadgetPage.Turns);
        Assert.Equal("Stay", fixture.PlainPage.Popup.Chosen);
        Assert.Null(fixture.GadgetPage.Popup.Chosen);
    }

    [Fact]
    public void APagePhrase_FailsBindingOnAnotherPage()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Plain page
            When I wind the gadget 3 times
            """);

        Assert.Equal("UATB009", error.Code);
        Assert.Contains("'I wind the gadget {turns} times' is a phrase of the Gadget page", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AfterAVerbThatNavigates_TheNextStepThatFitsThePage_KeepsIt()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I push Gadget
            And I choose "Stay" in the popup
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.Equal("Stay", fixture.GadgetPage.Popup.Chosen);
    }

    [Fact]
    public void AfterAVerbThatNavigates_AStepThatDoesNotFitThePage_NamesTheStepThatMayHaveLeftIt()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Gadget page
            When I push Gadget
            And I ring Bell
            """);

        Assert.Equal("UATB008", error.Code);
        Assert.Contains("does not fit the Gadget page, and the page may have changed at 'I push Gadget'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AControlThatSaysWhereItGoes_MakesThatPageCurrent_ForTheBinderAndTheRun()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I push Door
            And I ring Bell
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.True(fixture.PlainPage.Bell.Rung);
    }

    [Fact]
    public async Task AControlThatSaysWhereItGoes_WinsOverTheVerbReturningItsOwnPage()
    {
        // A link on a page: its Click returns the page it is on, but the link says where it goes.
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I follow Exit
            And I ring Bell
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.True(fixture.PlainPage.Bell.Rung);
    }

    [Fact]
    public async Task AControlVerbThatReturnsAPage_MakesThatPageCurrent()
    {
        var fixture = new GadgetFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Gadget page
            When I read the book "Dune"
            And I ring Bell
            """);

        Assert.True(result.Passed, FormatResults(result));
        Assert.True(fixture.PlainPage.Bell.Rung);
    }

    [Fact]
    public void APhraseOfThePage_AndAWiderPhrase_ForOneStep_AreAmbiguous()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Gadget page
            Then the gadget should hum
            """);

        Assert.Equal("UATB002", error.Code);
        Assert.Contains("on the Gadget page it matches", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APageThatDoesNotExist_FailsBinding()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();

        var error = BindError(catalog, """
            Given I am on the Nowhere page
            """);

        Assert.Equal("UATB009", error.Code);
        Assert.Contains("names the page 'Nowhere', which does not exist", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePage_IsFollowedThroughACallablesSteps()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GadgetFixture()).CreateCommandCatalog();
        var parse = BddMarkdownParser.Parse("""
            # Feature: Gadget

            ## Step: I step outside

            ```gherkin
            When I leave for the plain page
            ```

            ## Scenario: Out and ringing

            ```gherkin
            Given I am on the Gadget page
            When I step outside
            And I ring Bell
            ```
            """);
        Assert.True(parse.Success, string.Join(Environment.NewLine, parse.Diagnostics.Select(x => $"{x.Code} {x.Message}")));

        var bind = BddBinder.Bind(parse.Document!, catalog);

        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
    }

    private static BddDiagnostic BindError(BddCommandCatalog catalog, string steps)
    {
        var parse = BddMarkdownParser.Parse($"""
            # Feature: Gadget

            ## Scenario: Gadget steps
            ```gherkin
            {steps}
            ```
            """);
        Assert.True(parse.Success);
        return Assert.Single(BddBinder.Bind(parse.Document!, catalog).Diagnostics, x => x.Severity == BddDiagnosticSeverity.Error);
    }

    private static async Task<BddScenarioRunResult> RunAsync(BddCommandCatalog catalog, string steps)
    {
        var parse = BddMarkdownParser.Parse($"""
            # Feature: Gadget

            ## Scenario: Gadget steps
            ```gherkin
            {steps}
            ```
            """);
        Assert.True(parse.Success);
        var bind = BddBinder.Bind(parse.Document!, catalog);
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
        return await new BddScenarioRunner().RunAsync(Assert.Single(bind.Document!.Scenarios));
    }

    private static string FormatResults(BddScenarioRunResult result)
    {
        return string.Join(
            Environment.NewLine,
            result.Steps.Select(x => $"{x.Status}: {x.Invocation.Step.Text} {x.Message}"));
    }

    private static IEnumerable<BddCommandPattern> ControlVerbs(BddCommandCatalog catalog)
    {
        return catalog.Patterns.Where(pattern =>
            pattern.CommandId.Contains(".Control.", StringComparison.Ordinal));
    }

    // Command ids are "<projection>.Control.<verb>"; the verb is what must agree across projections.
    private static string StripPrefix(string commandId)
    {
        var firstDot = commandId.IndexOf('.', StringComparison.Ordinal);
        return firstDot < 0 ? commandId : commandId[(firstDot + 1)..];
    }

    private sealed class Fixture
    {
        public FrontPage FrontPage { get; } = new();
    }

    private sealed class FrontPage
    {
        public bool WaitReady(int? timeoutMs = null) => true;
    }

    private sealed class GadgetFixture
    {
        public GadgetFixture()
        {
            GadgetPage = new GadgetPage(PlainPage);
        }

        [BddThen("the gadget should hum")]
        public void Hums()
        {
        }

        public GadgetPage GadgetPage { get; }

        public PlainPage PlainPage { get; } = new();

        public LibraryPage LibraryPage { get; } = new();
    }

    private sealed class PlainPage
    {
        public Popup Popup { get; } = new();

        public BellControl Bell { get; } = new();

        public bool WaitReady(int? timeoutMs = null) => true;
    }

    private sealed class LibraryPage
    {
        public BookList FictionList { get; } = new();

        public BookList PoetryList { get; } = new();

        public bool WaitReady(int? timeoutMs = null) => true;
    }

    // A collection with item verbs: the row is named by {item}, and the collection finds it.
    private sealed class BookList
    {
        public List<string> Calls { get; } = [];

        [BddWhen("I open the book {item}")]
        public void Open(string title) => Calls.Add($"open {title}");

        [BddThen("the book {item} should be by {value}")]
        public void AssertAuthor(string title, string author) => Calls.Add($"{title} by {author}");

        [BddThen("the book {item} should be lent", HasLiteral = true, Literal = true)]
        [BddThen("the book {item} should be in", HasLiteral = true, Literal = false)]
        public void AssertLent(string title, bool lent) => Calls.Add($"{title} lent {lent}");

        [BddWhen("I read the book {item}")]
        public PlainPage Read(string title)
        {
            Calls.Add($"read {title}");
            return new PlainPage();
        }

        [BddThen("the shelf should hold {value}")]
        public void AssertHolds(string[] titles) => Calls.Add($"holds {string.Join('|', titles)}");
    }

    private sealed class GadgetPage(PlainPage plain)
    {
        public int Turns { get; private set; }

        [BddWhen("I wind the gadget {turns} times")]
        public void Wind(int turns) => Turns = turns;

        [BddWhen("I leave for the plain page")]
        public PlainPage Leave() => plain;

        // A page that exposes itself, as Bruinet page objects do: its phrases stay page phrases.
        public GadgetPage Self => this;

        [UatNavigatesTo(typeof(PlainPage))]
        public GadgetControl Door { get; } = new();

        [UatNavigatesTo(typeof(PlainPage))]
        public PageLink Exit => new(this);

        [BddThen("the gadget should hum")]
        public void Hums()
        {
        }

        public GadgetControl GadgetButton { get; } = new();

        public DialControl Dial { get; } = new();

        public Popup Popup { get; } = new();

        public BookList Books { get; } = new();

        public bool WaitReady(int? timeoutMs = null) => true;
    }

    // The gadget's page is a property of the fixture, so a session needs no services of its own.
    private sealed class NoServices : IBddSessionHost
    {
        public IServiceProvider UseSession(string name) => new ServiceCollection().BuildServiceProvider();
    }

    // The verb is declared on the base, as StateContainer's is under an app's own state container.
    private abstract class StateControlBase<TSelf>
        where TSelf : StateControlBase<TSelf>
    {
        public string? LastState { get; private set; }

        [BddThen("{control} should show the {value} state")]
        public TSelf AssertShowsState(string state, int? timeoutMs = null)
        {
            LastState = state;
            return (TSelf)this;
        }
    }

    private sealed class DialControl : StateControlBase<DialControl>
    {
    }

    private sealed class BellControl
    {
        public bool Rung { get; private set; }

        [BddWhen("I ring {control}")]
        public void Ring() => Rung = true;
    }

    // No control suffix in its name or its type's: its own verb makes it a control.
    private sealed class Popup
    {
        public string? Chosen { get; private set; }

        [BddWhen("I choose {value} in {control}")]
        public void Choose(string text) => Chosen = text;
    }

    // A link whose verb returns the page it is on, as a Bruinet control's Click returns its scope.
    private sealed class PageLink(GadgetPage page)
    {
        [BddWhen("I follow {control}", Navigates = true)]
        public GadgetPage Follow() => page;
    }

    private sealed class GadgetControl
    {
        public string? Level { get; private set; }

        public bool? LastExpected { get; private set; }

        [BddWhen("I push {control}", Navigates = true)]
        public void Push()
        {
        }

        [BddWhen("I frobnicate {control}", CommandId = "Gadget.Frobnicate")]
        public void Frobnicate(int? timeoutMs = null)
        {
        }

        [BddWhen("I set the level of {control} to {level}")]
        [BddWhen("{user} sets the level of {control} to {level}")]
        public void SetLevel(string level, int? timeoutMs = null)
        {
            Level = level;
        }

        [BddThen("{control} should not be frobnicated", HasLiteral = true, Literal = false)]
        public void AssertFrobnicated(bool expected, int? timeoutMs = null)
        {
            LastExpected = expected;
        }
    }
}
