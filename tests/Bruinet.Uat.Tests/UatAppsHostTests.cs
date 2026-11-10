using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Bruinet.Core.Configuration;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.AppsHostCases;

/// <summary>A fixture whose apps are a <see cref="TestAppSet"/> gets its sessions from it.</summary>
public sealed class UatAppsHostTests
{
    private const string AppsFile = """
        # Apps

        ## Apps

        | Name | User |
        | --- | --- |
        | Alice's PC | Alice |
        | Bob | |
        """;

    [Fact]
    public void AppsFile_IsReadRelativeToTheConfig()
    {
        using var suite = new Suite();

        var config = BddConfigParser.ParseFile(suite.ConfigFilePath);

        Assert.Equal(["Alice's PC", "Bob"], config.Apps.Select(app => app.Name));
        Assert.Equal("Alice", config.Apps[0].User);
    }

    [Fact]
    public void AppsFile_TheBuildCopiesIn_IsReadFromTheOutput_ForASourceConfig()
    {
        using var suite = new Suite();
        var name = $"apps-{Guid.NewGuid():N}.md";
        var built = Path.Combine(AppContext.BaseDirectory, name);
        File.WriteAllText(built, AppsFile);
        File.WriteAllText(suite.ConfigFilePath, File.ReadAllText(suite.ConfigFilePath).Replace("shared/apps.md", name, StringComparison.Ordinal));
        try
        {
            // As a runner reads its suite's source config, where the apps file is not (AD-022).
            var config = BddConfigParser.ParseFile(suite.ConfigFilePath);

            Assert.Equal(["Alice's PC", "Bob"], config.Apps.Select(app => app.Name));
        }
        finally
        {
            File.Delete(built);
        }
    }

    [Fact]
    public async Task EachApp_StartsOnce_AndKeepsItsScope()
    {
        using var suite = new Suite();
        var fixture = new AppsFixture();

        await suite.Run(fixture, """
            # Feature: Apps host

            ## Scenario: Subjects use the fixture's apps

            ```gherkin
            When Alice notes the app
            And Bob notes the app
            And Alice notes the app
            ```
            """);

        Assert.Equal(["Alice's PC#1", "Bob#1", "Alice's PC#2"], fixture.Noted);
        Assert.Equal(["Alice's PC", "Bob"], fixture.Apps.Started.Select(app => app.Definition.Name));
        Assert.Equal(["Alice's PC", "Bob", "Alice's PC"], fixture.Used);
    }

    private sealed class Suite : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-apps-{Guid.NewGuid():N}");

        public Suite()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "shared"));
            File.WriteAllText(Path.Combine(_directory, "shared", "apps.md"), AppsFile);
            File.WriteAllText(ConfigFilePath, $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | TEST |
                | AppsFile | shared/apps.md |

                ## Reporting

                | Field | Value |
                | --- | --- |
                | OutputDirectory | {{Path.Combine(_directory, "uat")}} |
                """);
        }

        public string ConfigFilePath => Path.Combine(_directory, "uat.config.md");

        public Task Run(AppsFixture fixture, string scenario)
        {
            var filePath = Path.Combine(_directory, "apps.feature.md");
            File.WriteAllText(filePath, scenario);
            return new Harness(fixture, ConfigFilePath).RunFileAsync(filePath);
        }

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private sealed class Harness(AppsFixture fixture, string configFilePath)
        : UatScenarioTestBase<AppsFixture>(fixture)
    {
        protected override string ConfigFilePath => configFilePath;

        protected override UatRuntimeValidationOptions RuntimeValidation { get; } = new(RequireAssemblies: false);

        public Task RunFileAsync(string filePath) => RunUatFileAsync(filePath);
    }
}

[TestModuleScan(typeof(AppsFixture), NamespacePrefix = "Bruinet.Uat.Tests.AppsHostCases")]
[BddIgnore]
public sealed class AppsFixture : IUatAppsHost
{
    public AppsFixture()
    {
        Composition = TestComposition.ForFixture(this, services => services.AddScoped(_ => new AppCounter("default")));
        Apps = new TestAppSet(
            TestAppDefinition.ReadMarkdown("""
                ## Apps

                | Name | User |
                | --- | --- |
                | Alice's PC | Alice |
                | Bob | |
                """),
            (definition, _) => Task.FromResult<ITestApp>(new FakeApp(definition)));
    }

    public TestComposition Composition { get; }

    public TestAppSet Apps { get; }

    public List<string> Noted { get; } = [];

    public List<string> Used { get; } = [];

    public void AddAppServices(ITestApp app, IServiceCollection services)
        => services.AddScoped(_ => new AppCounter(app.Definition.Name));

    public void OnAppUsed(ITestApp app) => Used.Add(app.Definition.Name);

    private sealed class FakeApp(TestAppDefinition definition) : ITestApp
    {
        public TestAppDefinition Definition => definition;

        public IScreenshotService Screenshots => throw new NotSupportedException();

        public ValueTask ResetAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Scoped per app: its count shows whether a switch back reused the app's scope.</summary>
public sealed class AppCounter(string app)
{
    private int _count;

    public string Next() => $"{app}#{++_count}";
}

[BddStepDefinitions]
[BddIgnore]
public sealed class AppsHostPhrases(AppCounter counter, AppsFixture fixture) : BddStepDefinitionsBase
{
    [BddWhen("I note the app")]
    [BddWhen("{user} notes the app")]
    public void Note() => fixture.Noted.Add(counter.Next());
}
