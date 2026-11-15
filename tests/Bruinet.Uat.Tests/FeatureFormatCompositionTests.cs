using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Xunit;

namespace Bruinet.Uat.Tests.FormatCompositionCases;

public sealed class FeatureFormatCompositionTests
{
    [Fact]
    public void SpecFormatTestBase_BindsCompositionPhraseClasses()
    {
        var directory = CreateTempDirectory();
        try
        {
            var scenarioFilePath = Path.Combine(directory, "composition.feature.md");
            File.WriteAllText(scenarioFilePath, """
                # Feature: Composition

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
            var harness = new FormatCompositionHarness();

            harness.AssertBinds(scenarioFilePath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bruinet-format-composition-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    [TestModuleScan(typeof(FeatureFormatCompositionTests), NamespacePrefix = "Bruinet.Uat.Tests.FormatCompositionCases")]
    [BddIgnore]
    public sealed class FormatCompositionFixture
    {
    }

    [BddStepDefinitions]
    [BddIgnore]
    public sealed class FormatCompositionPhrases : BddStepDefinitionsBase
    {
        [BddWhen("I run the composition-only phrase")]
        public void RunCompositionOnlyPhrase()
        {
        }
    }

    private sealed class FormatCompositionHarness : BddFeatureFormatTestBase
    {
        protected override Type? RuntimeRootType => typeof(FormatCompositionFixture);

        public void AssertBinds(string filePath) => AssertFeatureBindsThroughCatalog(filePath);
    }
}
