namespace Bruinet.Uat;

// The names suites used before AD-040, kept one release so a suite moves at its own pace (.my/bdd-uat, BU8 removes them).

/// <summary>Renamed to <see cref="BddFeatureFormatTestBase"/>.</summary>
[Obsolete("Renamed to BddFeatureFormatTestBase (AD-040). The AssertUatFile* methods are AssertFeature*.")]
public abstract class UatSpecFormatTestBase : BddFeatureFormatTestBase
{
    /// <summary>Renamed to <see cref="BddFeatureFormatTestBase.AssertFeatureFormat"/>.</summary>
    /// <param name="filePath">The feature file.</param>
    protected void AssertUatFileFormat(string filePath) => AssertFeatureFormat(filePath);

    /// <summary>Renamed to <see cref="BddFeatureFormatTestBase.AssertFeatureParsesAndContainsRequiredMetadata"/>.</summary>
    /// <param name="filePath">The feature file.</param>
    protected void AssertUatFileParsesAndContainsRequiredMetadata(string filePath) => AssertFeatureParsesAndContainsRequiredMetadata(filePath);

    /// <summary>Renamed to <see cref="BddFeatureFormatTestBase.AssertFeatureBindsThroughCatalog"/>.</summary>
    /// <param name="filePath">The feature file.</param>
    protected void AssertUatFileBindsThroughCatalog(string filePath) => AssertFeatureBindsThroughCatalog(filePath);

    /// <summary>Renamed to <see cref="BddFeatureFormatTestBase.AssertConfigParses"/>.</summary>
    protected void AssertUatConfigParses() => AssertConfigParses();
}
