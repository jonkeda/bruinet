using Bruinet.Bdd;
using Bruinet.Mocking.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.Mocking.Tests.Api;

/// <summary>The MockRequests steps, bound and run as a scenario against a started server.</summary>
public sealed class MockRequestStepsTests : IDisposable
{
    private readonly MockApiServer _server = MockApiServer.Start();

    public MockRequestStepsTests()
    {
        _server.Post("/contact").ReturnsStatus(201);
        _server.Post("/token").ReturnsStatus(200);
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task TheSteps_PassOnWhatWasSent()
    {
        var run = await RunAsync("""
            When I post '{"name":"Ada","address":{"city":"Delft"},"lines":[{"amount":3}],"note":null}' to /contact
            And I post form 'grant_type=refresh&scope=api' to /token
            Then one POST request was sent to /contact
            And 1 POST requests were sent to /token
            And no PUT was sent to /contact
            And the last POST to /contact has
              | Field          | Value     |
              |---|---|
              | name           | Ada       |
              | Address.City   | Delft     |
              | lines.0.amount | 3         |
              | note           | (absent)  |
              | phone          | (absent)  |
            And the last POST to /token has
              | Field      | Value   |
              |---|---|
              | grant_type | refresh |
            """);

        Assert.True(run.Passed, Failure(run));
    }

    [Fact]
    public async Task NoneSent_FailsWhenOneWasSent()
    {
        var run = await RunAsync("""
            When I post '{"name":"Ada"}' to /contact
            Then no POST was sent to /contact
            """);

        Assert.Contains("No POST to /contact was expected", Failure(run), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastHas_NamesEachFieldThatDiffers()
    {
        var run = await RunAsync("""
            When I post '{"name":"Ada","city":"Delft"}' to /contact
            Then the last POST to /contact has
              | Field | Value    |
              |---|---|
              | name  | Grace    |
              | city  | (absent) |
            """);

        var failure = Failure(run);
        Assert.Contains("name: expected Grace, sent Ada", failure, StringComparison.Ordinal);
        Assert.Contains("city: expected (absent), sent Delft", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyThisScenariosRequestsCount()
    {
        using (var client = new HttpClient { BaseAddress = new Uri(_server.BaseUrl) })
        {
            using var _ = await client.PostAsync("/contact", new StringContent("{}"));
        }

        var run = await RunAsync("""
            Then no POST was sent to /contact
            """);

        Assert.True(run.Passed, Failure(run));
    }

    private async Task<BddScenarioRunResult> RunAsync(string steps)
    {
        var markdown = $"""
            # Feature: Mock requests {Guid.NewGuid():N}

            ## Metadata

            | Field | Value |
            |---|---|
            | Using | {MockRequestSteps.Namespace} |

            ## Scenario: Requests

            ```gherkin
            {steps}
            ```
            """;
        var parse = BddMarkdownParser.Parse(markdown, Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.feature.md"));
        Assert.True(parse.Success, string.Join(Environment.NewLine, parse.Diagnostics.Select(x => x.Message)));
        var catalog = BddStepDefinitionRuntime.FromTypes(new object(), typeof(MockRequestSteps), typeof(SendSteps)).CreateCommandCatalog();
        var bind = BddBinder.Bind(parse.Document!, catalog);
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => x.Message)));

        using var services = new ServiceCollection()
            .AddSingleton(_server)
            .AddTransient<MockRequestSteps>()
            .AddTransient<SendSteps>()
            .BuildServiceProvider();
        var runner = new BddScenarioRunner();
        BddStepDefinitionRuntime.SetServiceProvider(runner.Context, services);
        return await runner.RunAsync(Assert.Single(bind.Document!.Scenarios));
    }

    private static string Failure(BddScenarioRunResult run)
        => string.Join(Environment.NewLine, run.Steps.Where(step => step.Status == BddStepResultStatus.Failed).Select(step => step.Message));

    [BddStepDefinitions(Namespace = MockRequestSteps.Namespace)]
    public sealed class SendSteps(MockApiServer server)
    {
        [BddWhen("I post {body} to {path}")]
        public void PostJson(string body, string path) => Send(path, new StringContent(body.Trim('\''), System.Text.Encoding.UTF8, "application/json"));

        [BddWhen("I post form {body} to {path}")]
        public void PostForm(string body, string path) => Send(path, new StringContent(body.Trim('\''), System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

        private void Send(string path, HttpContent content)
        {
            using var client = new HttpClient { BaseAddress = new Uri(server.BaseUrl) };
            using var response = client.PostAsync(path, content).GetAwaiter().GetResult();
        }
    }
}
