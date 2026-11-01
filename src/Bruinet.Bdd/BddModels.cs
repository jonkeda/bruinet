using System.Globalization;

namespace Bruinet.Bdd;

public sealed record BddSourceLocation(string? FilePath, int LineNumber)
{
    public override string ToString()
    {
        return FilePath is null ? LineNumber.ToString(CultureInfo.InvariantCulture) : $"{FilePath}:{LineNumber}";
    }
}

public enum BddDiagnosticSeverity
{
    Error,
    Warning
}

public sealed record BddDiagnostic(
    BddDiagnosticSeverity Severity,
    string Code,
    string Message,
    BddSourceLocation Location)
{
    /// <summary>The value rewritten on the diagnostic's line, when there is exactly one right answer.</summary>
    public BddTextFix? Fix { get; init; }

    /// <summary>
    /// For a problem inside a callable phrase, reported at the call: the steps it passed through
    /// in the callables' bodies, outermost first. Empty otherwise.
    /// </summary>
    public IReadOnlyList<BddSourceLocation> Within { get; init; } = [];
}

public enum BddStepKeyword
{
    Given,
    When,
    Then,
    And,
    But
}

public sealed record BddDocument(
    string Title,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<BddStep> Background,
    IReadOnlyList<BddNamedDataTable> DataTables,
    IReadOnlyList<BddScenario> Scenarios,
    BddSourceLocation Source)
{
    /// <summary>The file's own callable phrases (<c>## Step:</c> sections).</summary>
    public IReadOnlyList<BddCallable> Callables { get; init; } = [];
}

public sealed record BddNamedDataTable(
    string Name,
    BddTable Table,
    BddSourceLocation Source);

public sealed record BddScenario(
    string Name,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BddStep> Steps,
    BddSourceLocation Source,
    string? OutlineName = null,
    int? ExampleIndex = null,
    string? ExamplesName = null);

public sealed record BddStep(
    BddStepKeyword Keyword,
    BddEffectiveStepKeyword EffectiveKeyword,
    string Text,
    BddTable? Table,
    BddSourceLocation Source,
    string? DocString = null)
{
    /// <summary>In a callable phrase's body: the call's table or doc string goes under this step.</summary>
    public BddPassedBlock Passes { get; init; }
}

public sealed record BddTable(
    IReadOnlyList<string> Columns,
    IReadOnlyList<BddTableRow> Rows,
    BddSourceLocation Source)
{
    public string GetValue(string column, int rowIndex = 0)
    {
        if (!TryGetValue(column, out var value, rowIndex))
        {
            throw new KeyNotFoundException($"Table does not contain column '{column}' at row {rowIndex}.");
        }

        return value;
    }

    public bool TryGetValue(string column, out string value, int rowIndex = 0)
    {
        value = string.Empty;

        if (rowIndex < 0 || rowIndex >= Rows.Count)
        {
            return false;
        }

        if (!Rows[rowIndex].Cells.TryGetValue(column, out var found))
        {
            return false;
        }

        value = found;
        return true;
    }
}

public sealed record BddTableRow(
    IReadOnlyDictionary<string, string> Cells,
    BddSourceLocation Source);

public sealed class BddParseResult
{
    public BddParseResult(BddDocument? document, IReadOnlyList<BddDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = diagnostics;
    }

    public BddDocument? Document { get; }

    public IReadOnlyList<BddDiagnostic> Diagnostics { get; }

    public bool Success => Document is not null && Diagnostics.All(x => x.Severity != BddDiagnosticSeverity.Error);
}

/// <summary>A value rewritten in place: the one right answer for a near miss (<c>2026-05-12</c> to <c>12-May-2026</c>).</summary>
/// <param name="Original">The value as written.</param>
/// <param name="Replacement">The value as it should be written.</param>
public sealed record BddTextFix(string Original, string Replacement);
