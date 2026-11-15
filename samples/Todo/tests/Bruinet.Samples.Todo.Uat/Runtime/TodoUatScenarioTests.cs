using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Uat.Runtime;

/// <summary>
/// Runs every scenario of every <c>Features/*.feature.md</c> against the hermetic Todo app, one test
/// case per scenario (and per outline row).
/// </summary>
/// <remarks>
/// <para>
/// Before a file's first scenario, the fixture (<see cref="IUatScenarioLifecycle"/>) loads the
/// file's <c>TestData</c> (or <c>DefaultTestData</c>); <see cref="BddScenarioOrderer"/> keeps files
/// on the same test set together, so the app restarts once per test set.
/// </para>
/// <para>
/// Each scenario then gets a clean start from the fixture's <c>StartTest</c>: online again, backend
/// reset to the test set's server half, the list on screen. The device's database is not reset, so scenarios add their own todos and check only
/// those.
/// </para>
/// </remarks>
[Collection(TodoUatCollection.CollectionName)]
[TestCaseOrderer(BddScenarioOrderer.TypeName, BddScenarioOrderer.AssemblyName)]
[Trait("Category", "UAT")]
[Trait("Target", "MAUI")]
public sealed class TodoUatScenarioTests : UatScenarioTestBase<TodoUatFixture>
{
    /// <summary>Creates the scenario runner over the shared fixture.</summary>
    /// <param name="fixture">The hermetic Todo app.</param>
    public TodoUatScenarioTests(TodoUatFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>One case per scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    /// <summary>Runs one scenario and asserts it passes.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario in it.</param>
    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <summary>The files that must fail binding: <c>ExpectedFailures/*.feature.md</c>.</summary>
    public static IEnumerable<object[]> ExpectedFailures => GetScenarioFiles("ExpectedFailures");

    /// <summary>
    /// The users pages and phrases are in the <c>Users</c> namespace: a file without
    /// <c>Using | Users</c> does not see them, and fails binding before the app is driven, naming
    /// the folder they are in (AD-019).
    /// </summary>
    /// <param name="filePath">The file.</param>
    [Theory]
    [MemberData(nameof(ExpectedFailures))]
    public void ExpectedFailure_FailsBinding(string filePath)
        => RunExpectedBindFailureUatFile(filePath, "BDDB010", "the folder 'Users'", "Using");

    /// <inheritdoc />
    /// <remarks>
    /// An earlier scenario may have restarted the app with other launch settings; they are put back
    /// here, before the runtime reads the fixture's composition, so it binds to the app as restarted.
    /// </remarks>
    protected override UatRuntime CreateRuntime()
    {
        Fixture.RestoreLaunchSettings();
        return base.CreateRuntime();
    }

    /// <inheritdoc />
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "TodoUatFixture");
}
