// docs/guides/test-data.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using System.Text.Json;
using Bruinet.Bdd;
using Bruinet.Mocking;
using Bruinet.TestData;
using Bruinet.Uat;
using Xunit;

namespace Bruinet.Docs.Samples.Uat;

#region docs:definition
public sealed class TodoTestDataDefinition : TestDataDefinition<TodoTestSetFile>
{
    public override JsonSerializerOptions JsonOptions => TodoTestSet.JsonOptions;

    public override IReadOnlyList<Type> TableTypes => [typeof(TodoDto), typeof(LocalTodoData)];

    protected override string RelativeFolder => "TestData";

    protected override void DefineRules(TestDataRules<TodoTestSetFile> rules) => rules
        .UniqueIds("server", set => set.Server, todo => todo.Id)
        .Require("server", set => set.Server, todo => todo.CreatedAt <= todo.UpdatedAt, "updatedAt is before createdAt.")
        .ForEach("server", set => set.Server, todo => TodoValidation.ValidateTitle(todo.Title));
}
#endregion

public sealed class TestDataGate
{
    #region docs:gate
    [Fact]
    public void EveryTestSet_FollowsTheRules()
    {
        var report = TestDataValidator.Validate(new TodoTestDataDefinition());
        Assert.True(report.IsValid, report.Format());
    }
    #endregion
}

public sealed class TestDataScenarioTests(TodoUatFixture fixture) : UatScenarioTestBase<TodoUatFixture>(fixture)
{
    protected override UatRuntimeValidationOptions RuntimeValidation { get; } = new(Target: "MAUI", Fixture: "TodoUatFixture");

    #region docs:before-file
    protected override void BeforeFile(BddDocument document)
        => Fixture.UseTestSet(document.Metadata.TryGetValue(BddMetadataFields.TestData, out var name) && name.Length > 0
            ? name
            : TodoUatFixture.DefaultTestData);
    #endregion
}

public static class MockBodies
{
    public static void Stub(MockApiServer mock)
    {
        // The second line is the analyzer's counter-example, kept so the doc shows what it reports.
#pragma warning disable BRUINET001
        #region docs:mock-bodies
        mock.Get(TodoApi.TodosPath).ReturnsJson(new[] { new TodoDto(1, "Milk") });   // fine
        mock.Get(TodoApi.TodosPath).ReturnsJson(new { titel = "x" });           // BRUINET001
        #endregion
#pragma warning restore BRUINET001
    }
}
