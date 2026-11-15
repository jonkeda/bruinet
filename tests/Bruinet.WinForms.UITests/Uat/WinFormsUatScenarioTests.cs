namespace Bruinet.WinForms.UITests.Uat;

[Trait("Category", "UAT")]
[Trait("Target", "WINFORMS")]
public sealed class WinFormsUatScenarioTests(WinFormsUatFixture fixture)
    : BddScenarioTestBase<WinFormsUatFixture>(fixture),
        IClassFixture<WinFormsUatFixture>
{
    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "WINFORMS", Fixture: "WinFormsUatFixture");

    [Theory(Timeout = 120000)]
    [MemberData(nameof(ScenarioFiles))]
    public async Task UatFile_Passes(string filePath)
    {
        await RunUatFileAsync(filePath);
    }
}
