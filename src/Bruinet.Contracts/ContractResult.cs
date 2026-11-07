using System.Globalization;
using System.Text;

namespace Bruinet.Contracts;

public enum ContractSeverity
{
    Failure,
    Warning,
    Blocked
}

/// <summary>
/// One contract finding. Array indexes are collapsed to <c>[*]</c> so one issue in 500 items
/// is reported once with a count and the first index where it occurred.
/// </summary>
public sealed record ContractIssue(ContractSeverity Severity, string Path, string Message, int Count, string FirstLocation);

public sealed class ContractResult
{
    private readonly Dictionary<(ContractSeverity, string, string), ContractIssue> _issues = new();

    public ContractResult(string endpoint)
    {
        Endpoint = endpoint;
    }

    public string Endpoint { get; }

    /// <summary>How many items, fields or values the check covered.</summary>
    public int ItemCount { get; set; }

    public IReadOnlyList<ContractIssue> Issues => _issues.Values.ToList();

    public IEnumerable<ContractIssue> Failures => Issues.Where(issue => issue.Severity == ContractSeverity.Failure);

    public IEnumerable<ContractIssue> Warnings => Issues.Where(issue => issue.Severity == ContractSeverity.Warning);

    public IEnumerable<ContractIssue> Blocked => Issues.Where(issue => issue.Severity == ContractSeverity.Blocked);

    public bool Passed => !Failures.Any() && !Blocked.Any();

    public void Add(ContractSeverity severity, string location, string message)
    {
        var pattern = CollapseIndexes(location);
        var key = (severity, pattern, message);
        _issues[key] = _issues.TryGetValue(key, out var existing)
            ? existing with { Count = existing.Count + 1 }
            : new ContractIssue(severity, pattern, message, 1, location);
    }

    public string ToMarkdown()
    {
        var builder = new StringBuilder();
        var status = Blocked.Any() ? "BLOCKED" : Failures.Any() ? "FAILED" : "PASSED";
        builder.AppendLine(CultureInfo.InvariantCulture, $"### {Endpoint}: {status}");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Items checked: {ItemCount}");
        builder.AppendLine();
        if (_issues.Count == 0)
            return builder.ToString();

        builder.AppendLine("| Severity | Path | Issue | Count | First at |");
        builder.AppendLine("|---|---|---|---|---|");
        foreach (var issue in Issues.OrderBy(issue => issue.Severity).ThenBy(issue => issue.Path, StringComparer.Ordinal))
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {issue.Severity} | `{issue.Path}` | {issue.Message} | {issue.Count} | `{issue.FirstLocation}` |");

        return builder.ToString();
    }

    public override string ToString() => ToMarkdown();

    internal static string CollapseIndexes(string location)
        => System.Text.RegularExpressions.Regex.Replace(location, @"\[\d+\]", "[*]");
}
