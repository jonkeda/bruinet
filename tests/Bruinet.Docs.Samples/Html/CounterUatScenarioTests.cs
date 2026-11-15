// docs/getting-started/quick-start-uat.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:scenarios
using Bruinet.Uat;
using Xunit;

namespace MyApp.UITests;

// One test case per scenario of every Features/*.feature.md.
[Trait("Category", "UAT")]
public sealed class CounterUatScenarioTests(CounterUatFixture fixture)
    : BddScenarioTestBase<CounterUatFixture>(fixture), IClassFixture<CounterUatFixture>
{
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "HTML", Fixture: "CounterUatFixture");

    [BddScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);
}
#endregion
