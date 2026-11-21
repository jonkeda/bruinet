using System.CommandLine;
using System.Diagnostics;
using System.Reflection;

namespace Bruinet.Cli.Upgrade;

/// <summary>
/// <c>bruinet upgrade</c>: moves a project that uses Bruinet to the names this Bruinet has. It applies the rename rows
/// of the changes list (skill api-upgrade) to the folder's code, projects and the names a suite holds as text, then
/// builds and gives the errors left: those no rename explains, for the list's other rows and a person.
/// </summary>
internal static class UpgradeCommand
{
    /// <summary>The changes list, as this tool was built with it.</summary>
    public const string Resource = "Bruinet.Cli.changes.md";

    public static Command Build(CliIo io)
    {
        var folder = new Argument<string>("folder") { Description = "The folder of the project that uses Bruinet." };
        var from = new Option<string?>("--from") { Description = "The change the project already follows (a row's Commit): only the renames made after it are applied. Every rename when left out." };
        var check = new Option<bool>("--check") { Description = "Say what would change and change nothing. Exit 1 when anything would." };
        var noBuild = new Option<bool>("--no-build") { Description = "Do not build after the renames." };
        var changes = new Option<string?>("--changes") { Description = "The changes list to read; the one this tool was built with when left out." };
        var json = CommonOptions.Json();
        var command = new Command("upgrade", "Apply Bruinet's renames (packages, namespaces, types) to a project that uses it, then build and say what no rename explains.") { folder, from, check, noBuild, changes, json };
        command.Action(io, (parsed, cancellationToken) =>
        {
            var target = Path.GetFullPath(parsed.GetValue(folder)!, io.CurrentDirectory);
            var result = Run(target, parsed.GetValue(changes) is { } given ? File.ReadAllText(Path.GetFullPath(given, io.CurrentDirectory)) : ChangesList(), parsed.GetValue(from), parsed.GetValue(check), build: !parsed.GetValue(check) && !parsed.GetValue(noBuild));
            if (parsed.GetValue(json))
            {
                io.Out.WriteLine(CommonOptions.ToJson(result));
            }
            else
            {
                foreach (var line in result.Lines())
                {
                    io.Out.WriteLine(line);
                }
            }

            io.Error.WriteLine(result.Summary());
            return Task.FromResult(result.Exit);
        });
        return command;
    }

    /// <summary>Applies the renames to a folder; the answer of the command and of the MCP tool.</summary>
    /// <param name="folder">The consumer's folder.</param>
    /// <param name="changesList">The changes list's text.</param>
    /// <param name="from">The change the consumer already follows, or null.</param>
    /// <param name="check">Change nothing.</param>
    /// <param name="build">Build afterwards.</param>
    public static UpgradeResult Run(string folder, string changesList, string? from, bool check, bool build)
    {
        if (!Directory.Exists(folder))
        {
            throw new DirectoryNotFoundException($"There is no folder {folder}.");
        }

        var rows = Renames.Since(Renames.Read(changesList), from);
        var hits = Renames.Run(folder, rows, write: !check);
        var errors = build ? BuildErrors(folder) : null;
        return new UpgradeResult(check, rows.Count(row => row.Kind != "keep"), hits, errors);
    }

    /// <summary>The changes list this tool was built with.</summary>
    public static string ChangesList()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException("This bruinet was built without its changes list: pass --changes <path>.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // The build's errors, each once: what the renames did not explain. Null when the folder has nothing to build.
    private static IReadOnlyList<string>? BuildErrors(string folder)
    {
        var solutions = Directory.GetFiles(folder, "*.slnx").Concat(Directory.GetFiles(folder, "*.sln")).ToList();
        var projects = solutions.Count == 0 ? Directory.GetFiles(folder, "*.csproj") : [];
        if (solutions.Count + projects.Length != 1)
        {
            return solutions.Count + projects.Length == 0
                ? null
                : [$"{folder} has more than one solution or project: build the one you mean, then read its errors."];
        }

        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "build", solutions.Concat(projects).Single(), "-v:quiet", "/nr:false", "-nologo" })
        {
            start.ArgumentList.Add(argument);
        }

        using var dotnet = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        var output = dotnet.StandardOutput.ReadToEndAsync();
        dotnet.StandardError.ReadToEnd();
        dotnet.WaitForExit();
        return [.. output.Result.Split('\n')
            .Where(line => line.Contains(": error ", StringComparison.Ordinal))
            .Select(line => line.Trim())
            // The project a line ends with repeats per target framework.
            .Select(line => line.LastIndexOf(" [", StringComparison.Ordinal) is var at and > 0 ? line[..at] : line)
            .Distinct(StringComparer.Ordinal)];
    }
}

/// <summary>What <c>bruinet upgrade</c> did.</summary>
/// <param name="Checked">Whether nothing was written.</param>
/// <param name="Rows">How many rename rows were applied.</param>
/// <param name="Changes">What each rename changed, per file.</param>
/// <param name="BuildErrors">The build's errors afterwards; null when nothing was built.</param>
internal sealed record UpgradeResult(bool Checked, int Rows, IReadOnlyList<RenameHit> Changes, IReadOnlyList<string>? BuildErrors)
{
    /// <summary>0 nothing left to do, 1 something found (a check that would change, or a build with errors).</summary>
    public int Exit => (Checked && Changes.Count > 0) || BuildErrors is { Count: > 0 } ? 1 : 0;

    public IEnumerable<string> Lines()
    {
        foreach (var change in Changes)
        {
            yield return $"{change.File}: {change.Old} -> {change.New}{(change.Count > 1 ? $" ({change.Count})" : string.Empty)}";
        }

        foreach (var error in BuildErrors ?? [])
        {
            yield return "no rename explains: " + error;
        }
    }

    public string Summary()
    {
        var files = Changes.Select(change => change.File).Distinct().Count();
        var renamed = $"{Rows} renames, {Changes.Sum(change => change.Count)} names in {files} files {(Checked ? "would change" : "changed")}";
        return BuildErrors switch
        {
            null => renamed,
            { Count: 0 } => renamed + "; the build has no errors",
            _ => renamed + $"; the build has {BuildErrors.Count} errors no rename explains: read the other rows of the changes list (skill api-upgrade)",
        };
    }
}
