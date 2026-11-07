using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>
/// One contract run's folder: a <c>{report}/report.md</c> per module report, and one <c>summary.md</c> that
/// every report of the run rewrites. Several test processes of one night run share a folder by its name.
/// </summary>
public sealed class ContractRunFolder
{
    private readonly Func<IReadOnlyList<IContractModule>> _modules;
    private const string OutcomesFile = "outcomes.json";
    private static readonly Lock Gate = new();

    /// <param name="directory">The run folder; see <see cref="DirectoryFor"/>.</param>
    /// <param name="modules">The registered modules, read when the summary is written.</param>
    public ContractRunFolder(string directory, Func<IReadOnlyList<IContractModule>> modules)
    {
        Directory = directory;
        _modules = modules;
    }

    public string Directory { get; }

    /// <summary><c>{root}/TestResults/Contract/{run}</c>, where the run is named or else the start time.</summary>
    public static string DirectoryFor(string root, string? runName)
        => Path.Combine(root, "TestResults", "Contract", string.IsNullOrWhiteSpace(runName) ? DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) : runName);

    /// <summary>The nearest folder at or above <paramref name="start"/> that holds a solution file, or <paramref name="start"/>.</summary>
    public static string FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.sln").Length > 0 || directory.GetFiles("*.slnx").Length > 0)
                return directory.FullName;
        }

        return start;
    }

    /// <summary>
    /// Writes <c>{report}/report.md</c> and <c>{report}/outcomes.json</c>, then rewrites the run's <c>summary.md</c>
    /// from every report's outcomes in the folder, so separate test processes of one run share one summary.
    /// Writing a report again replaces its earlier outcomes. Returns the report path.
    /// </summary>
    public string Write(
        string report,
        string environment,
        IEnumerable<ContractResult> results,
        string? note = null,
        IReadOnlyList<UnregisteredEndpoint>? unregistered = null)
    {
        var list = results.ToList();
        var directory = Path.Combine(Directory, report);
        System.IO.Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "report.md");
        File.WriteAllText(path, ContractReport.Render(report, environment, list, note, unregistered));

        var module = ContractRunSummary.ModuleName(report);
        lock (Gate)
        {
            File.WriteAllText(Path.Combine(directory, OutcomesFile),
                JsonSerializer.Serialize(list.Select(result => ContractOutcome.From(module, result)).ToList()));
            File.WriteAllText(Path.Combine(Directory, "summary.md"),
                ContractRunSummary.Render(_modules().Select(registered => (registered.Name, registered.Contracts)).ToList(), ReadOutcomes()));
        }

        return path;
    }

    /// <summary>The outcomes of every report written to this run folder so far, by any process.</summary>
    public IReadOnlyList<ContractOutcome> ReadOutcomes()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];

        return System.IO.Directory.EnumerateFiles(Directory, OutcomesFile, SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .SelectMany(file => JsonSerializer.Deserialize<List<ContractOutcome>>(File.ReadAllText(file)) ?? [])
            .ToList();
    }
}

/// <summary>Renders one module report.</summary>
public static class ContractReport
{
    public static string Render(
        string module,
        string environment,
        IEnumerable<ContractResult> results,
        string? note = null,
        IReadOnlyList<UnregisteredEndpoint>? unregistered = null)
    {
        var list = results.ToList();
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"# {module} contract report");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Environment: {environment}. Run: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}.");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Passed {list.Count(result => result.Passed)} of {list.Count} endpoints; " +
            $"{list.Sum(result => result.Warnings.Count())} warning groups.");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.AppendLine(note);
            builder.AppendLine();
        }

        foreach (var result in list)
            builder.AppendLine(result.ToMarkdown());

        if (unregistered is not null)
            AppendUnregistered(builder, unregistered);

        return builder.ToString();
    }

    private static void AppendUnregistered(StringBuilder builder, IReadOnlyList<UnregisteredEndpoint> unregistered)
    {
        builder.AppendLine("## Endpoints without a contract");
        builder.AppendLine();
        if (unregistered.Count == 0)
        {
            builder.AppendLine("Every endpoint called during this run has a contract.");
            return;
        }

        builder.AppendLine("The app called these endpoints during this run, but no module registers a contract for them.");
        builder.AppendLine();
        builder.AppendLine("| Method | Host | Path | Calls |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var endpoint in unregistered)
            builder.AppendLine(CultureInfo.InvariantCulture, $"| {endpoint.Method} | {endpoint.Host} | `{endpoint.Path}` | {endpoint.Count} |");
    }
}
