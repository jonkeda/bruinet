namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddScenarioExecutionResult(
    BddScenarioRunResult Result,
    BddExecutionContext Context,
    string? EvidencePath);

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddScenarioExecutor
{
    public static async Task<BddScenarioExecutionResult> RunAsync(
        BddBoundScenario scenario,
        BddConfig? config = null,
        Action<BddBoundScenario>? beforeScenario = null,
        Action<BddExecutionContext>? configureContext = null,
        Func<BddBoundScenario, string?>? captureEvidenceOnFailure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        beforeScenario?.Invoke(scenario);

        var runner = new BddScenarioRunner();
        configureContext?.Invoke(runner.Context);
        var result = config is null
            ? await runner.RunAsync(scenario, cancellationToken).ConfigureAwait(false)
            : await runner.RunAsync(scenario, config, cancellationToken: cancellationToken).ConfigureAwait(false);

        string? evidencePath = null;
        if (!result.Passed &&
            !result.Skipped &&
            config?.Reporting.ScreenshotOnFailure == true &&
            captureEvidenceOnFailure is not null)
        {
            evidencePath = captureEvidenceOnFailure(scenario);
        }

        return new BddScenarioExecutionResult(result, runner.Context, evidencePath);
    }
}
