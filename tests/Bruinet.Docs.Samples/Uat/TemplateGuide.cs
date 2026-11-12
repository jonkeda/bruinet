// docs/guides/uat-template-guide.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Bdd;
using Bruinet.Core.Composition;
using Bruinet.Maui.Interfaces;
using Bruinet.Uat;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Docs.Samples.Uat;

#region docs:fixture
[TestModuleScan(typeof(TodoListPage), NamespacePrefix = "Bruinet.Samples.Todo.UITests.Pages")]
public sealed class TodoUatFixture : TodoAppFixture
{
    public TodoUatFixture()
    {
        Composition = TestComposition.ForFixture(this, services =>
            services.AddSingleton<IMauiTestContext>(Context));
    }

    public TestComposition Composition { get; }
}
#endregion

#region docs:collection
[CollectionDefinition(CollectionName)]
public sealed class TodoUatCollection : ICollectionFixture<TodoUatFixture>
{
    public const string CollectionName = "Todo UAT";
}
#endregion

#region docs:scenarios
[Collection(TodoUatCollection.CollectionName)]
[TestCaseOrderer(BddScenarioOrderer.TypeName, BddScenarioOrderer.AssemblyName)]
public sealed class TodoUatScenarioTests : UatScenarioTestBase<TodoUatFixture>
{
    public TodoUatScenarioTests(TodoUatFixture fixture) : base(fixture) { }

    public static IEnumerable<object[]> Scenarios => GetScenarioTheoryData();

    [UatScenarioTheory(Timeout = 120000)]
    [MemberData(nameof(Scenarios))]
    public Task Scenario_Passes(string filePath, string scenarioName) => RunUatScenarioAsync(filePath, scenarioName);

    // Load the file's TestData before its first scenario (the fixture may restart the app).
    protected override void BeforeFile(BddDocument document)
        => Fixture.UseTestSet(document.Metadata.GetValueOrDefault(BddMetadataFields.TestData, TodoUatFixture.DefaultTestData));

    // Reset the app between scenarios so they stay order-independent.
    protected override void BeforeScenario(BddBoundScenario scenario) => Fixture.StartTest();

    protected override UatRuntimeValidationOptions RuntimeValidation { get; } =
        new(Target: "MAUI", Fixture: "TodoUatFixture");
}
#endregion

public sealed class StoredDataTablePhrases(ITodoArrangement fixture) : BddStepDefinitionsBase
{
    #region docs:table
    [BddThen("the database should have todos")]
    public void DatabaseHas(BddTable table)
        => table.AssertContains(TodoDbReader.Todos(fixture.DatabasePath), fixture.TestSet.Now);
    #endregion
}
