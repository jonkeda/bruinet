using System.Text.RegularExpressions;

namespace Bruinet.Core.Tests;

/// <summary>
/// AD-009: an error is never taken for absence. A bare <c>catch</c>, or <c>catch (Exception)</c> with no
/// <c>when</c> filter, takes every fault with it, so no file may hold more of them than
/// <c>broad-catches.txt</c> allows; a file that holds fewer has its allowance lowered (review 05, R2-T18).
/// </summary>
public sealed partial class BroadCatchTests
{
    [GeneratedRegex(@"^\s*catch\s*(?<type>\(\s*(System\.)?Exception(\s+\w+)?\s*\))?\s*(?<when>when\b.*)?$")]
    private static partial Regex BroadCatch();

    [Fact]
    public void NoFile_HoldsMoreBroadCatches_ThanItsAllowance()
    {
        var root = SolutionTests.FindRepositoryRoot();
        Assert.NotNull(root);
        var allowed = Allowances(Path.Combine(root, "tests", "Bruinet.Core.Tests", "broad-catches.txt"));
        var found = Found(root);

        var over = found
            .Where(file => file.Value.Count > allowed.GetValueOrDefault(file.Key))
            .Select(file => $"{file.Key}: {file.Value.Count} broad catches, {allowed.GetValueOrDefault(file.Key)} allowed (lines {string.Join(", ", file.Value)})")
            .ToList();
        var under = allowed
            .Where(file => file.Value > (found.TryGetValue(file.Key, out var lines) ? lines.Count : 0))
            .Select(file => $"{file.Key}: allowed {file.Value}, holds {(found.TryGetValue(file.Key, out var lines) ? lines.Count : 0)}")
            .ToList();

        Assert.True(over.Count == 0, "Catch the exception you mean, or filter it with `when`; an error is not an answer (AD-009):\n" + string.Join("\n", over));
        Assert.True(under.Count == 0, "Fewer broad catches than allowed: lower these lines in tests/Bruinet.Core.Tests/broad-catches.txt:\n" + string.Join("\n", under));
    }

    private static Dictionary<string, int> Allowances(string path)
        => File.ReadLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(' ', 2))
            .ToDictionary(parts => parts[1].Trim(), parts => int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);

    // Each file under src and tools with a broad catch: its lines.
    private static Dictionary<string, List<int>> Found(string root)
    {
        var found = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var folder in new[] { "src", "tools" })
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/node_modules/", StringComparison.Ordinal))
                {
                    continue;
                }

                var number = 0;
                foreach (var line in File.ReadLines(file))
                {
                    number++;
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var match = BroadCatch().Match(line);
                    if (match.Success && (!match.Groups["type"].Success || !match.Groups["when"].Success))
                    {
                        if (!found.TryGetValue(relative, out var lines))
                        {
                            found[relative] = lines = [];
                        }

                        lines.Add(number);
                    }
                }
            }
        }

        return found;
    }
}
