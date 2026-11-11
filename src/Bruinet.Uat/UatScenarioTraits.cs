using System.Text.RegularExpressions;
using Bruinet.Traits;

namespace Bruinet.Uat;

/// <summary>
/// The traits a scenario's own feature file gives its case, so one runner over a folder still files each
/// scenario under its module, journeys and tier: <see cref="TraitKeys.Module"/> from the <c>Area</c>
/// metadata, <see cref="TraitKeys.Journey"/> from the scenario's journey tags (the file's <c>Journey</c>
/// metadata when it has none), and <see cref="TraitKeys.Pyramid"/> from <c>Priority</c> when that names a
/// tier (<c>Video</c>, <c>UI</c>, ...). A <c>Priority</c> such as <c>Smoke</c> names no tier, so the
/// runner's own tier attribute stands.
/// </summary>
public static partial class UatScenarioTraits
{
    /// <summary>The traits of the scenario <paramref name="scenarioName"/> in <paramref name="file"/>; empty when the file or the scenario is not found.</summary>
    /// <param name="file">The feature file.</param>
    /// <param name="scenarioName">The scenario's name.</param>
    public static IReadOnlyList<KeyValuePair<string, string>> Of(string file, string scenarioName)
    {
        var document = File.Exists(file) ? BddMarkdownParser.ParseFile(file).Document : null;
        var scenario = document?.Scenarios.FirstOrDefault(candidate => candidate.Name == scenarioName);
        return document is null || scenario is null ? [] : Of(document.Metadata, scenario.Tags);
    }

    /// <summary>The traits a file's metadata and a scenario's tags give.</summary>
    /// <param name="metadata">The file's <c>Metadata</c> table.</param>
    /// <param name="scenarioTags">The scenario's tags, with or without <c>@</c>.</param>
    public static IReadOnlyList<KeyValuePair<string, string>> Of(IReadOnlyDictionary<string, string> metadata, IEnumerable<string> scenarioTags)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(scenarioTags);
        List<KeyValuePair<string, string>> traits = [];

        if (Field(metadata, "Area") is { } area)
        {
            traits.Add(new(TraitKeys.Module, area));
        }

        var journeys = scenarioTags.Select(BddTagConventions.NormalizeTag).Where(IsJourney).ToList();
        if (journeys.Count == 0 && Field(metadata, "Journey") is { } fileJourneys)
        {
            journeys = [.. fileJourneys.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(IsJourney)];
        }

        traits.AddRange(journeys.Distinct(StringComparer.Ordinal).Select(journey => new KeyValuePair<string, string>(TraitKeys.Journey, journey)));

        if (Tier(Field(metadata, "Priority")) is { } tier)
        {
            traits.Add(new(TraitKeys.Pyramid, tier));
            // As the tier attributes write it: [Ui] also the old name during the move, [Video] also its Category.
            if (tier == TierNames.Ui)
            {
                traits.Add(new(TraitKeys.Pyramid, TierNames.HermeticUi));
            }
            else if (tier == TierNames.Video)
            {
                traits.Add(new(TraitKeys.Category, TierNames.Video));
            }
        }

        return traits;
    }

    /// <summary>
    /// Adds <paramref name="traits"/> to a case's <paramref name="existing"/> traits, leaving a key the runner's
    /// attributes already wrote as they wrote it.
    /// </summary>
    /// <param name="existing">The case's traits.</param>
    /// <param name="traits">The file's traits.</param>
    public static void AddTo(Dictionary<string, List<string>> existing, IEnumerable<KeyValuePair<string, string>> traits)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(traits);
        foreach (var group in traits.GroupBy(trait => trait.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (existing.TryGetValue(group.Key, out var values) && values.Count > 0)
            {
                continue;
            }

            existing[group.Key] = [.. group.Select(trait => trait.Value).Distinct(StringComparer.Ordinal)];
        }
    }

    // Video, or a tier by its name (old names map to their new one); anything else names no tier.
    private static string? Tier(string? priority)
        => priority is null ? null
            : TierNames.All.Concat([TierNames.HermeticUi]).FirstOrDefault(tier => tier.Equals(priority, StringComparison.OrdinalIgnoreCase)) is { } named
                ? TierNames.Current(named)
                : null;

    private static string? Field(IReadOnlyDictionary<string, string> metadata, string name)
        => metadata.FirstOrDefault(field => field.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value?.Trim() is { Length: > 0 } value ? value : null;

    // A journey id has a prefix and at least one dotted part: CTS.06, CTS.06.3.
    private static bool IsJourney(string tag) => JourneyId().IsMatch(tag);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$")]
    private static partial Regex JourneyId();
}
