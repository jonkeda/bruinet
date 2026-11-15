namespace Bruinet.Stride.UITests.Uat;

[Trait("Category", "UAT")]
[Trait("Target", "STRIDE")]
public sealed class StrideUatScenarioTests(StrideUatFixture fixture)
    : BddScenarioTestBase<StrideUatFixture>(fixture),
        IClassFixture<StrideUatFixture>
{
    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "STRIDE", Fixture: "StrideUatFixture");

    [Theory(Timeout = 120000)]
    [MemberData(nameof(ScenarioFiles))]
    public Task UatFile_Passes(string filePath)
    {
        return RunUatFileAsync(filePath);
    }
}
