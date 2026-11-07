using System.Globalization;
using System.Text;

namespace Bruinet.Contracts;

/// <summary>
/// One summary per test run: every registered endpoint with read or write and Validated, Mismatch,
/// Blocked or Skipped. An endpoint no report of this run covered is Skipped, which is not a pass.
/// </summary>
public sealed class ContractRunSummary
{
    private readonly Lock _gate = new();
    private readonly List<(string Module, ContractResult Result)> _recorded = [];

    /// <summary>Records one module report's results and returns the summary of everything recorded so far.</summary>
    public string Record(string reportName, IEnumerable<ContractResult> results, IReadOnlyList<IContractModule> modules)
    {
        lock (_gate)
        {
            var module = ModuleName(reportName);
            _recorded.AddRange(results.Select(result => (module, result)));
            return Render(modules.Select(registered => (registered.Name, registered.Contracts)).ToList(), _recorded);
        }
    }

    /// <summary>The module of a report name: <c>Contacts-Create</c> belongs to <c>Contacts</c>.</summary>
    public static string ModuleName(string reportName) => reportName.Split('-', 2)[0];

    public static string Render(
        IReadOnlyList<(string Name, IReadOnlyList<DtoContract> Contracts)> modules,
        IReadOnlyList<(string Module, ContractResult Result)> recorded)
    {
        var rows = modules
            .SelectMany(module => module.Contracts.Distinct().Select(contract => (
                Module: module.Name,
                contract.Endpoint,
                Kind: contract.Method == "GET" ? "read" : "write",
                Status: Status(recorded.Where(entry => entry.Module == module.Name && entry.Result.Endpoint == contract.Endpoint)
                    .Select(entry => entry.Result)))))
            .ToList();
        var registered = rows.Select(row => (row.Module, row.Endpoint)).ToHashSet();
        var other = recorded.Where(entry => !registered.Contains((entry.Module, entry.Result.Endpoint))).ToList();

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
            foreach (var group in other.GroupBy(entry => (entry.Module, entry.Result.Endpoint)))
                builder.AppendLine(CultureInfo.InvariantCulture, $"| {group.Key.Module} | {group.Key.Endpoint} | {Status(group.Select(entry => entry.Result))} |");
        }

        return builder.ToString();
    }

    // The worst outcome wins when several reports of one run checked the same endpoint.
    private static string Status(IEnumerable<ContractResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0)
            return "Skipped";
        if (list.Any(result => result.Failures.Any()))
            return "Mismatch";
        return list.Any(result => result.Blocked.Any()) ? "Blocked" : "Validated";
    }
}
