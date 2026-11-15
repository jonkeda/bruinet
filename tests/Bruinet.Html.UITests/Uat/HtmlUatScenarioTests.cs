namespace Bruinet.Html.UITests.Uat;

using Bruinet.Html.UITests.Uat.Pages;

[Trait("Category", "UAT")]
[Trait("Target", "HTML")]
public sealed class HtmlUatScenarioTests(HtmlUatFixture fixture)
    : BddScenarioTestBase<HtmlUatFixture>(fixture),
        IClassFixture<HtmlUatFixture>
{
    // One case per scenario, each with its BddScenario trait: one scenario runs by filter (bruinet affected).
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "HTML", Fixture: "HtmlUatFixture");

    [BddScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName)
    {
        return RunScenarioAsync(filePath, scenarioName);
    }
}
