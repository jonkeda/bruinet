using Bruinet.Bdd;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Uat;

/// <summary>
/// Runs a class's scenarios in the order of their files: file by file, each file's scenarios as they are written.
/// xUnit's own order is by each case's id, which mixes the files. A suite asks for it on its scenario class,
/// <c>[TestCaseOrderer("Bruinet.Uat.BddDocumentOrder", "Bruinet.Uat")]</c>; a suite that shares a chapter's run
/// (<c>ShareChapters</c>) needs it, because a chapter is shared with the example run just before.
/// </summary>
public sealed class BddDocumentOrder : ITestCaseOrderer
{
    /// <inheritdoc />
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        ArgumentNullException.ThrowIfNull(testCases);
        return InDocumentOrder(testCases, testCase => testCase.TestMethodArguments, ScenarioNamesOf);
    }

    // Cases that name a file and a scenario ([filePath, scenarioName]) go by file, then by the scenario's place in it;
    // any other case keeps its place among the others, after them.
    internal static IReadOnlyList<T> InDocumentOrder<T>(IEnumerable<T> cases, Func<T, object?[]?> arguments, Func<string, IReadOnlyList<string>> scenarioNames)
    {
        var names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        return [.. cases
            .Select((testCase, index) => (testCase, index, where: Place(arguments(testCase), names, scenarioNames)))
            .OrderBy(each => each.where.File is null)
            .ThenBy(each => each.where.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(each => each.where.Position)
            .ThenBy(each => each.index)
            .Select(each => each.testCase)];
    }

    private static (string? File, int Position) Place(object?[]? arguments, Dictionary<string, IReadOnlyList<string>> names, Func<string, IReadOnlyList<string>> scenarioNames)
    {
        if (arguments is not [string file, string scenario, ..])
        {
            return (null, 0);
        }

        if (!names.TryGetValue(file, out var inFile))
        {
            names[file] = inFile = scenarioNames(file);
        }

        var position = inFile.ToList().IndexOf(scenario);
        return (file, position < 0 ? int.MaxValue : position);
    }

    private static IReadOnlyList<string> ScenarioNamesOf(string filePath)
        => File.Exists(filePath) && BddMarkdownParser.ParseFile(filePath).Document is { } document
            ? [.. document.Scenarios.Select(scenario => scenario.Name)]
            : [];
}
