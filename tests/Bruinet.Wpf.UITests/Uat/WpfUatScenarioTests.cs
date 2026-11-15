namespace Bruinet.Wpf.UITests.Uat;

[Trait("Category", "UAT")]
[Trait("Target", "WPF")]
public sealed class WpfUatScenarioTests(WpfUatFixture fixture)
    : BddScenarioTestBase<WpfUatFixture>(fixture),
        IClassFixture<WpfUatFixture>
{
    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "WPF", Fixture: "WpfUatFixture");

    [Theory(Timeout = 120000)]
    [MemberData(nameof(ScenarioFiles))]
    public async Task UatFile_Passes(string filePath)
    {
        await RunUatFileAsync(filePath);
    }
}
