using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.SessionCases;

public sealed class UatSessionTests
{
    [Fact]
    public void Config_ReadsTheAppsTable()
    {
        var config = BddConfigParser.Parse("""
            # UAT Config

            ## Apps

            | Name | User |
            | --- | --- |
            | Alice | alice |
            | Bob | bob |
            """);

        Assert.Equal(["Alice", "Bob"], config.Apps.Select(app => app.Name));
        Assert.Equal("bob", config.Apps[1].Values["User"]);
    }

    [Fact]
    public async Task UsesTheApp_ResolvesLaterStepsThroughThatAppsServices()
    {
        using var suite = new Suite();
        var fixture = new SessionFixture();

        await suite.Run(fixture, """
            # Feature: Two apps

            ## Scenario: Each step runs on the current app

            ```gherkin
            When I note the app
            And Bob uses the app
            And I note the app
            And Alice uses the app
            And I note the app
            ```
            """);

        Assert.Equal(["default", "Bob", "Alice"], fixture.Noted);
    }

    [Fact]
    public async Task ASubject_RunsItsStepOnThatApp_AndLaterIStepsToo()
    {
        using var suite = new Suite();
        var fixture = new SessionFixture();

        await suite.Run(fixture, """
            # Feature: Two apps

            ## Scenario: Subjects switch the app

            ```gherkin
            When I note the app
            And Bob notes the app
            And I note the app
            And Alice notes the app
            ```
            """);

        Assert.Equal(["default", "Bob", "Bob", "Alice"], fixture.Noted);
    }

    [Fact]
    public async Task UsesTheApp_AnAppNotInTheAppsTable_FailsNamingTheApps()
    {
        using var suite = new Suite();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => suite.Run(new SessionFixture(), """
            # Feature: Two apps

            ## Scenario: An unknown app

            ```gherkin
            When Carol uses the app
            ```
            """));

        Assert.Contains("No app 'Carol'", error.Message);
        Assert.Contains("'Alice', 'Bob'", error.Message);
    }

    [Fact]
    public async Task AFailure_AfterASwitch_NamesTheApp()
    {
        using var suite = new Suite();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => suite.Run(new SessionFixture(), """
            # Feature: Two apps

            ## Scenario: A failure on Bob's app

            ```gherkin
            When Bob uses the app
            Then the step fails
            ```
            """));

        Assert.Contains("[Bob] ", error.Message);
    }

    /// <summary>A config with Alice and Bob, in a folder of its own.</summary>
    private sealed class Suite : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-sessions-{Guid.NewGuid():N}");

        public Suite()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(ConfigFilePath, $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | TEST |

                ## Apps

                | Name |
                | --- |
                | Alice |
                | Bob |

                ## Reporting

                | Field | Value |
                | --- | --- |
                | OutputDirectory | {{Path.Combine(_directory, "uat")}} |
                """);
        }

        private string ConfigFilePath => Path.Combine(_directory, "uat.config.md");

        public Task Run(SessionFixture fixture, string scenario)
        {
            var filePath = Path.Combine(_directory, "sessions.feature.md");
            File.WriteAllText(filePath, scenario);
            return new Harness(fixture, ConfigFilePath).RunFileAsync(filePath);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private sealed class Harness(SessionFixture fixture, string configFilePath)
        : UatScenarioTestBase<SessionFixture>(fixture)
    {
        protected override string ConfigFilePath => configFilePath;

        protected override UatRuntimeValidationOptions RuntimeValidation { get; } = new(RequireAssemblies: false);

        public Task RunFileAsync(string filePath) => RunUatFileAsync(filePath);
    }

    /// <summary>One composition per app; each knows its app's name.</summary>
    [TestModuleScan(typeof(UatSessionTests), NamespacePrefix = "Bruinet.Uat.Tests.SessionCases")]
    [BddIgnore]
    public sealed class SessionFixture : IBddSessionHost
    {
        public SessionFixture()
        {
            Composition = For("default");
        }

        public TestComposition Composition { get; }

        public List<string> Noted { get; } = [];

        public IServiceProvider UseSession(string name) => For(name).CreateScope().ServiceProvider;

        private TestComposition For(string app)
            => TestComposition.ForFixture(this, services => services.AddSingleton(new AppName(app)));
    }

    public sealed record AppName(string Value);

    [BddStepDefinitions]
    [BddIgnore]
    public sealed class SessionPhrases(AppName app, SessionFixture fixture) : BddStepDefinitionsBase
    {
        [BddWhen("I note the app")]
        [BddWhen("{user} notes the app")]
        public void Note() => fixture.Noted.Add(app.Value);

        [BddThen("the step fails")]
        public void Fail() => throw new InvalidOperationException("boom");
    }
}
