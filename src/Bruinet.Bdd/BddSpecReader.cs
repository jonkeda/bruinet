using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Bdd;

/// <summary>
/// Reads a Lirenet spec (<c>*.spec.md</c>) as a feature: each example a scenario named and tagged with its rule's id
/// (<c>B7.CON.01.R4 Part of a name is enough</c>, <c>@B7.CON.01.R4</c>). The spec's <c>## Background</c> goes before every
/// example; a chapter's (<c>### Chapter: …</c>) Given and When go before each of its examples (<c>#### R4 …</c>).
/// Everything else in a spec is for its readers and Lirenet. The text is rewritten line for line into the feature
/// grammar, so every step keeps its line, and then read by <see cref="BddMarkdownParser"/>.
/// </summary>
public static partial class BddSpecReader
{
    /// <summary>The tag every scenario read from a spec carries, besides its rule's id.</summary>
    public const string SpecTag = "spec";

    // An example at the Examples' own level (### R5 …): it ends the chapter before it.
    private const string OutsideChapterTag = "outside-chapter";

    /// <summary>The tag of a scenario that only checks (Then steps), after its chapter's steps: it may share its chapter's run.</summary>
    public const string ThenOnlyTag = "chapter-then";

    /// <summary>Whether a file is a spec, by its name.</summary>
    /// <param name="filePath">A file path or name.</param>
    public static bool IsSpecFile(string? filePath) =>
        filePath is not null && filePath.EndsWith(BddFileFormat.SpecExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether a text is a spec, by its title.</summary>
    /// <param name="markdown">The file's text.</param>
    public static bool IsSpec(string markdown) => SpecTitle().IsMatch(markdown);

    /// <summary>Reads a spec as a feature.</summary>
    /// <param name="markdown">The spec's text.</param>
    /// <param name="filePath">The spec's path, for locations.</param>
    public static BddParseResult Parse(string markdown, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var (feature, chapters) = ToFeature(markdown);
        var result = BddMarkdownParser.ParseFeature(feature, filePath);
        return result.Document is null || chapters.Count == 0 ? result : new BddParseResult(WithChapters(result.Document, chapters), result.Diagnostics);
    }

    /// <summary>
    /// The spec rewritten line for line into the feature grammar, and the names of the chapter scenarios it holds. A
    /// chapter becomes a scenario of its own, tagged <c>@chapter</c>, that <see cref="WithChapters"/> folds into its examples.
    /// </summary>
    internal static (string Feature, IReadOnlyList<string> Chapters) ToFeature(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var id = string.Empty;
        var inExamples = false;
        var inFence = false;
        List<string> chapters = [];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence)
            {
                continue;
            }

            if (SpecTitle().Match(line) is { Success: true } title)
            {
                id = title.Groups["id"].Value;
                lines[i] = ("# Feature: " + id + title.Groups["rest"].Value).TrimEnd();
                continue;
            }

            if (Heading().Match(line) is not { Success: true } heading)
            {
                continue;
            }

            var level = heading.Groups["hashes"].Value.Length;
            var text = heading.Groups["text"].Value.Trim();
            if (level == 2)
            {
                inExamples = text.Equals("Examples", StringComparison.OrdinalIgnoreCase);
                if (inExamples)
                {
                    lines[i] = string.Empty;
                }

                continue;
            }

            if (!inExamples || id.Length == 0)
            {
                continue;
            }

            if (level == 3 && ChapterHeading().Match(text) is { Success: true } chapter)
            {
                var name = "Chapter: " + chapter.Groups["name"].Value.Trim();
                chapters.Add(name);
                lines[i] = "## Scenario: " + name;
                Tag(lines, i, "@chapter");
                continue;
            }

            if (level is 3 or 4 && RuleHeading().Match(text) is { Success: true } rule)
            {
                var ruleId = $"{id}.R{rule.Groups["number"].Value}";
                lines[i] = $"## Scenario: {ruleId} {rule.Groups["name"].Value.Trim()}".TrimEnd();
                Tag(lines, i, level == 3 ? $"@{ruleId} @{SpecTag} @{OutsideChapterTag}" : $"@{ruleId} @{SpecTag}");
            }
        }

        return (string.Join('\n', lines), chapters);
    }

    // A tag line goes directly above the heading, on the blank line a spec has there, so no line moves.
    private static void Tag(string[] lines, int heading, string tags)
    {
        if (heading > 0 && lines[heading - 1].Trim().Length == 0)
        {
            lines[heading - 1] = tags;
        }
    }

    // Each chapter's steps go before its examples' own; the chapter scenarios themselves leave the document. An example
    // whose own steps are all Then is tagged so a runner may share its chapter's run.
    private static BddDocument WithChapters(BddDocument document, IReadOnlyList<string> chapters)
    {
        List<BddScenario> scenarios = [];
        BddScenario? chapter = null;
        foreach (var scenario in document.Scenarios)
        {
            if (scenario.Tags.Contains("chapter", StringComparer.OrdinalIgnoreCase) && chapters.Contains(scenario.Name, StringComparer.Ordinal))
            {
                chapter = scenario;
                continue;
            }

            if (scenario.Tags.Contains(OutsideChapterTag, StringComparer.Ordinal))
            {
                chapter = null;
            }

            if (chapter is null)
            {
                scenarios.Add(scenario);
                continue;
            }

            var thenOnly = scenario.Steps.Count > 0 && scenario.Steps.All(step => step.EffectiveKeyword == BddEffectiveStepKeyword.Then);
            scenarios.Add(scenario with
            {
                Steps = [.. chapter.Steps, .. scenario.Steps],
                Tags = thenOnly ? [.. scenario.Tags, ThenOnlyTag, "chapter:" + ChapterKey(chapter.Name)] : scenario.Tags,
            });
        }

        return document with { Scenarios = scenarios };
    }

    /// <summary>The key of a chapter in a scenario's <c>chapter:</c> tag: its name, without spaces.</summary>
    /// <param name="chapterName">The chapter scenario's name (<c>Chapter: The list at a glance</c>).</param>
    public static string ChapterKey(string chapterName)
    {
        ArgumentNullException.ThrowIfNull(chapterName);
        var name = chapterName.StartsWith("Chapter:", StringComparison.Ordinal) ? chapterName["Chapter:".Length..] : chapterName;
        var key = new StringBuilder();
        foreach (var c in name.Trim())
        {
            key.Append(char.IsLetterOrDigit(c) ? c : '-');
        }

        return key.ToString();
    }

    [GeneratedRegex(@"^#\s+Spec:\s+(?<id>[A-Z][A-Z0-9]*(?:\.[A-Z][A-Z0-9]*)*\.\d+)(?<rest>.*)$", RegexOptions.Multiline)]
    private static partial Regex SpecTitle();

    [GeneratedRegex(@"^(?<hashes>#{2,4})\s+(?<text>.+?)\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^Chapter:\s*(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterHeading();

    [GeneratedRegex(@"^R(?<number>\d+)\b\s*(?<name>.*)$")]
    private static partial Regex RuleHeading();
}
