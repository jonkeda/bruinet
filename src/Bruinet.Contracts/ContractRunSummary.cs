using System.Globalization;
using System.Text;

namespace Bruinet.Contracts;

/// <summary>One endpoint's outcome in one report: <c>Validated</c>, <c>Mismatch</c> or <c>Blocked</c>.</summary>
/// <param name="Module">The module the report belongs to.</param>
/// <param name="Endpoint">The endpoint, or the name of a check such as a round trip.</param>
/// <param name="Status">The outcome.</param>
public sealed record ContractOutcome(string Module, string Endpoint, string Status)
{
    public static ContractOutcome From(string module, ContractResult result)
        => new(module, result.Endpoint,
            result.Failures.Any() ? ContractRunSummary.Mismatch
            : result.Blocked.Any() ? ContractRunSummary.Blocked
            : ContractRunSummary.Validated);
}

/// <summary>
/// One summary per test run: every registered endpoint with read or write and Validated, Mismatch,
/// Blocked or Skipped. An endpoint no report of this run covered is Skipped, which is not a pass.
/// </summary>
public static class ContractRunSummary
{
    public const string Validated = "Validated";
    public const string Mismatch = "Mismatch";
    public const string Blocked = "Blocked";
    public const string Skipped = "Skipped";

    /// <summary>The module of a report name: <c>Contacts-Create</c> belongs to <c>Contacts</c>.</summary>
    public static string ModuleName(string reportName) => reportName.Split('-', 2)[0];

    public static string Render(
        IReadOnlyList<(string Name, IReadOnlyList<DtoContract> Contracts)> modules,
        IReadOnlyList<(string Module, ContractResult Result)> recorded)
        => Render(modules, recorded.Select(entry => ContractOutcome.From(entry.Module, entry.Result)).ToList());

    public static string Render(
        IReadOnlyList<(string Name, IReadOnlyList<DtoContract> Contracts)> modules,
        IReadOnlyList<ContractOutcome> outcomes)
    {
        var rows = modules
            .SelectMany(module => module.Contracts.Distinct().Select(contract => (
                Module: module.Name,
                contract.Endpoint,
                Kind: contract.Method == "GET" ? "read" : "write",
                Status: Worst(outcomes.Where(outcome => outcome.Module == module.Name && outcome.Endpoint == contract.Endpoint)))))
            .ToList();
        var registered = rows.Select(row => (row.Module, row.Endpoint)).ToHashSet();
        var other = outcomes.Where(outcome => !registered.Contains((outcome.Module, outcome.Endpoint))).ToList();

        var builder = new StringBuilder();
        builder.AppendLine("# Contract run summary");
        builder.AppendLine();
        var counts = string.Join(", ", rows.GroupBy(row => row.Status).OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} {group.Count()}"));
        builder.AppendLine(CultureInfo.InvariantCulture,
            $"Run: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}. {counts} of {rows.Count} registered endpoints. A skipped endpoint is not passed.");
        builder.AppendLine();
        builder.AppendLine("| Module | Endpoint | Kind | Status |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var row in rows)
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {row.Module} | `{row.Endpoint}` | {row.Kind} | {row.Status} |");

        if (other.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Other checks");
            builder.AppendLine();
            builder.AppendLine("Round trips and checks that are not one registered endpoint.");
            builder.AppendLine();
            builder.AppendLine("| Module | Check | Status |");
            builder.AppendLine("|---|---|---|");
            foreach (var group in other.GroupBy(outcome => (outcome.Module, outcome.Endpoint)))
                builder.AppendLine(CultureInfo.InvariantCulture, $"| {group.Key.Module} | {group.Key.Endpoint} | {Worst(group)} |");
        }

        return builder.ToString();
    }

    // The worst outcome wins when several reports of one run checked the same endpoint.
    private static string Worst(IEnumerable<ContractOutcome> outcomes)
    {
        var statuses = outcomes.Select(outcome => outcome.Status).ToList();
        if (statuses.Count == 0)
            return Skipped;
        if (statuses.Contains(Mismatch))
            return Mismatch;
        return statuses.Contains(Blocked) ? Blocked : Validated;
    }
}