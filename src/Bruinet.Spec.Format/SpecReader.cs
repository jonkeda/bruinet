using System.Text.RegularExpressions;

namespace Bruinet.Spec.Format;

/// <summary>
/// Reads a spec (<c>*.spec.md</c>) into a <see cref="SpecDocument"/>: the one reading of the format, which the spec check
/// and the spec runner share. Headings inside a fenced block (<c>```</c> or <c>~~~</c>) are text, not headings.
/// </summary>
public static partial class SpecReader
{
    /// <summary>The extension of a spec file.</summary>
    public const string Extension = ".spec.md";

    /// <summary>Whether a file is a spec, by its name.</summary>
    /// <param name="path">A file path or name.</param>
    public static bool IsSpecFile(string? path) =>
        path is not null && path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a spec's text.</summary>
    /// <param name="text">The spec's text, with any line endings.</param>
    public static SpecDocument Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n')
            .Select((line, index) => new SpecLine(index + 1, line))
            .ToList();

        var title = lines.Count > 0 ? Title().Match(lines[0].Text) : Match.Empty;
        List<SpecLine> header = [];
        List<(SpecLine Heading, List<SpecLine> Body)> sections = [];
        foreach (var (line, inFence) in Fenced(lines.Skip(1)))
        {
            if (!inFence && line.Text.StartsWith("## ", StringComparison.Ordinal))
                sections.Add((line, []));
            else if (sections.Count == 0)
                header.Add(line);
            else
                sections[^1].Body.Add(line);
        }

        var read = sections.Select(section => new SpecSection(section.Heading.Number, section.Heading.Text[3..].Trim(), section.Body)).ToList();
        var examples = read.FirstOrDefault(section => section.Name == "Examples");
        return new(
            title.Success ? title.Groups["id"].Value : null,
            title.Success ? title.Groups["rest"].Value : string.Empty,
            header,
            read,
            examples is null ? [] : Blocks(examples.Body));
    }

    /// <summary>The rows of the Markdown tables among some lines; a separator row (<c>| --- |</c>) is left out.</summary>
    /// <param name="lines">Lines of a spec.</param>
    public static IReadOnlyList<SpecRow> TableRows(IEnumerable<SpecLine> lines) => [.. lines
        .Where(line => line.Text.TrimStart().StartsWith('|'))
        .Select(line => new SpecRow(line.Number, [.. line.Text.Trim().Trim('|').Split('|').Select(cell => cell.Trim())]))
        .Where(row => !row.Cells.All(cell => cell.Length > 0 && cell.All(c => c is '-' or ':')))];

    private static List<SpecBlock> Blocks(IReadOnlyList<SpecLine> body)
    {
        List<SpecBlock> blocks = [];
        foreach (var (heading, lines) in Split(body, "### "))
        {
            var text = heading.Text[4..].Trim();
            if (Chapter().Match(text) is { Success: true } chapter)
            {
                var name = chapter.Groups["name"].Value.Trim();
                var preamble = Fenced(lines).TakeWhile(entry => entry.InFence || !entry.Line.Text.StartsWith("#### ", StringComparison.Ordinal)).Select(entry => entry.Line).ToList();
                var examples = Split(lines, "#### ").Select(example => Example(example.Heading, example.Heading.Text[5..].Trim(), example.Body, name)).ToList();
                blocks.Add(new SpecChapter(heading.Number, text, lines, name, preamble, examples));
                continue;
            }

            blocks.Add(Example(heading, text, lines, null));
        }

        return blocks;
    }

    private static SpecExample Example(SpecLine heading, string text, IReadOnlyList<SpecLine> body, string? chapter) =>
        Rule().Match(text) is { Success: true } rule
            ? new(heading.Number, text, body, int.Parse(rule.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture), rule.Groups["name"].Value.Trim(), chapter)
            : new(heading.Number, text, body, null, text, chapter);

    // The headings at one level, each with the lines under it up to the next; lines before the first belong to none.
    private static List<(SpecLine Heading, List<SpecLine> Body)> Split(IEnumerable<SpecLine> lines, string level)
    {
        List<(SpecLine Heading, List<SpecLine> Body)> parts = [];
        foreach (var (line, inFence) in Fenced(lines))
        {
            if (!inFence && line.Text.StartsWith(level, StringComparison.Ordinal))
                parts.Add((line, []));
            else if (parts.Count > 0)
                parts[^1].Body.Add(line);
        }

        return parts;
    }

    // Each line, and whether it is inside a fenced block; a fence's own lines count as inside.
    private static IEnumerable<(SpecLine Line, bool InFence)> Fenced(IEnumerable<SpecLine> lines)
    {
        var inFence = false;
        foreach (var line in lines)
        {
            var fence = line.Text.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.Text.TrimStart().StartsWith("~~~", StringComparison.Ordinal);
            yield return (line, inFence || fence);
            if (fence)
                inFence = !inFence;
        }
    }

    // A journey id may carry a product before it (B7.HRS.01): upper-case parts, then the journey's number.
    [GeneratedRegex(@"^#\s+Spec:\s+(?<id>[A-Z][A-Z0-9]*(?:\.[A-Z][A-Z0-9]*)*\.\d+)(?<rest>.*)$")]
    private static partial Regex Title();

    [GeneratedRegex(@"^Chapter:\s*(?<name>\S.*)$")]
    private static partial Regex Chapter();

    [GeneratedRegex(@"^R(?<number>\d+)\b\s*(?<name>.*)$")]
    private static partial Regex Rule();
}
