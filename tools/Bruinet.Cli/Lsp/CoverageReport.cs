using System.Globalization;
using System.Xml.Linq;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// A cobertura report read for the MCP server's <c>coverage</c>: each source file's lines, run or not, and
/// line numbers as the ranges an agent reads (<c>42-48, 51</c>).
/// </summary>
internal static class CoverageReport
{
    private static readonly string[] SourceExtensions = [".cs", ".razor", ".xaml", ".cshtml"];

    /// <summary>Each file's lines by number: whether the run ran it. A line in several classes counts when any ran it.</summary>
    /// <param name="path">The cobertura file.</param>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<int, bool>> Read(string path)
    {
        var root = XDocument.Load(path).Root!;
        var sources = root.Descendants("source").Select(source => source.Value).ToList();
        Dictionary<string, Dictionary<int, bool>> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (var type in root.Descendants("class"))
        {
            var name = (string?)type.Attribute("filename");
            if (name is null)
            {
                continue;
            }

            var file = Path.IsPathRooted(name) ? Path.GetFullPath(name) : Path.GetFullPath(Path.Combine(sources.FirstOrDefault() ?? string.Empty, name));
            if (!files.TryGetValue(file, out var lines))
            {
                files[file] = lines = [];
            }

            foreach (var line in type.Descendants("line"))
            {
                var number = int.Parse((string)line.Attribute("number")!, CultureInfo.InvariantCulture);
                var ran = long.Parse((string?)line.Attribute("hits") ?? "0", CultureInfo.InvariantCulture) > 0;
                lines[number] = lines.GetValueOrDefault(number) || ran;
            }
        }

        return files.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<int, bool>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Line numbers as ranges, <c>3-5, 9</c>; null when there are none.</summary>
    /// <param name="lines">The line numbers, in any order.</param>
    public static string? Ranges(IEnumerable<int> lines)
    {
        var sorted = lines.Distinct().Order().ToList();
        if (sorted.Count == 0)
        {
            return null;
        }

        List<string> ranges = [];
        var start = sorted[0];
        var end = start;
        foreach (var line in sorted.Skip(1))
        {
            if (line == end + 1)
            {
                end = line;
                continue;
            }

            ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
            start = end = line;
        }

        ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
        return string.Join(", ", ranges);
    }

    /// <summary>Whether a changed file is code a coverage report can name.</summary>
    /// <param name="path">The file.</param>
    public static bool IsSource(string path) => SourceExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
