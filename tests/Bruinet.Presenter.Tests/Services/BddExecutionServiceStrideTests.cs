using Bruinet.Bdd;
using Bruinet.Presenter.Services;
using Bruinet.Uat;

namespace Bruinet.Presenter.Tests.Services;

public sealed class BddExecutionServiceStrideTests
{
    [Fact]
    public async Task CreateSessionAsync_StrideTargetRunsScenarioWithStrideAppPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStrideUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appPath = Path.Combine(root, "FakeStrideApp.exe");
            File.WriteAllText(appPath, string.Empty);

            var scenarioPath = Path.Combine(root, "stride-smoke.feature.md");
            File.WriteAllText(
                scenarioPath,
                """
                # Feature: STRIDE Presenter Execution

                ## Scenario: Presenter creates a STRIDE session

                ```gherkin
                Given STRIDE app path is available
                Then STRIDE app path should still be available
                ```
                """);

            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | STRIDE |
                | Fixture | StridePresenterExecutionFixture |
                | AppPath | {{appPath}} |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | {{Path.GetFileName(typeof(StridePresenterExecutionFixture).Assembly.Location)}} |
                """);

            using var session = await new BddExecutionService().CreateSessionAsync(
                root,
                scenarioPath,
                "Presenter creates a STRIDE session",
                CancellationToken.None);

            while (session.HasNext)
            {
                var result = await session.RunNextAsync(CancellationToken.None);
                Assert.Equal(BddStepResultStatus.Passed, result.Status);
            }

            Assert.True(session.Passed);
            Assert.Contains("Result: not supported", session.AutPlacementReport, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

public sealed class StridePresenterExecutionFixture
{
    private readonly string? _appPathAtCreation = Environment.GetEnvironmentVariable("STRIDE_APP_PATH");

    [BddGiven("STRIDE app path is available")]
    public bool StrideAppPathIsAvailable()
    {
        return !string.IsNullOrWhiteSpace(_appPathAtCreation) &&
               File.Exists(_appPathAtCreation);
    }

    [BddThen("STRIDE app path should still be available")]
    public bool StrideAppPathShouldStillBeAvailable()
    {
        var current = Environment.GetEnvironmentVariable("STRIDE_APP_PATH");
        return string.Equals(_appPathAtCreation, current, StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(current) &&
               File.Exists(current);
    }
}
