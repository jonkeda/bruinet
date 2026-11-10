using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Xunit;

namespace Bruinet.Uat.Tests.SpecCompositionCases;

public sealed class UatSpecCompositionTests
{
    [Fact]
    public void SpecFormatTestBase_BindsCompositionPhraseClasses()
    {
        var directory = CreateTempDirectory();
        try
        {
            var scenarioFilePath = Path.Combine(directory, "composition-spec.feature.md");
            File.WriteAllText(scenarioFilePath, """
                # Feature: Composition Spec

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
                ## Scenario: Phrase class binding

                ```gherkin
                When I run the composition-only phrase
                ```
                """);
            var harness = new SpecCompositionHarness();

            harness.AssertBinds(scenarioFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bruinet-uat-spec-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [TestModuleScan(typeof(UatSpecCompositionTests), NamespacePrefix = "Bruinet.Uat.Tests.SpecCompositionCases")]
    [BddIgnore]
    public sealed class SpecCompositionFixture
    {
    }

    [BddStepDefinitions]
    [BddIgnore]
    public sealed class SpecCompositionPhrases : BddStepDefinitionsBase
    {
        [BddWhen("I run the composition-only phrase")]
        public void RunCompositionOnlyPhrase()
        {
        }
    }

    private sealed class SpecCompositionHarness : UatSpecFormatTestBase
    {
        protected override Type? RuntimeRootType => typeof(SpecCompositionFixture);

        public void AssertBinds(string filePath) => AssertUatFileBindsThroughCatalog(filePath);
    }
}
