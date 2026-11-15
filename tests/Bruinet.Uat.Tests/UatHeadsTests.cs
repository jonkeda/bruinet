using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.HeadCases
{
    /// <summary>Apps of two heads in one scenario, each binding against its own head's pages (AD-014).</summary>
    public sealed class UatHeadsTests
    {
        [Fact]
        public async Task EachApp_OpensItsOwnHeadsPage_OfTheSameName()
        {
            var fixture = new HeadsFixture();

            await Run(fixture, """
                # Feature: Two heads

                ## Scenario: The same page name on two heads

                ```gherkin
                Given Mia is on the Main page
                And Wendy is on the Main page
                And Mia is on the Main page
                ```
                """);

            Assert.Equal(["maui", "web", "maui"], fixture.Opened);
        }

        [Fact]
        public void TheRuntime_BuildsACatalogForTheOtherHead()
        {
            var directory = CreateSuite(out var configFilePath);
            try
            {
                var runtime = new UatRuntime(new HeadsFixture(), configFilePath, new BddRuntimeValidationOptions(RequireAssemblies: false));

                var catalogs = runtime.CreateCatalogSet();

                Assert.Equal(["web"], catalogs.Heads);
                Assert.NotSame(catalogs.Default, catalogs.CatalogFor(catalogs.FindApp("Wendy")));
                Assert.Same(catalogs.Default, catalogs.CatalogFor(catalogs.FindApp("Mia")));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void EachApp_KeepsItsOwnCurrentPage()
        {
            var context = new BddExecutionContext { SessionHost = new HeadsFixture() };

            context.SwitchApp("Mia");
            context.CurrentPageName = "Edit";
            context.SwitchApp("Wendy");
            var wendysFirstPage = context.CurrentPageName;
            context.CurrentPageName = "List";
            context.SwitchApp("Mia");

            Assert.Null(wendysFirstPage);
            Assert.Equal("Edit", context.CurrentPageName);
            Assert.Equal("Mia", context.CurrentSession);
        }

        [Fact]
        public void AMixedSuite_RunsOnlyMixedScenarios()
        {
            var mixed = BddConfigParser.Parse("""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Head | mixed |
                """);
            var maui = BddConfigParser.Parse("""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Head | maui |
                """);

            Assert.False(mixed.EvaluateSkip(["@mixed"]).ShouldSkip);
            Assert.Contains("@mixed", mixed.EvaluateSkip(["@sync"]).Reason);
            Assert.True(mixed.EvaluateSkip(["@web"]).ShouldSkip);
            Assert.True(maui.EvaluateSkip(["@mixed"]).ShouldSkip);
            Assert.False(maui.EvaluateSkip(["@sync"]).ShouldSkip);
        }

        [Theory]
        [InlineData("service", new[] { "@sync" }, true)]
        [InlineData("service", new[] { "@service" }, false)]
        [InlineData("service", new[] { "@service", "@maui" }, false)]
        [InlineData("service", new[] { "@maui" }, true)]
        [InlineData("service", new[] { "@mixed" }, true)]
        [InlineData("maui", new[] { "@service" }, true)]
        [InlineData("maui", new[] { "@service", "@maui" }, false)]
        [InlineData("web", new[] { "@service", "@maui" }, true)]
        [InlineData("maui", new[] { "@sync" }, false)]
        public void AServiceSuite_RunsOnlyServiceScenarios_AndAServiceScenarioAlsoTaggedForAUiHead_RunsThereToo(string head, string[] tags, bool skipped)
        {
            var config = BddConfigParser.Parse($"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Head | {head} |
                """);

            Assert.Equal(skipped, config.EvaluateSkip(tags).ShouldSkip);
            Assert.Equal(!skipped, config.RunsOnHead(tags));
        }

        [Fact]
        public void AServiceSuite_ListsOnlyItsScenarios_AndAUiSuiteStillListsEveryScenario()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-service-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(directory, "Features"));
            try
            {
                File.WriteAllText(Path.Combine(directory, "Features", "sync.feature.md"), """
                    # Feature: Sync

                    @service
                    ## Scenario: Headless only

                    ```gherkin
                    Given the device is offline
                    ```

                    @service @maui
                    ## Scenario: Both tiers

                    ```gherkin
                    Given the device is offline
                    ```

                    ## Scenario: On screen

                    ```gherkin
                    Given I am on the Main page
                    ```
                    """);

                string[] Listed(string head)
                {
                    File.WriteAllText(Path.Combine(directory, "bdd.config.md"), $"""
                        # UAT Config

                        ## Runtime

                        | Field | Value |
                        | --- | --- |
                        | Head | {head} |
                        """);
                    return BddScenarioSource.GetScenarioTheoryData(baseDirectory: directory).Select(item => (string)item[1]).ToArray();
                }

                Assert.Equal(["Headless only", "Both tiers"], Listed("service"));
                Assert.Equal(["Headless only", "Both tiers", "On screen"], Listed("maui"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static async Task Run(HeadsFixture fixture, string scenario)
        {
            var directory = CreateSuite(out var configFilePath);
            try
            {
                var filePath = Path.Combine(directory, "heads.feature.md");
                File.WriteAllText(filePath, scenario);
                await new Harness(fixture, configFilePath).RunFile(filePath);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string CreateSuite(out string configFilePath)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-heads-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            configFilePath = Path.Combine(directory, "bdd.config.md");
            File.WriteAllText(configFilePath, $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | TEST |
                | Head | maui |

                ## Apps

                | Name | Head |
                | --- | --- |
                | Mia | |
                | Wendy | web |

                ## Reporting

                | Field | Value |
                | --- | --- |
                | OutputDirectory | {{Path.Combine(directory, "uat")}} |
                """);
            return directory;
        }

        private sealed class Harness(HeadsFixture fixture, string configFilePath)
            : BddScenarioTestBase<HeadsFixture>(fixture)
        {
            protected override string ConfigFilePath => configFilePath;

            protected override BddRuntimeValidationOptions RuntimeValidation { get; } = new(RequireAssemblies: false);

            public Task RunFile(string filePath) => RunFileAsync(filePath);
        }
    }

    /// <summary>A maui head of its own, and a web head that <see cref="CompositionFor"/> brings.</summary>
    [BddIgnore]
    public sealed class HeadsFixture : IBddSessionHost
    {
        private readonly TestComposition _web;

        public HeadsFixture()
        {
            Composition = ComposeHead("Bruinet.Uat.Tests.HeadCases.Maui");
            _web = ComposeHead("Bruinet.Uat.Tests.HeadCases.Web");
        }

        public TestComposition Composition { get; }

        public List<string> Opened { get; } = [];

        public IServiceProvider UseSession(string name) => CompositionOf(name).CreateScope().ServiceProvider;

        public TestComposition? CompositionFor(string name) => name == "Wendy" ? _web : null;

        private TestComposition CompositionOf(string name) => CompositionFor(name) ?? Composition;

        private TestComposition ComposeHead(string namespacePrefix)
            => TestComposition.ForFixture(
                this,
                TestCompositionDiscoveryOptions.FromScans([new TestModuleScanAttribute(typeof(HeadsFixture)) { NamespacePrefix = namespacePrefix }]));
    }
}

namespace Bruinet.Uat.Tests.HeadCases.Maui
{
    [TestPage("Main")]
    [BddIgnore]
    public sealed class MainPage(HeadsFixture fixture)
    {
        public bool WaitReady(int? timeoutMs = null)
        {
            fixture.Opened.Add("maui");
            return true;
        }
    }
}

namespace Bruinet.Uat.Tests.HeadCases.Web
{
    [TestPage("Main")]
    [BddIgnore]
    public sealed class MainPage(HeadsFixture fixture)
    {
        public bool WaitReady(int? timeoutMs = null)
        {
            fixture.Opened.Add("web");
            return true;
        }
    }
}
