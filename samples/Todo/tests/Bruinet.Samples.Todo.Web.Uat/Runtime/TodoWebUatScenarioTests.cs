using Bruinet.Uat;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Samples.Todo.Web.Uat.Runtime;

/// <summary>
/// Runs every scenario of the Todo scenario files against the hermetic web head, one test case per
/// scenario (and per outline row). A scenario tagged for another head only (<c>@maui</c> without
/// <c>@web</c>) is skipped by the engine: <c>Head</c> is <c>web</c> in <c>uat.config.md</c>.
/// </summary>
/// <remarks>
/// As in the MAUI suite: before a file's first scenario, the fixture (<see cref="IUatScenarioLifecycle"/>) loads the
/// file's <c>TestData</c>; each scenario then starts from the list, online, with the backend reset.
/// </remarks>
[Collection(TodoWebUatCollection.CollectionName)]
[TestCaseOrderer(BddScenarioOrderer.TypeName, BddScenarioOrderer.AssemblyName)]
[Trait("Category", "UAT")]
[Trait("Target", "BLAZOR")]
public sealed class TodoWebUatScenarioTests : UatScenarioTestBase<TodoWebUatFixture>
{
    /// <summary>Creates the scenario runner over the shared fixture.</summary>
    /// <param name="fixture">The hermetic web head.</param>
    public TodoWebUatScenarioTests(TodoWebUatFixture fixture)
        : base(fixture)
    {
    }

    /// <summary>One case per scenario: <c>[filePath, scenarioName]</c>.</summary>
    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    /// <summary>Runs one scenario and asserts it passes, or that the engine skipped it.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario in it.</param>
    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    /// <inheritdoc />
    /// <remarks>An earlier scenario may have restarted the app with other launch settings; they are put back here.</remarks>
    protected override UatRuntime CreateRuntime()
    {
        Fixture.RestoreLaunchSettings();
        return base.CreateRuntime();
    }

    /// <inheritdoc />
    /// <remarks>Next to the screenshot: what the fake backend received, with its clients and times.</remarks>
    protected override string? CaptureEvidenceOnFailure(BddBoundScenario scenario)
    {
        var screenshot = base.CaptureEvidenceOnFailure(scenario);
        if (!string.IsNullOrEmpty(screenshot))
        {
            File.WriteAllLines(Path.ChangeExtension(screenshot, ".requests.txt"), Fixture.Backend.Requests().Select(request =>
                $"{request.ReceivedAtUtc:HH:mm:ss.fff} {request.Method} {request.Path} "
                + $"[{(request.Headers.TryGetValue(Contracts.TodoApi.ClientHeader, out var client) ? client : "-")}]"));
        }

        return screenshot;
    }

    /// <inheritdoc />
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "BLAZOR", Fixture: "TodoWebUatFixture");
}

/// <summary>
/// Runs scenario cases in <see cref="BddScenarioSource.GetOrderKey"/> order - by test set, then
/// file, then position - so the app restarts once per test set.
/// </summary>
public sealed class BddScenarioOrderer : ITestCaseOrderer
{
    /// <summary>For <see cref="TestCaseOrdererAttribute"/>.</summary>
    public const string TypeName = "Bruinet.Samples.Todo.Web.Uat.Runtime.BddScenarioOrderer";

    /// <summary>For <see cref="TestCaseOrdererAttribute"/>.</summary>
    public const string AssemblyName = "Bruinet.Samples.Todo.Web.Uat";

    /// <inheritdoc />
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(
            testCase => testCase.TestMethodArguments is [string file, string scenario, ..]
                ? BddScenarioSource.GetOrderKey(file, scenario)
                : testCase.DisplayName,
            StringComparer.Ordinal);
}
