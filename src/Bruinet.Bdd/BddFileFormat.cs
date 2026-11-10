using System.Diagnostics.CodeAnalysis;
namespace Bruinet.Bdd;

/// <summary>
/// The names of feature and step files, and the rewrite from the forms before them
/// (<c>Scenarios/</c>, <c>*.uat.md</c>, <c># UAT:</c>, <c>*.phrases.md</c>, <c>## Phrase:</c>) that
/// <c>bruinet data validate --fix</c> applies.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddFileFormat
{
    /// <summary>The folder of feature and step files, beside the config.</summary>
    public const string FeatureFolder = "Features";

    /// <summary>The name <see cref="FeatureFolder"/> had before feature files.</summary>
    public const string OldFeatureFolder = "Scenarios";

    /// <summary>The name ending of a feature file.</summary>
    public const string FeatureExtension = ".feature.md";

    /// <summary>The search pattern for feature files.</summary>
    public const string FeatureFilePattern = "*" + FeatureExtension;

    /// <summary>The name ending of a step file.</summary>
    public const string StepsExtension = ".steps.md";

    /// <summary>The search pattern for step files.</summary>
    public const string StepFilePattern = "*" + StepsExtension;

    /// <summary>The config of a suite that drives a UI.</summary>
    public const string UiConfigFileName = "uat.config.md";

    /// <summary>The config of a suite below the UI (the <c>service</c> head).</summary>
    public const string ServiceConfigFileName = "bdd.config.md";

    /// <summary>
    /// The config in a folder: <c>uat.config.md</c> or <c>bdd.config.md</c>. The name says which
    /// kind of suite it is; both are read the same way. When neither exists, the
    /// <c>uat.config.md</c> path.
    /// </summary>
    /// <param name="directory">The folder to look in.</param>
    public static string FindConfig(string directory)
    {
        var service = Path.Combine(directory, ServiceConfigFileName);
        var ui = Path.Combine(directory, UiConfigFileName);
        return !File.Exists(ui) && File.Exists(service) ? service : ui;
    }

    /// <summary>Whether a file name is a config's.</summary>
    /// <param name="fileName">A file name, without folder.</param>
    public static bool IsConfigFileName(string fileName) =>
        fileName.Equals(UiConfigFileName, StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals(ServiceConfigFileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The diagnostic code for a heading or file name in the old form.</summary>
    public const string OldFormCode = "UAT029";

    private static readonly (string Old, string New)[] Extensions =
    [
        (".uat.md", FeatureExtension),
        (".phrases.md", StepsExtension),
    ];

    // Longest first, so '## Phrase for each row:' is not read as '## Phrase:'.
    private static readonly (string Old, string New)[] Headings =
    [
        ("## Phrase for each row:", "## Step for each row:"),
        ("## Phrase:", "## Step:"),
        ("# Phrases:", "# Steps:"),
        ("# UAT:", "# Feature:"),
    ];

    /// <summary>The search patterns of files in the old form.</summary>
    public static IEnumerable<string> OldFilePatterns => Extensions.Select(x => "*" + x.Old);

    /// <summary>The new name of a file in the old form; null when its name is already right.</summary>
    /// <param name="path">A file path.</param>
    public static string? NewFileName(string path)
    {
        foreach (var (old, @new) in Extensions)
        {
            if (path.EndsWith(old, StringComparison.OrdinalIgnoreCase))
            {
                return path[..^old.Length] + @new;
            }
        }

        return null;
    }

    /// <summary>The new form of a heading in the old form; null when it is not one.</summary>
    /// <param name="trimmedLine">A line, trimmed.</param>
    public static string? NewHeading(string trimmedLine)
    {
        foreach (var (old, @new) in Headings)
        {
            if (trimmedLine.StartsWith(old, StringComparison.Ordinal))
            {
                return @new + trimmedLine[old.Length..];
            }
        }

        return null;
    }

    /// <summary>Rewrites every heading in the old form; the rest of the text is kept as it is.</summary>
    /// <param name="text">A feature or step file's text.</param>
    /// <returns>The text, and how many headings changed.</returns>
    public static (string Text, int Count) RewriteHeadings(string text)
    {
        var count = 0;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var indent = line[..(line.Length - line.TrimStart().Length)];
            if (NewHeading(line.Trim()) is { } heading)
            {
                lines[i] = indent + heading + (line.EndsWith('\r') ? "\r" : string.Empty);
                count++;
            }
        }

        return (string.Join('\n', lines), count);
    }

    /// <summary>What a "no steps" error adds when its section holds text that reads like steps.</summary>
    public const string FenceHint = "Steps go in a ```gherkin block; run 'bruinet data validate --fix' to wrap them.";

    private static readonly string[] StepSections = ["## Background", "## Scenario:", "## Scenario Outline:", "## Step:", "## Step for each row:"];

    private static readonly string[] StepKeywords = ["Given ", "When ", "Then ", "And ", "But "];

    /// <summary>
    /// Wraps the steps of each step section that has no <c>```gherkin</c> block in one: the form
    /// before AD-023, where steps were written bare. A section with a block is left as it is, and
    /// so are its Parameters table and the tags above the next heading.
    /// </summary>
    /// <param name="text">A feature or step file's text.</param>
    /// <returns>The text, and how many sections changed.</returns>
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    public static (string Text, int Count) FenceSteps(string text)
    {
        var lines = text.Split('\n').ToList();
        var fenced = FencedLines(lines);
        List<(int Start, int Last, string Indent, string Eol)> wraps = [];
        for (var h = 0; h < lines.Count; h++)
        {
            var heading = lines[h].Trim();
            heading = NewHeading(heading) ?? heading;
            if (fenced[h] || !StepSections.Any(section => heading.StartsWith(section, StringComparison.Ordinal)))
            {
                continue;
            }

            var end = h + 1;
            while (end < lines.Count && !(fenced[end] ? IsFenceDelimiter(lines[end]) : lines[end].TrimStart().StartsWith('#')))
            {
                end++;
            }

            // A fence in the section: it already has a block, or holds something else.
            if (end < lines.Count && fenced[end])
            {
                continue;
            }

            var start = h + 1;
            while (start < end && string.IsNullOrWhiteSpace(lines[start]))
            {
                start++;
            }

            if (heading.StartsWith("## Step", StringComparison.Ordinal) && start < end &&
                lines[start].Trim() is var first && first.StartsWith('|') && first.TrimStart('|').TrimStart().StartsWith("Parameter", StringComparison.OrdinalIgnoreCase))
            {
                while (start < end && lines[start].TrimStart().StartsWith('|'))
                {
                    start++;
                }

                while (start < end && string.IsNullOrWhiteSpace(lines[start]))
                {
                    start++;
                }
            }

            var last = end - 1;
            while (last >= start && (string.IsNullOrWhiteSpace(lines[last]) || IsTagLine(lines[last])))
            {
                last--;
            }

            if (last < start || !Enumerable.Range(start, last - start + 1).Any(i => StepKeywords.Any(keyword => lines[i].TrimStart().StartsWith(keyword, StringComparison.Ordinal))))
            {
                continue;
            }

            var raw = lines[h];
            wraps.Add((start, last, raw[..(raw.Length - raw.TrimStart().Length)], raw.EndsWith('\r') ? "\r" : string.Empty));
        }

        for (var i = wraps.Count - 1; i >= 0; i--)
        {
            var (start, last, indent, eol) = wraps[i];
            if (last == lines.Count - 1)
            {
                // The text ends in a step: the closing fence becomes the last line.
                lines[last] += eol;
                lines.Add(indent + "```");
            }
            else
            {
                lines.Insert(last + 1, indent + "```" + eol);
            }

            lines.Insert(start, indent + "```gherkin" + eol);
        }

        return (string.Join('\n', lines), wraps.Count);
    }

    // Each line inside a fence, its delimiters included.
    private static bool[] FencedLines(List<string> lines)
    {
        var fenced = new bool[lines.Count];
        string? open = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (open is null && IsFenceDelimiter(trimmed))
            {
                open = trimmed[..3];
                fenced[i] = true;
            }
            else if (open is not null)
            {
                fenced[i] = true;
                if (trimmed.StartsWith(open, StringComparison.Ordinal) && trimmed.TrimStart(open[0]).Length == 0)
                {
                    open = null;
                }
            }
        }

        return fenced;
    }

    private static bool IsFenceDelimiter(string line)
        => line.TrimStart().StartsWith("```", StringComparison.Ordinal) || line.TrimStart().StartsWith("~~~", StringComparison.Ordinal);

    private static bool IsTagLine(string line)
        => line.Trim() is { Length: > 0 } trimmed && trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => word.StartsWith('@'));
}
