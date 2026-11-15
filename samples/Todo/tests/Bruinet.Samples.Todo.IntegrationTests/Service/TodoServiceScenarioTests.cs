using Bruinet.Uat;

namespace Bruinet.Samples.Todo.IntegrationTests.Service;

/// <summary>
/// Runs the Todo scenarios tagged <c>@service</c> against the app without its UI: one test case per
/// scenario, each on a new <see cref="TodoHost"/>.
/// </summary>
/// <remarks>
/// The scenario files are the MAUI suite's, linked; the <c>service</c> head in <c>bdd.config.md</c>
/// lists only the <c>@service</c> scenarios and binds only them. A step that needs a screen is a bind
/// error (<c>BDDB013</c>), shown by the expected-failure file.
/// </remarks>
[Trait("Module", "Todo")]
[Trait("Pyramid", "Integration")]
[Trait("Category", "BDD")]
public sealed class TodoServiceScenarioTests : BddScenarioTestBase<TodoServiceFixture>, IClassFixture<TodoServiceFixture>
{
    /// <summary>Creates the runner over the shared fixture.</summary>
    /// <param name="fixture">The headless app.</param>
    public TodoServiceScenarioTests(TodoServiceFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>One case per <c>@service</c> scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    /// <summary>Runs one scenario and asserts it passes.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario in it.</param>
    [BddScenarioTheory]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <summary>The files that must fail binding: <c>ExpectedFailures/*.feature.md</c>.</summary>
    public static IEnumerable<object[]> ExpectedFailures => GetScenarioFiles("ExpectedFailures");

    /// <summary>A screen step in a <c>@service</c> scenario fails binding before anything runs.</summary>
    /// <param name="filePath">The file.</param>
    [Theory]
    [MemberData(nameof(ExpectedFailures))]
    public void ExpectedFailure_FailsBinding(string filePath)
        => RunExpectedBindFailureUatFile(filePath, "BDDB013", "the service head has no screen");

    /// <inheritdoc />
    protected override void BeforeFile(BddDocument document)
        => Fixture.UseTestSet(document.Metadata.TryGetValue(BddMetadataFields.TestData, out var name) && name.Length > 0
            ? name
            : TodoServiceFixture.DefaultTestData);

    /// <inheritdoc />
    protected override void BeforeScenario(BddBoundScenario scenario) => Fixture.StartTest();

    /// <inheritdoc />
    protected override BddRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "SERVICE", Fixture: nameof(TodoServiceFixture));
}
