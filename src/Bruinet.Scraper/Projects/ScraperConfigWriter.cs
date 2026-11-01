using Bruinet.Core.Utilities;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// One change to a <c>scraper.config.md</c>: a field set, a row added or a row removed. Everything else
/// in the file (prose, other sections, line endings) stays as it was, so a person's file is not
/// rewritten by the view.
/// </summary>
public static class ScraperConfigWriter
{
    /// <summary>Sets a field of a <c>| Field | Value |</c> section (<c>## App</c>, <c>## Explore</c>), adding the row or the section when missing.</summary>
    public static ConfigEdit SetField(string markdown, string section, string field, string value)
    {
        Check(value);
        var doc = new Doc(markdown);
        var table = doc.Table(section);
        if (table is null)
        {
            doc.AppendSection(section, ["Field", "Value"], [[field, value]]);
            return new(doc.Text, true, $"Added ## {section} with {field} = {value}.");
        }
        var row = table.Rows.FirstOrDefault(r => r.Cells.GetValueOrDefault("Field", "") == field);
        if (row is not null)
        {
            if (row.Cells.GetValueOrDefault("Value", "") == value)
                return new(markdown, false, $"{field} is {value} already.");
            doc.Replace(row.LineNumber, Row([field, value]));
            return new(doc.Text, true, $"Set {field} to {value} in ## {section}.");
        }
        doc.Insert(table.EndLineNumber, Row([field, value]));
        return new(doc.Text, true, $"Added {field} = {value} to ## {section}.");
    }

    /// <summary>Adds a row to a section's table (<c>## Aliases</c>, <c>## Explore hosts</c>, <c>## Never click</c>…), unless a row with the same first cell is there; adds the section when missing.</summary>
    public static ConfigEdit AddRow(string markdown, string section, IReadOnlyList<string> columns, IReadOnlyList<string> values)
    {
        if (columns.Count != values.Count || columns.Count == 0)
            throw new ArgumentException("One value per column.", nameof(values));
        foreach (var value in values)
            Check(value);
        var doc = new Doc(markdown);
        var table = doc.Table(section);
        if (table is null)
        {
            doc.AppendSection(section, columns, [values]);
            return new(doc.Text, true, $"Added ## {section} with {string.Join(", ", values)}.");
        }
        if (table.Rows.Any(r => r.Cells.GetValueOrDefault(table.Columns[0], "") == values[0]))
            return new(markdown, false, $"{values[0]} is in ## {section} already.");
        var ordered = table.Columns.Select(c => columns.Contains(c) ? values[columns.ToList().IndexOf(c)] : "").ToArray();
        doc.Insert(table.EndLineNumber, Row(ordered));
        return new(doc.Text, true, $"Added {values[0]} to ## {section}.");
    }

    /// <summary>Removes the rows of a section's table whose first cell is <paramref name="first"/>.</summary>
    public static ConfigEdit RemoveRow(string markdown, string section, string first)
    {
        var doc = new Doc(markdown);
        var table = doc.Table(section);
        var rows = table?.Rows.Where(r => r.Cells.GetValueOrDefault(table.Columns[0], "") == first).ToArray() ?? [];
        if (rows.Length == 0)
            return new(markdown, false, $"{first} is not in ## {section}.");
        foreach (var row in rows.OrderByDescending(r => r.LineNumber))
            doc.Remove(row.LineNumber);
        return new(doc.Text, true, $"Removed {first} from ## {section}.");
    }

