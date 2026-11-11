using System.Text.RegularExpressions;

namespace Bruinet.Cli.Tests;

/// <summary>
/// Every <c>bruinet …</c> line in README.md and docs/ parses against the command tree (review 06, proposal 3): a
/// renamed command or option fails here, not in a reader's terminal.
/// </summary>
public sealed partial class DocsCommandLineTests
{
    [Fact]
    public void EveryBruinetLineInTheDocs_Parses()
    {
        var root = RepositoryRoot();
        var files = new[] { Path.Combine(root, "README.md") }
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories));

        var failures = new List<string>();
        var checkedLines = 0;
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Command().Match(lines[i]) is not { Success: true } match)
                {
                    continue;
                }

                var args = Arguments(match.Groups["args"].Value);
                var parse = BruinetCommand.Build(new CliIo(TextReader.Null, TextWriter.Null, TextWriter.Null, root, Interactive: false)).Parse(args);
                checkedLines++;
                if (parse.Errors.Count > 0)
                {
                    failures.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {lines[i].Trim()} -> {string.Join("; ", parse.Errors.Select(e => e.Message))}");
                }
            }
        }

        Assert.True(checkedLines > 20, $"Only {checkedLines} bruinet lines found: is the pattern still right?");
        Assert.True(failures.Count == 0, "These documented command lines do not parse:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    // A command line as the docs write it: the shell comment, the [optional] parts and the <placeholders> made plain.
    private static string[] Arguments(string line)
    {
        var text = Comment().Replace(line, string.Empty);
        text = Optional().Replace(text, string.Empty);
        text = Redirect().Replace(text, string.Empty);
        text = Placeholder().Replace(text, "x");
        return Token().Matches(text).Select(m => m.Value.Trim('"')).Where(t => t.Length > 0).ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bruinet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Bruinet.sln not found above the test's folder.");
    }

    [GeneratedRegex(@"^\s*(?:\$\s*)?bruinet\s+(?<args>[a-z].*)$")]
    private static partial Regex Command();

    [GeneratedRegex(@"\s+#.*$")]
    private static partial Regex Comment();

    // A shell's redirect or pipe, and what follows it, is not the command's.
    [GeneratedRegex(@"\s+(?:>|\||2>).*$")]
    private static partial Regex Redirect();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex Optional();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Placeholder();

    [GeneratedRegex("\"[^\"]*\"|\\S+")]
    private static partial Regex Token();
}
