// docs/guides/coming-from-reqnroll.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Bdd;

namespace Bruinet.Docs.Samples.Uat;

#region docs:stored-data
// Bruinet (samples/Todo/tests/Bruinet.Samples.Todo.TestSupport/Uat/StoredDataPhrases.cs)
[BddStepDefinitions(Namespace = "StoredData")]
public sealed class StoredDataPhrases(ITodoArrangement fixture) : BddStepDefinitionsBase
{
    [BddThen("the database should have todos")]
    public void DatabaseHas(BddTable table) => table.AssertContains(TodoDbReader.Todos(fixture.DatabasePath), fixture.TestSet.Now);
}
#endregion
