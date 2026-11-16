using Bruinet.Uat;

namespace Bruinet.NativeIos.UITests.Uat;

/// <summary>The platform's UAT suite: every <c>Features/*.feature.md</c>, on the gallery's session.</summary>
[Collection(GalleryCollection.Name)]
[Trait("Category", "UAT")]
[Trait("Target", "NATIVEIOS")]
public sealed class NativeIosUatScenarioTests(GalleryFixture fixture)
    : BddScenarioTestBase<GalleryFixture>(fixture)
{
    public static IEnumerable<object[]> ScenarioFiles => GetScenarioFiles();

    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "NATIVEIOS", Fixture: "GalleryFixture");

    [Theory(Timeout = 180000)]
    [MemberData(nameof(ScenarioFiles))]
    public Task UatFile_Passes(string filePath)
    {
        return RunFileAsync(filePath);
    }
}