    /// <summary>The columns of each section that holds rows; a <c>| Field | Value |</c> section is set with <see cref="SetField"/>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RowSections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["Aliases"] = ["Url"],
        ["Danger words"] = ["Category", "On"],
        ["Explore hosts"] = ["Host"],
        ["Never click"] = ["Match"],
        ["Always click"] = ["Match"],
        ["Never visit"] = ["Url"],
        ["Allow requests"] = ["Method", "Url"],
        ["Components"] = ["Match", "Kind", "Name"],
    };

    /// <summary>
    /// Makes one change: <c>set</c> a field (values: field, value; for <c>Danger words</c>: category, true or
    /// false), <c>add</c> a row (values: its cells, in the section's columns) or <c>remove</c> one (values: its first cell).
    /// </summary>
    public static ConfigEdit Apply(string markdown, ConfigChange change)
    {
        var values = change.Values;
        switch (change.Action)
        {
            case "set" when values.Count == 2 && change.Section == "Danger words":
                // One row per category: the old one goes and the new one comes.
                var removed = RemoveRow(markdown, change.Section, values[0]);
                var added = AddRow(removed.Text, change.Section, RowSections[change.Section], values);
                return new(added.Text, added.Text != markdown, $"Set {values[0]} to {values[1]} in ## Danger words.");
            case "set" when values.Count == 2:
                return SetField(markdown, change.Section, values[0], values[1]);
            case "add" when RowSections.TryGetValue(change.Section, out var columns) && values.Count == columns.Count:
                return AddRow(markdown, change.Section, columns, values);
            case "remove" when values.Count == 1:
                return RemoveRow(markdown, change.Section, values[0]);
            default:
                throw new ArgumentException(
                    $"'{change.Action}' in ## {change.Section} with {values.Count} value(s) is not a change: set takes field and value, add takes {(RowSections.TryGetValue(change.Section, out var c) ? string.Join(" and ", c) : "a section's cells")}, remove takes the first cell.");
        }
    }

    /// <summary>A new config for a project: <c>## App</c> with the start URL, and the rest left to the defaults.</summary>
    public static string New(string startUrl, string platform = "html", TestTarget target = TestTarget.Xunit) =>
        "# Scraper Config\n\n## App\n\n| Field | Value |\n| --- | --- |\n" + Row(["Platform", platform]) + "\n" + Row(["StartUrl", startUrl]) + "\n" + Tests(target);

    /// <summary>The <c>## Tests</c> section a new config gets: its Target, visible so it is changed in one place.</summary>
    public static string Tests(TestTarget target) =>
        "\n## Tests\n\n| Field | Value |\n| --- | --- |\n" + Row(["Target", TestSettings.Name(target)]) + "\n";

    private static string Row(IReadOnlyList<string> cells) => "| " + string.Join(" | ", cells) + " |";

    private static void Check(string value)
    {
        if (value.Contains('|', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            throw new ArgumentException($"'{value}' cannot go in a table cell: no '|' and no line break.");
    }

    // The file as lines, with its own line ending; line numbers are 1-based as MarkdownTables gives them.
    private sealed class Doc
    {
        private readonly List<string> _lines;
        private readonly string _newline;
        private readonly bool _endsWithNewline;

        public Doc(string markdown)
        {
            _newline = markdown.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
            _endsWithNewline = text.EndsWith('\n');
            _lines = [.. (_endsWithNewline ? text[..^1] : text).Split('\n')];
        }

        public string Text => string.Join(_newline, _lines) + (_endsWithNewline ? _newline : "");

        public MarkdownTable? Table(string section) =>
            MarkdownTables.ReadSections(string.Join('\n', _lines)).TryGetValue(section, out var table) ? table : null;

        public void Replace(int line, string text) => _lines[line - 1] = text;

        public void Insert(int line, string text) => _lines.Insert(line - 1, text);

        public void Remove(int line) => _lines.RemoveAt(line - 1);

        public void AppendSection(string section, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            while (_lines.Count > 0 && _lines[^1].Trim().Length == 0)
                _lines.RemoveAt(_lines.Count - 1);
            if (_lines.Count > 0)
                _lines.Add("");
            _lines.Add("## " + section);
            _lines.Add("");
            _lines.Add(Row(columns));
            _lines.Add(Row(columns.Select(_ => "---").ToArray()));
            _lines.AddRange(rows.Select(Row));
        }
    }
}

/// <summary>One change to a config: <c>set</c>, <c>add</c> or <c>remove</c>, in a section, with its values.</summary>
public sealed record ConfigChange(string Action, string Section, IReadOnlyList<string> Values);

/// <summary>What a change did: the new text, whether anything changed, and a sentence saying what.</summary>
public sealed record ConfigEdit(string Text, bool Changed, string Description);
