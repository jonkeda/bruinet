using Bruinet.Uat;

namespace Bruinet.Maui.UITests.Uat;

// The UI tests' collection and its one app: a collection of its own would launch a second app,
// and take one of the two collections the parallelism tests need running side by side.
[Collection("Maui")]
[Trait("Category", "UAT")]
[Trait("Target", "MAUI")]
public sealed class MauiUatScenarioTests
    : BddScenarioTestBase<MauiFixture>
{
    public MauiUatScenarioTests(MauiFixture fixture)
        : base(fixture)
    {
    }

    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    public static IEnumerable<object[]> ExpectedFailureScenarioFiles => GetScenarioFiles("ExpectedFailures");

    [Theory(Timeout = 120000)]
    [MemberData(nameof(ScenarioFiles))]
    public Task UatFile_Passes(string filePath) => RunFileAsync(filePath);

    // A control the page does not have is a bind error: the file fails before the app is driven.
    [Theory]
    [MemberData(nameof(ExpectedFailureScenarioFiles))]
    public void ExpectedFailureUatFile_ReturnsUsefulDiagnostics(string filePath) =>
        RunExpectedBindFailureUatFile(
            filePath,
            "BDDB009",
            "Imaginary Button",
            "Its controls are",
            Path.GetFileName(filePath));

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "MauiFixture");
}
