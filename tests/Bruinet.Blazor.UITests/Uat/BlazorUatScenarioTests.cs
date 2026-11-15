namespace Bruinet.Blazor.UITests.Uat;

[Trait("Category", "UAT")]
[Trait("Target", "BLAZOR")]
public sealed class BlazorUatScenarioTests(BlazorUatFixture fixture)
    : BddScenarioTestBase<BlazorUatFixture>(fixture),
        IClassFixture<BlazorUatFixture>
{
    // One case per scenario, each with its BddScenario trait: one scenario runs by filter (bruinet affected).
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "BLAZOR", Fixture: "BlazorUatFixture");

    [BddScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName)
    {
        return RunUatScenarioAsync(filePath, scenarioName);
    }
}
