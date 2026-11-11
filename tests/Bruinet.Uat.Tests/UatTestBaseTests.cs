using Bruinet.Bdd;
using Bruinet.Core.Configuration;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Settings;
using Xunit;

namespace Bruinet.Uat.Tests;

public sealed class UatTestBaseTests
{
    [Fact]
    public async Task ScenarioTestBase_RunUatFileAsync_ExecutesScenarioAndProjectHooks()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Metadata

                | Field | Value |
                | --- | --- |
                | App | Harness |
                | Area | Runtime |
                | Target | TEST |
                | Tags | smoke |
                | Mode | Automated |
                | Requires | None |
                | Priority | Smoke |
                | Evidence | none |

                @smoke
                ## Scenario: Passing flow

                ```gherkin
                When I run the custom action
                Then the custom action should have run
                ```
                """);
            var fixture = new HarnessFixture();
            var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture));

            await harness.RunFileAsync(scenarioFilePath);

            Assert.Equal(1, fixture.ActionCount);
            Assert.Equal("Passing flow", harness.BeforeScenarioName);
            Assert.Equal("Passing flow", harness.AfterScenarioName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_AfterScenarioThatFails_DoesNotHideTheScenariosOwnFailure()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Scenario: Failing flow

                ```gherkin
                Then the custom action should have run
                ```
                """);
            var fixture = new HarnessFixture();
            var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture)) { ThrowAfterScenario = true };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunScenarioAsync(scenarioFilePath, "Failing flow"));
            Assert.Contains("Action did not run.", error.Message, StringComparison.Ordinal);
            Assert.Equal("Failing flow", harness.AfterScenarioName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_AfterScenarioThatFails_FailsAPassingScenario()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Scenario: Passing flow

                ```gherkin
                When I run the custom action
                ```
                """);
            var fixture = new HarnessFixture();
            var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture)) { ThrowAfterScenario = true };

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunScenarioAsync(scenarioFilePath, "Passing flow"));
            Assert.Equal("AfterScenario failed", error.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_RunExpectedFailureUatFileAsync_AssertsFormattedFailureDetails()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory, screenshotOnFailure: true);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                @smoke
                ## Scenario: Expected failure flow

                ```gherkin
                When I fail the custom action
                ```
                """);
            var fixture = new HarnessFixture();
            var catalog = new BddCommandCatalog();
            catalog.Register(
                BddEffectiveStepKeyword.When,
                "I fail the custom action",
                "Harness.Fail",
                handler: (_, invocation, _) => Task.FromResult(
                    BddStepResult.Failed(invocation, "Intentional failure from harness")));
            var harness = new ScenarioHarness(fixture, configFilePath, catalog);

            await harness.RunExpectedFailureFileAsync(
                scenarioFilePath,
                "Harness.Fail",
                "Intentional failure from harness");

            Assert.Equal("Expected failure flow", harness.BeforeScenarioName);
            Assert.Equal("Expected failure flow", harness.AfterScenarioName);
            Assert.Equal("ScenarioHarness", fixture.ScreenshotService.TestClass);
            Assert.Equal("Expected failure flow", fixture.ScreenshotService.TestMethod);
            Assert.Equal("failure", fixture.ScreenshotService.Description);
            Assert.Equal("evidence.png", fixture.ScreenshotService.LastCapturePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_AScenarioThatDoesNotBind_LeavesAFailedResultOfKindBind()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Scenario: A phrase nobody wrote

                ```gherkin
                When I do what no step says
                ```
                """);
            var harness = new ScenarioHarness(new HarnessFixture(), configFilePath, new BddCommandCatalog());

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunScenarioAsync(scenarioFilePath, "A phrase nobody wrote"));

            var result = Directory.EnumerateFiles(Path.Combine(directory, "uat"), "*__result.json").Single();
            var report = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result))!;
            Assert.Equal("A phrase nobody wrote", (string?)report["scenario"]);
            Assert.Equal("failed", (string?)report["status"]);
            Assert.Equal("bind", (string?)report["steps"]![0]!["failure"]!["kind"]);
            Assert.Contains("UATB", (string?)report["steps"]![0]!["message"], StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_TheReport_NamesItsFileAndLines_WithoutTheTrace()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Scenario: Reported flow

                ```gherkin
                When I run the custom action
                And I fail the custom action
                ```
                """);
            var fixture = new HarnessFixture();
            var catalog = CreatePassingCatalog(fixture);
            catalog.Register(
                BddEffectiveStepKeyword.When,
                "I fail the custom action",
                "Harness.Fail",
                handler: (_, invocation, _) => Task.FromResult(BddStepResult.Failed(invocation, "Intentional failure from harness")));
            var harness = new ScenarioHarness(fixture, configFilePath, catalog);

            await harness.RunExpectedFailureFileAsync(scenarioFilePath, "Intentional failure from harness");

            using var report = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "uat", "scenario__Reported flow__result.json")));
            var root = report.RootElement;
            Assert.Equal("scenario.feature.md", root.GetProperty("file").GetString());
            Assert.Equal(3, root.GetProperty("line").GetInt32());
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("durationMs").GetInt64() >= 0);
            var steps = root.GetProperty("steps").EnumerateArray().ToList();
            Assert.Equal([6, 7], steps.Select(step => step.GetProperty("line").GetInt32()));
            Assert.Equal(["Passed", "Failed"], steps.Select(step => step.GetProperty("status").GetString()));
            Assert.Equal("Intentional failure from harness", steps[1].GetProperty("message").GetString());
            Assert.False(root.TryGetProperty("commandCatalog", out _), "The catalog is trace detail.");
            Assert.False(steps[0].TryGetProperty("commandId", out _), "A step's binding is trace detail.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SpecFormatTestBase_ValidatesMetadataBindingAndConfig()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Base Harness

                ## Metadata

                | Field | Value |
                | --- | --- |
                | App | Harness |
                | Area | Runtime |
                | Target | TEST |
                | Tags | smoke |
                | Mode | Automated |
                | Requires | None |
                | Priority | Smoke |
                | Evidence | none |

                @smoke
                ## Scenario: Spec format flow

                ```gherkin
                When I run the custom action
                ```
                """);
            var harness = new SpecFormatHarness(configFilePath);

            harness.AssertMetadata(scenarioFilePath);
            harness.AssertBinds(scenarioFilePath);
            harness.AssertConfigParses();

            Assert.True(harness.ConfigAsserted);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_RunUatFileAsync_LoadsScenarioSettings()
    {
        var directory = CreateTempDirectory();
        try
        {
            var settingsDirectory = Path.Combine(directory, "TestSettings");
            Directory.CreateDirectory(Path.Combine(settingsDirectory, "scenarios"));
            File.WriteAllText(Path.Combine(settingsDirectory, "testsettings.json"), """
                {
                  "settings": {
                    "harness": {
                      "mode": "default"
                    }
                  }
                }
                """);
            File.WriteAllText(Path.Combine(settingsDirectory, "scenarios", "uat-900-1.json"), """
                {
                  "settings": {
                    "harness": {
                      "mode": "scenario"
                    }
                  }
                }
                """);
            var configFilePath = WriteConfig(directory);
            var scenarioFilePath = WriteScenario(directory, """
                # Feature: Settings Harness

                @uat-900-1
                ## Scenario: Scenario settings flow

                ```gherkin
                Then the scenario setting should be available
                ```
                """);
            var fixture = new HarnessFixture();
            var catalog = new BddCommandCatalog();
            catalog.Register(
                BddEffectiveStepKeyword.Then,
                "the scenario setting should be available",
                "Harness.AssertSettings",
                handler: (context, invocation, _) =>
                {
                    return Task.FromResult(
                        context.GetSettings().GetRequired<string>("harness.mode") == "scenario"
                            ? BddStepResult.Passed(invocation)
                            : BddStepResult.Failed(invocation, "Scenario settings were not loaded."));
                });
            var harness = new ScenarioHarness(fixture, configFilePath, catalog);

            await harness.RunFileAsync(scenarioFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_RunUatScenarioAsync_RunsOnlyTheNamedScenario_AndEntersEachFileOnce()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var first = WriteScenario(directory, """
                # Feature: First file

                ## Scenario: One

                ```gherkin
                When I run the custom action
                ```

                ## Scenario: Two

                ```gherkin
                When I run the custom action
                ```
                """);
            var second = Path.Combine(directory, "second.feature.md");
            File.WriteAllText(second, """
                # Feature: Second file

                ## Scenario: Three

                ```gherkin
                When I run the custom action
                ```
                """);
            var fixture = new HarnessFixture();
            var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture));

            await harness.RunScenarioAsync(first, "Two");
            await harness.RunScenarioAsync(first, "One");
            await harness.RunScenarioAsync(second, "Three");

            Assert.Equal(3, fixture.ActionCount);
            Assert.Equal(["First file", "Second file"], harness.FilesEntered);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// As xUnit runs them: a test-class instance per case, two collections each with a fixture of
    /// its own, alternating files. Each fixture enters its file once.
    /// </summary>
    [Fact]
    public async Task ScenarioTestBase_TwoFixturesAlternatingFiles_EachEnterTheirFileOnce()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var first = WriteScenario(directory, """
                # Feature: First file

                ## Scenario: One

                ```gherkin
                When I run the custom action
                ```

                ## Scenario: Two

                ```gherkin
                When I run the custom action
                ```
                """);
            var second = Path.Combine(directory, "second.feature.md");
            File.WriteAllText(second, """
                # Feature: Second file

                ## Scenario: Three

                ```gherkin
                When I run the custom action
                ```

                ## Scenario: Four

                ```gherkin
                When I run the custom action
                ```
                """);
            var fixtureA = new HarnessFixture();
            var fixtureB = new HarnessFixture();
            var entered = new List<string>();

            async Task Run(HarnessFixture fixture, string file, string scenario)
            {
                var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture));
                await harness.RunScenarioAsync(file, scenario);
                entered.AddRange(harness.FilesEntered.Select(title => $"{(ReferenceEquals(fixture, fixtureA) ? "A" : "B")}: {title}"));
            }

            await Run(fixtureA, first, "One");
            await Run(fixtureB, second, "Three");
            await Run(fixtureA, first, "Two");
            await Run(fixtureB, second, "Four");

            Assert.Equal(["A: First file", "B: Second file"], entered);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ScenarioTestBase_RunUatScenarioAsync_NamesTheScenariosWhenTheNameIsUnknown()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configFilePath = WriteConfig(directory);
            var file = WriteScenario(directory, """
                # Feature: Named

                ## Scenario: Present

                ```gherkin
                When I run the custom action
                ```
                """);
            var fixture = new HarnessFixture();
            var harness = new ScenarioHarness(fixture, configFilePath, CreatePassingCatalog(fixture));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RunScenarioAsync(file, "Absent"));

            Assert.Contains("'Present'", exception.Message);
            Assert.Equal(0, fixture.ActionCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ScenarioSource_GivesOneCasePerScenario_GroupedByTestData()
    {
        var directory = CreateTempDirectory();
        try
        {
            var scenarios = Directory.CreateDirectory(Path.Combine(directory, "Features")).FullName;
            File.WriteAllText(Path.Combine(scenarios, "a.feature.md"), ScenarioFile("A", "two", "A1", "A2"));
            File.WriteAllText(Path.Combine(scenarios, "b.feature.md"), ScenarioFile("B", "one", "B1"));
            File.WriteAllText(Path.Combine(scenarios, "c.feature.md"), ScenarioFile("C", null, "C1"));
            File.WriteAllText(Path.Combine(scenarios, "d.feature.md"), """
                # Feature: D

                ## Scenario Outline: Row

                ```gherkin
                When I run the custom action <n>
                ```

                ### Examples

                | n |
                | --- |
                | 1 |
                | 2 |
                """);
            File.WriteAllText(Path.Combine(scenarios, "e.feature.md"), "not a uat file");

            var cases = BddScenarioSource.GetScenarioTheoryData(baseDirectory: directory)
                .Select(data => $"{Path.GetFileName((string)data[0])}:{data[1]}")
                .ToList();

            Assert.Equal(["c.feature.md:C1", "d.feature.md:Row [1]", "d.feature.md:Row [2]", "e.feature.md:", "b.feature.md:B1", "a.feature.md:A1", "a.feature.md:A2"], cases);
            Assert.True(
                string.CompareOrdinal(
                    BddScenarioSource.GetOrderKey(Path.Combine(scenarios, "b.feature.md"), "B1"),
                    BddScenarioSource.GetOrderKey(Path.Combine(scenarios, "a.feature.md"), "A1")) < 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string ScenarioFile(string title, string? testData, params string[] scenarios)
    {
        var metadata = testData is null ? string.Empty : $"## Metadata\n\n| Field | Value |\n| --- | --- |\n| TestData | {testData} |\n\n";
        return $"# Feature: {title}\n\n{metadata}" + string.Concat(scenarios.Select(name => $"## Scenario: {name}\n\n```gherkin\nWhen I run the custom action\n```\n\n"));
    }

    private static BddCommandCatalog CreatePassingCatalog(HarnessFixture fixture)
    {
        var catalog = new BddCommandCatalog();
        catalog.Register(
            BddEffectiveStepKeyword.When,
            "I run the custom action",
            "Harness.Run",
            handler: (_, invocation, _) =>
            {
                fixture.ActionCount++;
                return Task.FromResult(BddStepResult.Passed(invocation));
            });
        catalog.Register(
            BddEffectiveStepKeyword.Then,
            "the custom action should have run",
            "Harness.AssertRun",
            handler: (_, invocation, _) =>
            {
                return Task.FromResult(
                    fixture.ActionCount > 0
                        ? BddStepResult.Passed(invocation)
                        : BddStepResult.Failed(invocation, "Action did not run."));
            });

        return catalog;
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-base-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteConfig(string directory, bool screenshotOnFailure = false)
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
            | ScreenshotOnFailure | {{screenshotOnFailure}} |
            """);
        return filePath;
    }

    private static string WriteScenario(string directory, string markdown)
    {
        var filePath = Path.Combine(directory, "scenario.feature.md");
        File.WriteAllText(filePath, markdown);
        return filePath;
    }

    private sealed class HarnessFixture
    {
        public int ActionCount { get; set; }

        public FakeScreenshotService ScreenshotService { get; } = new();

        [BddWhen("I run the custom action")]
        public void RunCustomAction()
        {
        }
    }

    private sealed class ScenarioHarness(
        HarnessFixture fixture,
        string configFilePath,
        BddCommandCatalog catalog)
        : UatScenarioTestBase<HarnessFixture>(fixture)
    {
        public string? BeforeScenarioName { get; private set; }

        public string? AfterScenarioName { get; private set; }

        protected override string ConfigFilePath => configFilePath;

        protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
            new(RequireAssemblies: false);

        public Task RunFileAsync(string filePath) => RunUatFileAsync(filePath);

        public Task RunScenarioAsync(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

        public List<string> FilesEntered { get; } = [];

        protected override void BeforeFile(BddDocument document) => FilesEntered.Add(document.Title);

        public Task RunExpectedFailureFileAsync(string filePath, params string[] expectedDiagnostics) =>
            RunExpectedFailureUatFileAsync(filePath, expectedDiagnostics);

        protected override BddCommandCatalog CreateCommandCatalog(UatRuntime runtime) => catalog;

        protected override void BeforeScenario(BddBoundScenario scenario)
        {
            BeforeScenarioName = scenario.Source.Name;
        }

        public bool ThrowAfterScenario { get; init; }

        protected override void AfterScenario(BddBoundScenario scenario)
        {
            AfterScenarioName = scenario.Source.Name;
            if (ThrowAfterScenario)
            {
                throw new InvalidOperationException("AfterScenario failed");
            }
        }
    }

    private sealed class SpecFormatHarness(string configFilePath)
        : UatSpecFormatTestBase
    {
        public bool ConfigAsserted { get; private set; }

        protected override string ConfigFilePath => configFilePath;

        protected override string? ExpectedApp => "Harness";

        protected override string? ExpectedTarget => "TEST";

        protected override Type? RuntimeRootType => typeof(HarnessFixture);

        public void AssertMetadata(string filePath) =>
            AssertUatFileParsesAndContainsRequiredMetadata(filePath);

        public void AssertBinds(string filePath) => AssertUatFileBindsThroughCatalog(filePath);

        public void AssertConfigParses() => AssertUatConfigParses();

        protected override void AssertConfig(BddConfig config)
        {
            Assert.Equal("TEST", config.Runtime["Target"]);
            ConfigAsserted = true;
        }
    }

    private sealed class FakeScreenshotService : IScreenshotService
    {
        public ScreenshotSettings Settings { get; } = ScreenshotSettings.Default;

        public string LastCapturePath { get; private set; } = string.Empty;

        public string? TestClass { get; private set; }

        public string? TestMethod { get; private set; }

        public string? Description { get; private set; }

        public string Capture(string? description = null)
        {
            Description = description;
            LastCapturePath = "evidence.png";
            return LastCapturePath;
        }

        public string Capture(string testClass, string testMethod, string description)
        {
            TestClass = testClass;
            TestMethod = testMethod;
            Description = description;
            LastCapturePath = "evidence.png";
            return LastCapturePath;
        }

        public string CaptureOnFailure(string testClass, string testMethod, Exception exception)
        {
            return Capture(testClass, testMethod, "failure");
        }
    }
}
