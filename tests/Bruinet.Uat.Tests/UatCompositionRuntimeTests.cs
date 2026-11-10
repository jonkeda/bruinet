using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.CompositionCases;

public sealed class UatCompositionRuntimeTests
{
    [Fact]
    public async Task ScenarioTestBase_UsesCompositionScopePerScenario()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Composition Runtime

                @composition
                ## Scenario: First scoped flow

                ```gherkin
                Given I am on the Main page
                When I tap Save
                And I choose DI flow
                Then scoped action count should be 2
                ```

                @composition
                ## Scenario: Second scoped flow

                ```gherkin
                Given I am on the Main page
                When I tap Save
                And I choose DI flow
                Then scoped action count should be 2
                ```
                """);
            var fixture = new CompositionFixture();
            var harness = new CompositionScenarioHarness(fixture, configFilePath);

            await harness.RunFileAsync(scenarioFilePath);

            Assert.Equal(2, fixture.NavigationCalls);
            Assert.Equal(0, fixture.RootPhraseCalls);
            Assert.Equal(0, fixture.LegacyMainPage.SaveButton.ClickCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task APhraseWithATableParameter_ReceivesTheStepsTable()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Tables

                ## Scenario: Rows reach the phrase

                ```gherkin
                When I store these rows
                | Title |
                | --- |
                | One |
                | Two |
                Then 2 rows should be stored
                ```
                """);
            var harness = new CompositionScenarioHarness(new CompositionFixture(), configFilePath);

            await harness.RunFileAsync(scenarioFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BddThen_OnAPhraseClassMethod_RunsAfterBut()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: But

                ## Scenario: A phrase-class Then follows But

                ```gherkin
                Given the composition state is fresh
                When I store these rows
                | Title |
                | --- |
                | One |
                Then 1 rows should be stored
                But no action should have run
                ```
                """);
            var harness = new CompositionScenarioHarness(new CompositionFixture(), configFilePath);

            await harness.RunFileAsync(scenarioFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BddGiven_OnAPhraseClassMethod_IsAPhraseNotAControlVerb()
    {
        var fixture = new CompositionFixture();
        var catalog = UatReflectionRuntime.FromComposition(fixture, fixture.Composition).CreateCommandCatalog();

        var pattern = Assert.Single(catalog.Patterns, pattern =>
            pattern.Phrase == "the composition state is fresh");
        Assert.Equal(BddEffectiveStepKeyword.Given, pattern.Keyword);
        Assert.Equal(
            $"{typeof(CompositionPhrases).FullName}.{nameof(CompositionPhrases.AssertFresh)}",
            pattern.CommandId);
    }

    [Fact]
    public void UatRuntime_AcceptsTestCompositionPropertyAlias()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var fixture = new AliasFixture();

            var runtime = new UatRuntime(
                fixture,
                configFilePath,
                new UatRuntimeValidationOptions(RequireAssemblies: false));

            Assert.Same(fixture.TestComposition, runtime.Composition);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BddStepDefinitionsAttribute_RegistersPhraseClassAsScopedService()
    {
        var fixture = new CompositionFixture();

        Assert.Contains(fixture.Composition.Catalog.Services, service =>
            service.Type == typeof(CompositionPhrases));

        using var scope = fixture.Composition.CreateScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<CompositionPhrases>(),
            scope.ServiceProvider.GetRequiredService<CompositionPhrases>());
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteConfig(string directory)
    {
        var filePath = Path.Combine(directory, "uat.config.md");
        File.WriteAllText(filePath, $$"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | TEST |

            ## Reporting

            | Field | Value |
            | --- | --- |
            | OutputDirectory | {{Path.Combine(directory, "uat")}} |
            """);
        return filePath;
    }

    private static string WriteScenario(string directory, string markdown)
    {
        var filePath = Path.Combine(directory, "composition.feature.md");
        File.WriteAllText(filePath, markdown);
        return filePath;
    }

    [TestModuleScan(typeof(UatCompositionRuntimeTests), NamespacePrefix = "Bruinet.Uat.Tests.CompositionCases")]
    [BddIgnore]
    public sealed class CompositionFixture
    {
        public CompositionFixture()
        {
            Composition = TestComposition.ForFixture(this, services =>
                services.AddScoped<CompositionState>());
        }

        public TestComposition Composition { get; }

        public LegacyMainPage LegacyMainPage { get; } = new();

        public int NavigationCalls { get; private set; }

        public int RootPhraseCalls { get; private set; }

        public void NavigateToMain()
        {
            NavigationCalls++;
        }

        [BddWhen("I choose DI flow")]
        public void ChooseDiFlowOnFixture()
        {
            RootPhraseCalls++;
        }
    }

    [TestModuleScan(typeof(UatCompositionRuntimeTests), NamespacePrefix = "Bruinet.Uat.Tests.CompositionCases")]
    [BddIgnore]
    public sealed class AliasFixture
    {
        public AliasFixture()
        {
            TestComposition = Bruinet.Core.Composition.TestComposition.ForFixture(this);
        }

        public TestComposition TestComposition { get; }
    }

    [TestPage("Main")]
    [BddIgnore]
    public sealed class CompositionMainPage
    {
        private readonly CompositionState _state;

        public CompositionMainPage(CompositionState state)
        {
            _state = state;
            SaveButton = new CompositionSaveButton(state);
        }

        public CompositionSaveButton SaveButton { get; }

        public bool WaitReady(int? timeoutMs = null)
        {
            _state.ReadyCount++;
            return true;
        }
    }

    [BddStepDefinitions]
    [BddIgnore]
    public sealed class CompositionPhrases : BddStepDefinitionsBase
    {
        private readonly CompositionState _state;

        public CompositionPhrases(CompositionState state)
        {
            _state = state;
        }

        [BddGiven("the composition state is fresh")]
        public void AssertFresh()
        {
            Assert.Equal(0, _state.StoredRows);
        }

        [BddWhen("I choose DI flow")]
        public void ChooseDiFlow()
        {
            _state.ActionCount++;
        }

        [BddWhen("I store these rows")]
        public void StoreRows(BddTable table)
        {
            _state.StoredRows = table.Rows.Count;
        }

        [BddThen("{count} rows should be stored")]
        public void AssertStoredRows(int count)
        {
            Assert.Equal(count, _state.StoredRows);
        }

        [BddThen("no action should have run")]
        public void AssertNoAction()
        {
            Assert.Equal(0, _state.ActionCount);
        }

        [BddThen("scoped action count should be {count}")]
        public void AssertScopedActionCount(int count)
        {
            Assert.Equal(1, _state.ReadyCount);
            Assert.Equal(count, _state.ActionCount);
        }
    }

    public sealed class CompositionState
    {
        public int ReadyCount { get; set; }

        public int ActionCount { get; set; }

        public int StoredRows { get; set; }
    }

    public sealed class CompositionSaveButton
    {
        private readonly CompositionState _state;

        public CompositionSaveButton(CompositionState state)
        {
            _state = state;
        }

        public void Click(int? timeoutMs = null)
        {
            _state.ActionCount++;
        }
    }

    public sealed class LegacyMainPage
    {
        public LegacySaveButton SaveButton { get; } = new();

        public bool WaitReady(int? timeoutMs = null) => true;
    }

    public sealed class LegacySaveButton
    {
        public int ClickCount { get; private set; }

        public void Click(int? timeoutMs = null)
        {
            ClickCount++;
        }
    }

    private sealed class CompositionScenarioHarness(
        CompositionFixture fixture,
        string configFilePath)
        : UatScenarioTestBase<CompositionFixture>(fixture)
    {
        protected override string ConfigFilePath => configFilePath;

        protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
            new(RequireAssemblies: false);

        public Task RunFileAsync(string filePath) => RunUatFileAsync(filePath);
    }
}
