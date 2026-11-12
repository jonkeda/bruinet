using System.Text.RegularExpressions;

namespace Bruinet.Core.Tests;

/// <summary>
/// A doc's code sample is the code the build compiles (review 06, proposal 4): a block after a
/// <c>&lt;!-- sample: &lt;file&gt;#&lt;name&gt; --&gt;</c> marker equals that file's <c>#region docs:&lt;name&gt;</c>, and a quick
/// start has a marker on every C# block. A region's common indentation is left out, so a statement inside a method
/// is quoted as the doc shows it. <c>tools/Scripts/update-doc-samples.ps1</c> rewrites the blocks.
/// </summary>
public sealed partial class DocsSamplesTests
{
    [Fact]
    public void EveryMarkedSample_EqualsItsRegion_AndAQuickStartMarksEveryBlock()
    {
        var root = SolutionTests.FindRepositoryRoot();
        Assert.NotNull(root);
        var problems = new List<string>();
        var marked = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file).ReplaceLineEndings("\n");
            var where = Path.GetRelativePath(root, file);
            foreach (System.Text.RegularExpressions.Match sample in Sample().Matches(text))
            {
                marked++;
                var source = Path.Combine(root, sample.Groups["file"].Value);
                if (!File.Exists(source))
                {
                    problems.Add($"{where}: {sample.Groups["file"].Value} does not exist");
                    continue;
                }

                var region = Region(File.ReadAllText(source).ReplaceLineEndings("\n"), sample.Groups["name"].Value);
                if (region is null)
                {
                    problems.Add($"{where}: {sample.Groups["file"].Value} has no #region docs:{sample.Groups["name"].Value}");
                }
                else if (region != sample.Groups["code"].Value.Trim('\n'))
                {
                    problems.Add($"{where}: the {sample.Groups["name"].Value} sample differs from its region; run tools/Scripts/update-doc-samples.ps1");
                }
            }

            if (Path.GetFileName(file).StartsWith("quick-start-", StringComparison.Ordinal)
                && Unmarked().Count(text) > 0)
            {
                problems.Add($"{where}: a quick start's C# blocks all come from compiled files");
            }
        }

        Assert.True(marked > 0, "No marked sample found: is the marker still <!-- sample: <file>#<name> -->?");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // The region's lines, without the #region and #endregion lines and without their common indentation: a region
    // inside a method or a class reads as the doc shows it.
    private static string? Region(string source, string name)
    {
        var start = source.IndexOf($"#region docs:{name}\n", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start = source.IndexOf('\n', start) + 1;
        var end = source.IndexOf("#endregion", start, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var lines = source[start..end].TrimEnd(' ').Trim('\n').Split('\n');
        var indent = lines.Where(line => line.Trim().Length > 0).Select(line => line.Length - line.TrimStart(' ').Length).DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart(' ')));
    }

    [GeneratedRegex(@"<!-- sample: (?<file>[^#\s]+)#(?<name>[\w-]+) -->\n```csharp\n(?<code>.*?)```", RegexOptions.Singleline)]
    private static partial Regex Sample();

    // A C# block with no marker line before it.
    [GeneratedRegex(@"(?<!-->\n)```csharp\n")]
    private static partial Regex Unmarked();
}
