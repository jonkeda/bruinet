using System.Text;
using System.Text.RegularExpressions;
using Bruinet.Core.Utilities;

namespace Bruinet.Cli.Upgrade;

/// <summary>A renamed name of Bruinet's, as a row of the changes list's <c>## Renames</c> table.</summary>
/// <param name="Commit">The Bruinet change that made it.</param>
/// <param name="Kind"><c>package</c>, <c>namespace</c>, <c>type</c>, or <c>keep</c> for a longer name that a shorter row must leave alone.</param>
/// <param name="Old">The name consumers have.</param>
/// <param name="New">The name to write instead; the old one for a <c>keep</c>.</param>
internal sealed record Rename(string Commit, string Kind, string Old, string New);

/// <summary>What one rename changes in one file.</summary>
/// <param name="File">The file, relative to the folder upgraded.</param>
/// <param name="Old">The old name.</param>
/// <param name="New">The new name.</param>
/// <param name="Count">How often.</param>
internal sealed record RenameHit(string File, string Old, string New, int Count);

/// <summary>
/// The renames a project using Bruinet follows, read from the changes list
/// (<c>.github/skills/api-upgrade/references/changes.md</c>, <c>## Renames</c>) and applied to a folder's files: a name
/// is replaced where it stands as a whole dotted name, so <c>Bruinet.Mocking</c> does not touch
/// <c>Bruinet.Mocking.Data</c> when a <c>keep</c> row names that one, and does take <c>Bruinet.Mocking.Steps</c> with it.
/// </summary>
internal static class Renames
{
    /// <summary>The section of the changes list a program reads.</summary>
    public const string Section = "Renames";

    // What holds a Bruinet name in a consumer: code, projects, and the names a suite keeps as text.
    private static readonly string[] Extensions =
        [".cs", ".csproj", ".props", ".targets", ".sln", ".slnx", ".md", ".json", ".feature", ".steps", ".config", ".yml", ".yaml", ".ps1", ".razor", ".xaml"];

    private static readonly string[] TypeExtensions = [".cs", ".razor", ".xaml", ".md"];

    private static readonly string[] SkippedFolders = ["bin", "obj", ".git", ".vs", "node_modules", "TestResults", "packages"];

    // The lists that say what was renamed keep the old names.
    private static readonly string[] SkippedFiles = ["changes.md", "CHANGELOG.md"];

    /// <summary>The rename rows of a changes list, as written (newest first).</summary>
    /// <param name="markdown">The changes list's text.</param>
    public static IReadOnlyList<Rename> Read(string markdown)
    {
        if (!MarkdownTables.ReadSections(markdown).TryGetValue(Section, out var table))
        {
            return [];
        }

        static string Cell(MarkdownTableRow row, string column) => row.Cells.TryGetValue(column, out var value) ? value.Trim().Trim('`') : string.Empty;
        return [.. table.Rows
            .Select(row => new Rename(Cell(row, "Commit"), Cell(row, "Kind").ToLowerInvariant(), Cell(row, "Old"), Cell(row, "New")))
            .Where(row => row.Old.Length > 0 && row.Kind is "package" or "namespace" or "type" or "keep")
            .Select(row => row.Kind == "keep" ? row with { New = row.Old } : row)];
    }

