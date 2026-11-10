using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>A step that starts with an app or user runs on that app, through a phrase declared with <c>{user}</c> first (AD-015).</summary>
public sealed class UatSubjectTests
{
    private const string Apps = """
        ## Apps

        | Name | User | Head |
        | --- | --- | --- |
        | Daan's PC | Daan | |
        | Daan's phone | Daan | |
        | Eva | | |
        | Eva's phone | Eva | |
        | Finn's web | Finn | web |
        """;

    [Theory]
    [InlineData("When Eva taps OK", "Test.Tap")]
    [InlineData("Given Eva is on the Main page", "Test.Open")]
    [InlineData("Then Eva should be on the Main page", "Test.AssertOpen")]
    [InlineData("When Eva presses Enter", "Test.Press")]
    public void ASubject_BindsTheDeclaredNamedPhrase_OnItsApp(string step, string commandId)
    {
        var invocation = BindSingle(step);

        Assert.Equal(commandId, invocation.CommandId);
        Assert.Equal("Eva", invocation.App);
    }

    [Fact]
    public void ASubject_FillsThePhrasesArguments()
    {
        var invocation = BindSingle("When Eva presses Enter");

        Assert.Equal("Enter", invocation.Arguments["key"]);
        Assert.Equal("Eva", invocation.Arguments["user"]);
        Assert.Equal("Eva presses Enter", invocation.Step.Text);
    }

    [Fact]
    public void AVerbForm_NobodyDeclared_DoesNotBind()
    {
        // "I tap {control}" has a named form "{user} taps {control}"; "tap" after a name is not it.
        var diagnostic = Assert.Single(Bind("When Eva tap OK").Diagnostics);

        Assert.Equal("UATB001", diagnostic.Code);
    }

    [Fact]
    public void AnIPhrase_WithoutANamedForm_CannotBeSaidOfAUser()
    {
        // "I advance the clock" is declared, but not "{user} advances the clock".
        var diagnostic = Assert.Single(Bind("When Eva advances the clock").Diagnostics);

        Assert.Equal("UATB001", diagnostic.Code);
        Assert.Contains("binds only to a phrase declared with {user} first", diagnostic.Message);
        Assert.Contains("'{user} presses {key}'", diagnostic.Message);
        Assert.Contains("'{user} taps {control}'", diagnostic.Message);
    }

    [Fact]
    public void ASubject_Sticks_UntilAnotherIsNamed()
    {
        var invocations = BindScenario("""
            When I tap OK
            And Eva taps OK
            And I tap OK
            And Finn's web taps OK
            And I tap OK
            """);

        Assert.Equal([null, "Eva", "Eva", "Finn's web", "Finn's web"], invocations.Select(invocation => invocation.App));
    }

    [Fact]
    public void TheLongestName_Wins()
    {
        Assert.Equal("Eva's phone", BindSingle("When Eva's phone taps OK").App);
    }

    [Fact]
    public void AUser_WithOneApp_PicksThatApp()
    {
        Assert.Equal("Finn's web", BindSingle("When Finn taps OK").App);
    }

    [Fact]
    public void AUser_WithSeveralApps_PicksTheOneUsedLast()
    {
        var invocations = BindScenario("""
            When Daan's phone taps OK
            And Daan taps OK
            """);

        Assert.Equal("Daan's phone", invocations[1].App);
    }

    [Fact]
    public void AUser_WithSeveralApps_NoneUsedYet_FailsNamingTheApps()
    {
        var result = Bind("When Daan taps OK");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("UATB006", diagnostic.Code);
        Assert.Contains("Daan's PC, Daan's phone", diagnostic.Message);
    }

    [Fact]
    public void AStepThatStartsWithAControl_IsNotASubject()
    {
        var invocation = BindSingle("""Then Todos should have todo "Buy milk" """);

        Assert.Equal("Test.HasTodo", invocation.CommandId);
        Assert.Null(invocation.App);
    }

