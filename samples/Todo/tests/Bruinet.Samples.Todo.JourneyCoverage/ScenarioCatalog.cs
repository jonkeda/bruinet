using System.Text.RegularExpressions;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.JourneyCoverage;

/// <summary>
/// The scenarios of the sample's scenario files, as tests: parsed, not bound or run.
/// </summary>
/// <remarks>
/// A scenario covers the subtasks its id tags name (<c>@TOD.07.2</c>), in the tier of each head it
/// runs on: <c>@service</c> is Integration, a UI head (<c>@maui</c>, <c>@web</c>, <c>@mixed</c>) or
/// no head tag is UiHermetic. A scenario tagged for both counts in both tiers. <c>@smoke</c> and
/// <c>@review</c> are the gates.
/// </remarks>
public static partial class ScenarioCatalog
{
    /// <summary>Where the build links the scenario files, under this project's output.</summary>
    public const string Folder = "Features";

    /// <summary>Every scenario, once per tier it runs in.</summary>
    public static IReadOnlyList<TestMethod> Load()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, Folder);
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.feature.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal).SelectMany(Read)]
            : throw new DirectoryNotFoundException($"No scenario files at {folder}; the project links them from the UAT suites.");
    }

    /// <summary>The tests one scenario file contributes.</summary>
    /// <param name="filePath">The file.</param>
    public static IEnumerable<TestMethod> Read(string filePath)
    {
        var document = BddMarkdownParser.ParseFile(filePath).Document
            ?? throw new InvalidOperationException($"{filePath} does not parse; the UAT suites report why.");
        var file = Path.GetFileName(filePath).Replace(".feature.md", string.Empty, StringComparison.Ordinal);

        foreach (var scenario in document.Scenarios)
        {
            var tags = scenario.Tags.Select(BddTagConventions.NormalizeTag).ToList();
            var journeys = tags.Where(tag => SubtaskId().IsMatch(tag)).Distinct().ToList();
            var gates = tags.Select(Gate).OfType<string>().Distinct().ToList();
            foreach (var pyramid in Tiers(tags))
            {
                yield return new TestMethod($"{file}: {scenario.Name}", pyramid, gates, journeys, 1);
            }
        }
    }

    private static IEnumerable<string> Tiers(IReadOnlyList<string> tags)
    {
        var heads = BddTagConventions.HeadsOf(tags.Select(BddTagConventions.NormalizeTag).ToHashSet(StringComparer.OrdinalIgnoreCase));
        if (heads.Contains(BddTagConventions.Service, StringComparer.OrdinalIgnoreCase))
        {
            yield return "Integration";
        }

        if (heads.Count == 0 || heads.Any(head => !head.Equals(BddTagConventions.Service, StringComparison.OrdinalIgnoreCase)))
        {
            yield return "UiHermetic";
        }
    }

    private static string? Gate(string tag) => tag.ToLowerInvariant() switch
    {
        "smoke" => "Smoke",
        "review" => "Review",
        _ => null,
    };

    [GeneratedRegex(@"^TOD\.\d+\.\d+$")]
    private static partial Regex SubtaskId();
}
