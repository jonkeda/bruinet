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

    // R2-T91: a command added to the tree is a row in docs/run/cli.md, and a row names a command the tree has.
    [Fact]
    public void EveryCommand_HasARowInTheCommandLineGuide()
    {
        var documented = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "run", "cli.md"))
            .Select(line => Row().Match(line))
            .Where(match => match.Success)
            .SelectMany(match => Paths(match.Groups["command"].Value))
            .ToHashSet(StringComparer.Ordinal);
        var tree = Leaves(BruinetCommand.Build(new CliIo(TextReader.Null, TextWriter.Null, TextWriter.Null, RepositoryRoot(), Interactive: false)), []).ToList();

        Assert.Empty(tree.Except(documented));
        Assert.Empty(documented.Except(tree));
    }

    // The commands that run something: a group's own action only prints its help.
    private static IEnumerable<string> Leaves(System.CommandLine.Command command, List<string> path)
    {
        if (command.Subcommands.Count == 0)
        {
            yield return string.Join(' ', path);
            yield break;
        }

        foreach (var sub in command.Subcommands)
        {
            foreach (var leaf in Leaves(sub, [.. path, sub.Name]))
            {
                yield return leaf;
            }
        }
    }

    // A row's command words, up to its first argument or option; "config get\|set" is two commands.
    private static IEnumerable<string> Paths(string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeWhile(word => char.IsLetter(word[0])).ToList();
        return words[^1].Split("\\|").Select(last => string.Join(' ', words[..^1].Append(last)));
    }

    [GeneratedRegex(@"^\|\s*`bruinet (?<command>[^`]+)`")]
    private static partial Regex Row();

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
