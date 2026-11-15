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

/// <summary>Renamed to <see cref="BddScenarioTestBase{TFixture}"/>.</summary>
/// <typeparam name="TFixture">The suite's fixture.</typeparam>
[Obsolete("Renamed to BddScenarioTestBase (AD-040).")]
public abstract class UatScenarioTestBase<TFixture> : BddScenarioTestBase<TFixture>
    where TFixture : class
{
    /// <param name="fixture">The suite's fixture.</param>
    protected UatScenarioTestBase(TFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>Renamed to <see cref="BddScenarioTestBase{TFixture}.RunScenarioAsync(string, string, CancellationToken)"/>.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario's name.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    protected Task RunUatScenarioAsync(string filePath, string scenarioName, CancellationToken cancellationToken = default)
        => RunScenarioAsync(filePath, scenarioName, cancellationToken);

    /// <summary>Renamed to <see cref="BddScenarioTestBase{TFixture}.RunFileAsync"/>.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    protected Task RunUatFileAsync(string filePath, CancellationToken cancellationToken = default)
        => RunFileAsync(filePath, cancellationToken);

    /// <summary>Renamed to <see cref="BddScenarioTestBase{TFixture}.RunExpectedFailureFileAsync"/>.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="expectedDiagnostics">The diagnostics the file must give.</param>
    protected Task RunExpectedFailureUatFileAsync(string filePath, params string[] expectedDiagnostics)
        => RunExpectedFailureFileAsync(filePath, expectedDiagnostics);
}

/// <summary>Renamed to <see cref="BddRuntimeValidationOptions"/>.</summary>
/// <param name="Target">The target the config must name; null for any.</param>
/// <param name="Fixture">The fixture the config must name; null for any.</param>
/// <param name="RequireAssemblies">Whether the config must register an assembly.</param>
[Obsolete("Renamed to BddRuntimeValidationOptions (AD-040).")]
public sealed record UatRuntimeValidationOptions(
    string? Target = null,
    string? Fixture = null,
    bool RequireAssemblies = true) : BddRuntimeValidationOptions(Target, Fixture, RequireAssemblies);
