using System.Text.RegularExpressions;

namespace Bruinet.Samples.Todo.JourneyCoverage;

/// <summary>Where a subtask is tested: one letter of the <c>Tier</c> column in <c>journeys.md</c>.</summary>
public enum Tier
{
    /// <summary>U - a unit test (<c>Pyramid=Unit</c>).</summary>
    Unit,

    /// <summary>I - headless, against WireMock and SQLite (<c>Pyramid=Integration</c>).</summary>
    Integration,

    /// <summary>C - against the real API in process (<c>Pyramid=Contract</c>).</summary>
    Contract,

    /// <summary>H - the app, with WireMock behind it (<c>Pyramid=UiHermetic</c>).</summary>
    UiHermetic,

    /// <summary>L - the app, with the real API behind it (<c>Pyramid=UiLive</c>).</summary>
    UiLive,

    /// <summary>S - the Smoke gate (<c>Gate=Smoke</c>).</summary>
    Smoke,

    /// <summary>R - the Reviews gate (<c>Gate=Review</c>).</summary>
    Review,

    /// <summary>M - a manual charter in <c>manual/charters.md</c>.</summary>
    Manual,
}

/// <summary>One row of <c>journeys.md</c>: a subtask and the tiers it is assigned to.</summary>
/// <param name="Id">The subtask id, <c>TOD.02.3</c>.</param>
/// <param name="Text">What the user can do or see.</param>
/// <param name="Tiers">Where it must be tested.</param>
public sealed record Subtask(string Id, string Text, IReadOnlyList<Tier> Tiers)
{
    /// <summary>The journey the subtask belongs to, <c>TOD.02</c>.</summary>
    public string Journey => Id[..Id.LastIndexOf('.')];
}

/// <summary>Reads the subtask tables of <c>journeys.md</c>.</summary>
public static partial class Journeys
{
    /// <summary>The subtasks, in file order.</summary>
    /// <param name="markdown">The file's text.</param>
    /// <exception cref="FormatException">A subtask row names a tier letter this report does not know.</exception>
    public static IReadOnlyList<Subtask> Parse(string markdown)
    {
        var subtasks = new List<Subtask>();
        foreach (var line in markdown.Split('\n'))
        {
            var row = SubtaskRow().Match(line);
            if (!row.Success)
                continue;

            var id = row.Groups["id"].Value;
            subtasks.Add(new Subtask(id, row.Groups["text"].Value.Trim(), ParseTiers(id, row.Groups["tier"].Value)));
        }

        return subtasks;
    }

    // "U + **S**", "I + L", "**R**": letters joined by '+', bold for the gates.
    private static List<Tier> ParseTiers(string id, string cell)
        => [.. cell.Split('+').Select(part => part.Replace("*", "").Trim()).Select(letter => letter switch
        {
            "U" => Tier.Unit,
            "I" => Tier.Integration,
            "C" => Tier.Contract,
            "H" => Tier.UiHermetic,
            "L" => Tier.UiLive,
            "S" => Tier.Smoke,
            "R" => Tier.Review,
            "M" => Tier.Manual,
            _ => throw new FormatException($"{id}: unknown tier '{letter}' in '{cell.Trim()}'."),
        })];

    [GeneratedRegex(@"^\|\s*(?<id>TOD\.\d{2}\.\d+)\s*\|(?<text>[^|]*)\|(?<tier>[^|]*)\|")]
    private static partial Regex SubtaskRow();
}