    /// <summary>The rows made after a change: those written above the first row of <paramref name="from"/>. All when null.</summary>
    /// <param name="rows">The rows, newest first.</param>
    /// <param name="from">The change the consumer already follows.</param>
    /// <exception cref="ArgumentException">No row names that change.</exception>
    public static IReadOnlyList<Rename> Since(IReadOnlyList<Rename> rows, string? from)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            return rows;
        }

        var at = rows.Select((row, index) => (row, index)).FirstOrDefault(each => string.Equals(each.row.Commit, from, StringComparison.OrdinalIgnoreCase), (null!, -1)).Item2;
        return at < 0
            ? throw new ArgumentException($"No rename row is of the change '{from}'. The changes are: {string.Join(", ", rows.Select(row => row.Commit).Distinct())}.")
            : [.. rows.Take(at)];
    }

    /// <summary>The files of a folder a rename may touch: not a build's output, and not a repository nested in it.</summary>
    /// <param name="folder">The folder.</param>
    public static IEnumerable<string> Files(string folder)
    {
        var pending = new Stack<string>([folder]);
        while (pending.TryPop(out var current))
        {
            foreach (var file in Directory.EnumerateFiles(current))
            {
                if (Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase) && !SkippedFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }

            foreach (var sub in Directory.EnumerateDirectories(current))
            {
                // A folder with its own .git is another repository (Bruinet as a submodule): it is not the consumer's code.
                if (!SkippedFolders.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase) && !Path.Exists(Path.Combine(sub, ".git")))
                {
                    pending.Push(sub);
                }
            }
        }
    }

    /// <summary>A file's text with the renames applied, and how often each was.</summary>
    /// <param name="text">The text.</param>
    /// <param name="extension">The file's extension: a type's rename is applied in code only.</param>
    /// <param name="rows">The rows.</param>
    public static (string Text, IReadOnlyList<(Rename Row, int Count)> Hits) Apply(string text, string extension, IReadOnlyList<Rename> rows)
    {
        var counts = new Dictionary<Rename, int>();
        var dotted = rows.Where(row => row.Kind != "type").OrderByDescending(row => row.Old.Length).ToList();
        if (dotted.Count > 0)
        {
            // Longest first, so a longer name's own row (or its keep) wins over a shorter one's.
            var pattern = new Regex(@"(?<![\w.])(?:" + string.Join('|', dotted.Select(row => Regex.Escape(row.Old)).Distinct()) + @")(?!\w)", RegexOptions.CultureInvariant);
            text = pattern.Replace(text, match =>
            {
                var row = dotted.First(each => each.Old == match.Value);
                if (row.Kind != "keep" && row.New != row.Old)
                {
                    counts[row] = counts.GetValueOrDefault(row) + 1;
                }

                return row.New;
            });
        }

        if (TypeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var row in rows.Where(row => row.Kind == "type"))
            {
                var count = 0;
                text = Regex.Replace(text, @"(?<![\w])" + Regex.Escape(row.Old) + @"(?!\w)", _ =>
                {
                    count++;
                    return row.New;
                }, RegexOptions.CultureInvariant);
                if (count > 0)
                {
                    counts[row] = count;
                }
            }
        }

        return (text, [.. counts.Select(each => (each.Key, each.Value))]);
    }

    /// <summary>Applies the renames to every file of a folder, or only says what they would change.</summary>
    /// <param name="folder">The consumer's folder.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="write">Whether the files are written.</param>
    public static IReadOnlyList<RenameHit> Run(string folder, IReadOnlyList<Rename> rows, bool write)
    {
        var found = new List<RenameHit>();
        foreach (var file in Files(folder).Order(StringComparer.Ordinal))
        {
            var (text, encoding) = ReadKeepingEncoding(file);
            var (changed, hits) = Apply(text, Path.GetExtension(file), rows);
            if (hits.Count == 0)
            {
                continue;
            }

            var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            found.AddRange(hits.Select(hit => new RenameHit(relative, hit.Row.Old, hit.Row.New, hit.Count)));
            if (write)
            {
                File.WriteAllText(file, changed, encoding);
            }
        }

        return found;
    }

    // A file is written back as it was read: with its byte order mark when it had one.
    private static (string Text, Encoding Encoding) ReadKeepingEncoding(string file)
    {
        var bytes = File.ReadAllBytes(file);
        var marked = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return (new UTF8Encoding(false).GetString(bytes, marked ? 3 : 0, bytes.Length - (marked ? 3 : 0)), new UTF8Encoding(marked));
    }
}