    [Fact]
    public void AName_NotInTheAppsTable_FailsNamingTheApps()
    {
        var diagnostic = Assert.Single(Bind("When Dan taps OK").Diagnostics);

        Assert.Equal("UATB007", diagnostic.Code);
        Assert.Contains("no app or user 'Dan'", diagnostic.Message);
    }

    [Fact]
    public void ASubject_MatchingTwoNamedPhrases_IsAmbiguous()
    {
        var catalog = CreateCatalog();
        catalog.Register(BddEffectiveStepKeyword.When, "{user} taps {target}", "Test.TapTarget");

        var diagnostic = Assert.Single(Bind("When Eva taps OK", catalog).Diagnostics);

        Assert.Equal("UATB002", diagnostic.Code);
    }

    [Fact]
    public void UsesTheApp_SwitchesTheAppForLaterSteps()
    {
        var invocations = BindScenario("""
            Given Daan's PC uses the app
            When Daan taps OK
            """);

        Assert.Equal(["Daan's PC", "Daan's PC"], invocations.Select(invocation => invocation.App));
        Assert.Equal("Daan's PC", invocations[0].Arguments["app"]);
    }

    [Fact]
    public void AnAppOfAnotherHead_BindsAgainstThatHeadsCatalog()
    {
        var web = new BddCommandCatalog();
        web.Register(BddEffectiveStepKeyword.When, "I tap {control}", "Web.Tap");
        web.Register(BddEffectiveStepKeyword.When, "{user} taps {control}", "Web.Tap");

        var invocations = BindScenario(
            """
            When Eva taps OK
            And Finn taps OK
            And I tap OK
            """,
            catalogsByHead: new Dictionary<string, BddCommandCatalog> { ["web"] = web });

        Assert.Equal(["Test.Tap", "Web.Tap", "Web.Tap"], invocations.Select(invocation => invocation.CommandId));
    }

    [Fact]
    public void AUserParameter_InFirstPlace_SwitchesToThatUsersApp()
    {
        var invocation = BindSingle("Then Finn should have 3 todos on the server");

        Assert.Equal("Test.ServerCount", invocation.CommandId);
        Assert.Equal("Finn", invocation.Arguments["user"]);
        Assert.Equal("Finn's web", invocation.App);
    }

    [Fact]
    public void AUserParameter_SaidAsI_IsTheCurrentAppsUser()
    {
        var invocations = BindScenario("""
            When Finn's web taps OK
            Then I should have 3 todos on the server
            """);

        Assert.Equal("Finn", invocations[1].Arguments["user"]);
    }

    [Fact]
    public void AUserParameter_SaidAsI_WithNoAppCurrent_Fails()
    {
        var diagnostic = Assert.Single(Bind("Then I should have 3 todos on the server").Diagnostics);

        Assert.Equal("UATB007", diagnostic.Code);
        Assert.Contains("no app is current", diagnostic.Message);
    }

    [Fact]
    public void AUserParameter_InALaterPlace_IsCheckedButDoesNotSwitch()
    {
        var invocation = BindSingle("""Then the server should have received "Milk" from Finn""");

        Assert.Equal("Finn", invocation.Arguments["user"]);
        Assert.Null(invocation.App);
    }

    [Fact]
    public void AnUnknownUser_InAUserParameter_Fails()
    {
        var diagnostic = Assert.Single(Bind("""Then the server should have received "Milk" from Zed""").Diagnostics);

        Assert.Equal("UATB007", diagnostic.Code);
        Assert.Contains("no app or user 'Zed'", diagnostic.Message);
    }

    [Fact]
    public void AnAppNamedLikeAControl_IsAnError()
    {
        var catalogs = new BddCatalogSet(CreateCatalog(), ReadApps(), suiteHead: null, new Dictionary<string, BddCommandCatalog>(), ["eva"]);

        var result = BddBinder.Bind(Parse("When I tap OK"), catalogs);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("UATB005", diagnostic.Code);
        Assert.Contains("'Eva'", diagnostic.Message);
    }

