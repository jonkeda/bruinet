using Bruinet.Bdd;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Docs.Samples.Uat;

/// <summary>The Todo sample's own orderer (samples/Todo/tests/Bruinet.Samples.Todo.Uat/Runtime), as the template guide uses it.</summary>
public sealed class BddScenarioOrderer : ITestCaseOrderer
{
    public const string TypeName = "Bruinet.Docs.Samples.Uat.BddScenarioOrderer";

    public const string AssemblyName = "Bruinet.Docs.Samples";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
        => testCases.OrderBy(
            testCase => testCase.TestMethodArguments is [string file, string scenario, ..]
                ? BddScenarioSource.GetOrderKey(file, scenario)
                : testCase.DisplayName,
            StringComparer.Ordinal);
}
