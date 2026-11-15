using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Mixed.Uat.Runtime;

/// <summary>
/// Runs every <c>@mixed</c> scenario, one test case each. Each scenario starts with the backend
/// reset and every started app online and on its list; apps start the first time a step names them.
/// </summary>
[Collection(TodoMixedUatCollection.Name)]
[Trait("Category", "UAT")]
[Trait("Target", "MIXED")]
public sealed class TodoMixedUatScenarioTests(TodoMixedUatFixture fixture)
    : BddScenarioTestBase<TodoMixedUatFixture>(fixture)
{
    /// <summary>One case per scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    /// <summary>Runs one scenario and asserts it passes, or that the engine skipped it.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario in it.</param>
    [BddScenarioTheory(Timeout = 240000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <inheritdoc />
    protected override void BeforeScenario(BddBoundScenario scenario) => Fixture.StartTestAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    /// <remarks>Every started app, one folder each: the cause is often on another app's screen.</remarks>
    protected override string? CaptureEvidenceOnFailure(BddBoundScenario scenario)
    {
        var captures = Fixture.Apps.CaptureAll(EvidenceTestClassName, FormatEvidenceScenarioName(scenario), EvidenceDescription);
        return captures.Count == 0
            ? null
            : string.Join("; ", captures.Select(capture => $"{capture.App}: {capture.Path ?? capture.Error}"));
    }

    /// <inheritdoc />
    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MIXED", Fixture: "TodoMixedUatFixture");
}