    [Fact]
    public void WithoutApps_ANamedPhrase_IsAnError()
    {
        var result = BddBinder.Bind(Parse("When Eva taps OK"), CreateCatalog());

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("UATB007", diagnostic.Code);
        Assert.Contains("no Apps table", diagnostic.Message);
    }

    [Fact]
    public void AppsTable_UserAndHead_DefaultToTheNameAndTheSuite()
    {
        var apps = ReadApps();

        Assert.Equal("Eva", apps.Single(app => app.Name == "Eva").User);
        Assert.Equal("Daan", apps.Single(app => app.Name == "Daan's PC").User);
        Assert.Null(apps.Single(app => app.Name == "Eva").Head);
        Assert.Equal("web", apps.Single(app => app.Name == "Finn's web").Head);
    }

    private static BddStepInvocation BindSingle(string step)
        => Assert.Single(BindScenario(step));

    private static IReadOnlyList<BddStepInvocation> BindScenario(
        string steps,
        IReadOnlyDictionary<string, BddCommandCatalog>? catalogsByHead = null)
    {
        var catalogs = new BddCatalogSet(CreateCatalog(), ReadApps(), suiteHead: null, catalogsByHead ?? new Dictionary<string, BddCommandCatalog>(), []);
        var result = BddBinder.Bind(Parse(steps), catalogs);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
        return Assert.Single(result.Document!.Scenarios).Invocations;
    }

    private static BddBindResult Bind(string steps, BddCommandCatalog? catalog = null)
        => BddBinder.Bind(
            Parse(steps),
            new BddCatalogSet(catalog ?? CreateCatalog(), ReadApps(), suiteHead: null, new Dictionary<string, BddCommandCatalog>(), []));

    private static BddDocument Parse(string steps)
    {
        var result = BddMarkdownParser.Parse($"""
            # Feature: Subjects

            ## Scenario: Subjects
            ```gherkin
            {steps}
            ```
            """);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
        return result.Document!;
    }

    private static IReadOnlyList<BddAppConfig> ReadApps() => BddConfigParser.Parse($"# UAT Config\n\n{Apps}").Apps;

    private static BddCommandCatalog CreateCatalog()
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(BddEffectiveStepKeyword.Given, "I am on the {page} page", "Test.Open");
        catalog.Register(BddEffectiveStepKeyword.Given, "{user} is on the {page} page", "Test.Open");
        catalog.Register(BddEffectiveStepKeyword.Given, "{app} uses the app", "Test.Use");
        catalog.Register(BddEffectiveStepKeyword.When, "{app} uses the app", "Test.Use");
        catalog.Register(BddEffectiveStepKeyword.When, "I tap {control}", "Test.Tap");
        catalog.Register(BddEffectiveStepKeyword.When, "{user} taps {control}", "Test.Tap");
        catalog.Register(BddEffectiveStepKeyword.When, "I advance the clock", "Test.Advance");
        catalog.Register(BddEffectiveStepKeyword.When, "I press {key}", "Test.Press");
        catalog.Register(BddEffectiveStepKeyword.When, "{user} presses {key}", "Test.Press");
        catalog.Register(BddEffectiveStepKeyword.Then, "I should be on the {page} page", "Test.AssertOpen");
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should be on the {page} page", "Test.AssertOpen");
        catalog.Register(BddEffectiveStepKeyword.Then, "{control} should have todo {title}", "Test.HasTodo");
        catalog.Register(BddEffectiveStepKeyword.Then, "{user} should have {count} todos on the server", "Test.ServerCount");
        catalog.Register(BddEffectiveStepKeyword.Then, "the server should have received {title} from {user}", "Test.ServerReceived");
        return catalog;
    }
}
