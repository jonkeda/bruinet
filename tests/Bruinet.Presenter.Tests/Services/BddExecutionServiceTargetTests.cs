using Bruinet.Bdd;
using Bruinet.Presenter.Services;
using Bruinet.Uat;

namespace Bruinet.Presenter.Tests.Services;

public sealed class BddExecutionServiceTargetTests
{
    public static TheoryData<string, string> TargetEnvironmentVariables => new()
    {
        { "MAUI", "APPIUM_APP_PATH" },
        { "WPF", "WPF_APP_PATH" },
        { "WINFORMS", "WINFORMS_APP_PATH" },
        { "BLAZOR", "BLAZOR_APP_PATH" },
        { "HTML", "HTML_APP_PATH" },
        { "STRIDE", "STRIDE_APP_PATH" }
    };

    [Theory]
    [MemberData(nameof(TargetEnvironmentVariables))]
    public async Task CreateSessionAsync_SupportedTargetSetsExpectedAppPathEnvironment(
        string target,
        string environmentVariable)
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterTargetUat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var appPath = Path.Combine(root, $"Fake{target}App.exe");
            File.WriteAllText(appPath, string.Empty);

            var scenarioPath = Path.Combine(root, "target-smoke.feature.md");
            File.WriteAllText(
                scenarioPath,
                $$"""
                # Feature: {{target}} Presenter Execution

                ## Scenario: Presenter creates a {{target}} session

                ```gherkin
                Given app path is available from {{environmentVariable}}
                Then app path should still be available from {{environmentVariable}}
                ```
                """);

            File.WriteAllText(
                Path.Combine(root, "uat.config.md"),
                $$"""
                # UAT Config

                ## Runtime

                | Field | Value |
                | --- | --- |
                | Target | {{target}} |
                | Fixture | PresenterTargetExecutionFixture |
                | AppPath | {{appPath}} |

                ## Assemblies

                | Kind | Assembly |
                | --- | --- |
                | Pages | {{Path.GetFileName(typeof(PresenterTargetExecutionFixture).Assembly.Location)}} |
                """);

            using var session = await new BddExecutionService().CreateSessionAsync(
                root,
                scenarioPath,
                $"Presenter creates a {target} session",
                CancellationToken.None);

            while (session.HasNext)
            {
                var result = await session.RunNextAsync(CancellationToken.None);
                Assert.Equal(BddStepResultStatus.Passed, result.Status);
            }

            Assert.True(session.Passed);
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

public sealed class PresenterTargetExecutionFixture
{
    private readonly Dictionary<string, string?> _capturedEnvironment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["APPIUM_APP_PATH"] = Environment.GetEnvironmentVariable("APPIUM_APP_PATH"),
        ["WPF_APP_PATH"] = Environment.GetEnvironmentVariable("WPF_APP_PATH"),
        ["WINFORMS_APP_PATH"] = Environment.GetEnvironmentVariable("WINFORMS_APP_PATH"),
        ["BLAZOR_APP_PATH"] = Environment.GetEnvironmentVariable("BLAZOR_APP_PATH"),
        ["HTML_APP_PATH"] = Environment.GetEnvironmentVariable("HTML_APP_PATH"),
        ["STRIDE_APP_PATH"] = Environment.GetEnvironmentVariable("STRIDE_APP_PATH")
    };

    [BddGiven("app path is available from {environmentVariable}")]
    public bool AppPathIsAvailable(string environmentVariable)
    {
        return _capturedEnvironment.TryGetValue(environmentVariable, out var appPath) &&
               !string.IsNullOrWhiteSpace(appPath) &&
               File.Exists(appPath);
    }

    [BddThen("app path should still be available from {environmentVariable}")]
    public bool AppPathShouldStillBeAvailable(string environmentVariable)
    {
        var current = Environment.GetEnvironmentVariable(environmentVariable);
        return _capturedEnvironment.TryGetValue(environmentVariable, out var captured) &&
               string.Equals(captured, current, StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(current) &&
               File.Exists(current);
    }
}
