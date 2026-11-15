using Xunit;

namespace Bruinet.Bdd.LanguageServer.Tests;

/// <summary>
/// Hover, go to definition and find references over the Todo MAUI suite. Needs the Todo sample built
/// (<c>dotnet build samples/Todo/Bruinet.Samples.Todo.slnx</c>).
/// </summary>
public sealed class NavigationTests : IAsyncLifetime
{
    private static readonly string Feature = Path.Combine(TodoPaths.Project, "Features", "TOD.01-see-my-todos.feature.md");
    private static readonly string Steps = Path.Combine(TodoPaths.Project, "Features", "Todo.steps.md");
    private static readonly string[] Lines = File.ReadAllLines(Feature);
    private ServerHarness _server = null!;
    private string _uri = null!;

    public async Task InitializeAsync()
    {
        _server = await ServerHarness.StartAsync(TodoPaths.Todo);
        _uri = await _server.OpenAsync(Feature);
    }

    public async Task DisposeAsync() => await _server.DisposeAsync();

    [Fact]
    public async Task Hover_OnAControlVerb_NamesThePageItRunsOn()
    {
        var hover = await _server.AtAsync<Hover>("textDocument/hover", _uri, LineOf("When I tap Sync"), 3);

        Assert.Contains("**binds to**", hover.Contents.Value);
        Assert.Contains("page Todo List", hover.Contents.Value);
    }

    [Fact]
    public async Task Hover_OnACall_ShowsTheCallableAndItsBody()
    {
        var hover = await _server.AtAsync<Hover>("textDocument/hover", _uri, LineOf("Given the synced list has todos"), 3);

        Assert.Contains("**calls** `Step: the synced list has todos` (Todo.steps.md:3)", hover.Contents.Value);
        Assert.Contains("- When I tap Sync", hover.Contents.Value);
    }

    [Fact]
    public async Task Definition_OfACall_IsTheStepHeading()
    {
        var location = Assert.Single(await _server.AtAsync<IReadOnlyList<Location>>("textDocument/definition", _uri, LineOf("Given the synced list has todos"), 3));

        Assert.Equal(new Uri(Steps).AbsoluteUri, location.Uri);
        Assert.Equal(2, location.Range.Start.Line);
    }

    [Fact]
    public async Task Definition_OfAControlVerb_IsTheControlOnThePage()
    {
        var location = Assert.Single(await _server.AtAsync<IReadOnlyList<Location>>("textDocument/definition", _uri, LineOf("When I tap Sync"), 3));

        var path = new Uri(location.Uri).LocalPath;
        Assert.EndsWith(".cs", path);
        Assert.Contains("Sync", File.ReadAllLines(path)[location.Range.Start.Line]);
    }

    [Fact]
    public async Task Definition_OfAPageStep_IsThePageClass()
    {
        var location = Assert.Single(await _server.AtAsync<IReadOnlyList<Location>>("textDocument/definition", _uri, LineOf("Given I am on the Todo List page"), 3));

        Assert.Matches(@"\b(class|record)\s+\w+", File.ReadAllLines(new Uri(location.Uri).LocalPath)[location.Range.Start.Line]);
    }

    [Fact]
    public async Task References_OfAStep_AreEveryStepAcrossTheSuiteBindingToIt()
    {
        var locations = await _server.AtAsync<IReadOnlyList<Location>>("textDocument/references", _uri, LineOf("When I tap Sync"), 3, new { includeDeclaration = false });

        Assert.True(locations.Select(location => location.Uri).Distinct().Count() > 1);
        Assert.All(locations, location => Assert.Contains(" tap", File.ReadAllLines(new Uri(location.Uri).LocalPath)[location.Range.Start.Line]));
    }

    [Fact]
    public async Task References_OfAStepHeading_AreItsCalls()
    {
        var steps = await _server.OpenAsync(Steps);

        var locations = await _server.AtAsync<IReadOnlyList<Location>>("textDocument/references", steps, 2, 5, new { includeDeclaration = true });

        Assert.Contains(locations, location => location.Uri == _uri && location.Range.Start.Line == LineOf("Given the synced list has todos"));
        Assert.Contains(locations, location => location.Uri == steps && location.Range.Start.Line == 2);
    }

    [Fact]
    public async Task Usages_OfAStepDefinition_AreItsStepsAcrossTheSuites()
    {
        var hover = await _server.AtAsync<Hover>("textDocument/hover", _uri, LineOf("And the server answers after 2 seconds"), 5);
        var method = System.Text.RegularExpressions.Regex.Match(hover.Contents.Value, "binds to\\*\\* `([^`]+)`").Groups[1].Value;

        var usages = await _server.Client.InvokeWithParameterObjectAsync<UsagesResult>("bruinet/usages", new { symbol = method + "(int)" });

        Assert.Contains(usages.Usages, usage => usage.Uri == _uri && usage.Line == LineOf("And the server answers after 2 seconds"));
        Assert.Equal(usages.Usages.Count, usages.Steps);
        Assert.True(usages.Scenarios >= 1);
    }

    [Fact]
    public async Task Usages_OfAPageControl_AreTheControlVerbsOnItsPage()
    {
        using var suite = Workspace.BddSuite.Open(Path.Combine(TodoPaths.Project, Bdd.BddFileFormat.ConfigFileName));
        var page = UatPageSet.Of(suite.TryGetCatalogs(out _)!.Default).Values.Single(model => model.Name == "Todo List");

        var usages = await _server.Client.InvokeWithParameterObjectAsync<UsagesResult>("bruinet/usages", new { symbol = page.PageType.FullName!.Replace('+', '.') + ".Sync" });

        Assert.Contains(usages.Usages, usage => usage.Uri == _uri && usage.Line == LineOf("When I tap Sync"));
        Assert.All(usages.Usages, usage => Assert.Contains("Sync", usage.Text));
    }

    [Fact]
    public async Task Explorer_Steps_GroupsTheSteps_AndOpensEachCallable()
    {
        var steps = await _server.Client.InvokeWithParameterObjectAsync<ExplorerResult>("bruinet/explorer", new { view = "steps", suite = "Bruinet.Samples.Todo.Uat" });

        Assert.Equal("Bruinet.Samples.Todo.Uat", steps.Suite);
        Assert.Contains("Bruinet.Samples.Todo.Web.Uat", steps.Suites);
        Assert.Contains("<h3>Control verbs", steps.Html);
        Assert.Contains("I tap {control}", steps.Html);
        Assert.Contains($"data-open=\"{new Uri(Steps).AbsoluteUri}\" data-line=\"3\"><span class=\"uat-kind\">Step</span>the synced list has todos", steps.Html);
    }

    [Fact]
    public async Task Explorer_Pages_ListsEachPageWithItsControls()
    {
        var pages = await _server.Client.InvokeWithParameterObjectAsync<ExplorerResult>("bruinet/explorer", new { view = "pages", suite = "Bruinet.Samples.Todo.Uat" });

        Assert.Contains("Todo List", pages.Html);
        Assert.Matches("data-open=\"file:[^\"]+\\.cs\" data-line=\"\\d+\"><span class=\"uat-kind\">control</span>Sync<", pages.Html);
    }

    private static int LineOf(string text) => Array.IndexOf(Lines, text);
}
