namespace Bruinet.Scraper.Factory;

/// <summary>
/// Moves a control's signs from an app's own config up to a control library's file of signs: what was learned on one
/// app and holds for the library is the library's, so the next app that uses it starts with it.
/// </summary>
public static class SignMove
{
    /// <summary>The section of an app's config that holds its own control signs.</summary>
    public const string ConfigSection = "## Control signs";

    /// <summary>The section of a library's file that holds its signs.</summary>
    public const string LibrarySection = "## Signs";

    /// <summary>Moves every row of a control out of the config's table and into the library's, before the rows it has.</summary>
    /// <param name="config">The app's <c>scraper.config.md</c>.</param>
    /// <param name="library">The library's <c>control-signs.md</c>.</param>
    /// <param name="control">The control whose rows move.</param>
    /// <returns>Both texts after the move, and the rows that moved as the library writes them.</returns>
    /// <exception cref="FormatException">The config has no row for the control, or one of the two has no table to move between.</exception>
    public static (string Config, string Library, IReadOnlyList<string> Moved) Move(string config, string library, string control)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(control);
        var newline = config.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var from = config.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var (start, end) = Table(from, ConfigSection) ?? throw new FormatException($"The config has no table under '{ConfigSection}'.");
        var rows = Enumerable.Range(start, end - start).Where(at => Cells(from[at]) is { Length: >= 2 } cells && string.Equals(cells[0], control, StringComparison.Ordinal)).ToList();
        if (rows.Count == 0)
            throw new FormatException($"The config has no sign for '{control}' under '{ConfigSection}'.");
        // A library writes a control and a sign as code.
        var moved = rows.Select(at => "| " + string.Join(" | ", Cells(from[at])!.Select((cell, column) => column < 2 ? $"`{cell}`" : cell)) + " |").ToList();
        foreach (var at in rows.OrderDescending())
            from.RemoveAt(at);

        var libraryNewline = library.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var to = library.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var (first, _) = Table(to, LibrarySection) ?? throw new FormatException($"The library's file has no table under '{LibrarySection}'.");
        // First in the table: the first row that fits decides, and what an app found is the more particular.
        to.InsertRange(first, moved);
        return (string.Join(newline, from), string.Join(libraryNewline, to), moved);
    }

    // The rows of the first table under a heading: from the line after its separator to the line after its last row.
    private static (int Start, int End)? Table(List<string> lines, string heading)
    {
        var at = lines.FindIndex(line => line.Trim() == heading);
        if (at < 0)
            return null;
        var separator = lines.FindIndex(at + 1, line => line.TrimStart().StartsWith("| ---", StringComparison.Ordinal) || line.TrimStart().StartsWith("|---", StringComparison.Ordinal));
        var next = lines.FindIndex(at + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        if (separator < 0 || (next >= 0 && separator > next))
            return null;
        var end = separator + 1;
        while (end < lines.Count && lines[end].TrimStart().StartsWith('|'))
            end++;
        return (separator + 1, end);
    }

    private static string[]? Cells(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.EndsWith('|') ? [.. trimmed[1..^1].Split('|').Select(cell => cell.Trim().Trim('`'))] : null;
    }
}
