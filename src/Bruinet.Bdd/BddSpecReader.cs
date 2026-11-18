using System.Text;
using Bruinet.Spec.Format;

namespace Bruinet.Bdd;

/// <summary>
/// Reads a spec (<c>*.spec.md</c>) as a feature: each example a scenario named and tagged with its rule's id
/// (<c>B7.CON.01.R4 Part of a name is enough</c>, <c>@B7.CON.01.R4</c>). The spec's <c>## Background</c> goes before every
/// example; a chapter's (<c>### Chapter: …</c>) Given and When go before each of its examples (<c>#### R4 …</c>).
/// Everything else in a spec is for its readers and the spec check. The text is rewritten line for line into the feature
/// grammar, so every step keeps its line, and then read by <see cref="BddMarkdownParser"/>. Where the title, the
/// chapters and the examples are comes from the format's one reader, <see cref="SpecReader"/>.
/// </summary>
public static class BddSpecReader
{
    /// <summary>The tag every scenario read from a spec carries, besides its rule's id.</summary>
    public const string SpecTag = "spec";

    // An example at the Examples' own level (### R5 …): it ends the chapter before it.
    private const string OutsideChapterTag = "outside-chapter";

    /// <summary>The tag of a scenario that only checks (Then steps), after its chapter's steps: it may share its chapter's run.</summary>
    public const string ThenOnlyTag = "chapter-then";

    /// <summary>Whether a file is a spec, by its name.</summary>
    /// <param name="filePath">A file path or name.</param>
    public static bool IsSpecFile(string? filePath) => SpecReader.IsSpecFile(filePath);

    /// <summary>Whether a text is a spec, by its title.</summary>
    /// <param name="markdown">The file's text.</param>
    public static bool IsSpec(string markdown) => SpecReader.Read(markdown).JourneyId is not null;

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
        var spec = SpecReader.Read(markdown);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        List<string> chapters = [];

        if (spec.Section("Examples") is { } examples)
        {
            lines[examples.Line - 1] = string.Empty;
        }

        if (spec.JourneyId is not { } id)
        {
            return (string.Join('\n', lines), chapters);
        }

        lines[0] = ("# Feature: " + id + spec.TitleRest).TrimEnd();
        foreach (var block in spec.Blocks)
        {
            if (block is SpecChapter chapter)
            {
                var name = "Chapter: " + chapter.Name;
                chapters.Add(name);
                lines[chapter.Line - 1] = "## Scenario: " + name;
                Tag(lines, chapter.Line - 1, "@chapter");
            }

            foreach (var example in block is SpecChapter of ? of.Examples : [(SpecExample)block])
            {
                if (example.RuleNumber is not { } number)
                {
                    continue;
                }

                var ruleId = $"{id}.R{number}";
                lines[example.Line - 1] = $"## Scenario: {ruleId} {example.Name}".TrimEnd();
                Tag(lines, example.Line - 1, example.ChapterName is null ? $"@{ruleId} @{SpecTag} @{OutsideChapterTag}" : $"@{ruleId} @{SpecTag}");
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
}
