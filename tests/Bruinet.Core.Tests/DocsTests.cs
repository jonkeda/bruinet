using System.Text.RegularExpressions;

namespace Bruinet.Core.Tests;

/// <summary>
/// The docs point at what is there (review 06, proposal 3): every relative link in README.md and docs/ reaches a file,
/// and every repository path written in backticks exists. The command lines are Bruinet.Cli.Tests' DocsCommandLineTests.
/// </summary>
public sealed partial class DocsTests
{
    // A path in a consumer's repository, named as an example (docs/guides/bunit.md names Tomorrow's UI host).
    private static readonly string[] ForeignProjects = ["Tomorrow.", "Kofflard.", "Exact."];

    [Fact]
    public void EveryRelativeLinkAndRepositoryPath_InTheDocs_Exists()
    {
        var root = SolutionTests.FindRepositoryRoot();
        Assert.NotNull(root);
        var files = new[] { Path.Combine(root, "README.md") }
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories));

        var missing = new List<string>();
        foreach (var file in files)
        {
            var text = Fence().Replace(File.ReadAllText(file), string.Empty);
            var where = Path.GetRelativePath(root, file);
            foreach (System.Text.RegularExpressions.Match link in Link().Matches(text))
            {
                var target = link.Groups[1].Value.Split('#')[0];
                if (target.Length == 0 || Scheme().IsMatch(target))
                {
                    continue;
                }

                if (!Exists(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, Uri.UnescapeDataString(target)))))
                {
                    missing.Add($"{where}: link {link.Groups[1].Value}");
                }
            }

            foreach (System.Text.RegularExpressions.Match path in RepositoryPath().Matches(text))
            {
                var value = LineSuffix().Replace(path.Groups[1].Value.TrimEnd('.', ',', ':', ';'), string.Empty);
                if (ForeignProjects.Any(name => value.Split('/').ElementAtOrDefault(1)?.StartsWith(name, StringComparison.Ordinal) == true))
                {
                    continue;
                }

                if (!Exists(Path.Combine(root, value)))
                {
                    missing.Add($"{where}: path {value}");
                }
            }
        }

        Assert.True(missing.Count == 0, "The docs point at what is not there:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    [GeneratedRegex("```.*?```", RegexOptions.Singleline)]
    private static partial Regex Fence();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex("^[a-z]+:", RegexOptions.IgnoreCase)]
    private static partial Regex Scheme();

    [GeneratedRegex(@"`((?:src|tests|tools|samples|docs|data)/[^`\s*<>{}]+)`")]
    private static partial Regex RepositoryPath();

    [GeneratedRegex(@":\d+(-\d+)?$")]
    private static partial Regex LineSuffix();
}
