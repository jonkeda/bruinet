using System.CommandLine;
using System.Text.Json;

namespace Bruinet.Scraper.Host;

/// <summary>The options every command spells the same way, and what goes with them.</summary>
public static class HostOptions
{
    private static readonly JsonSerializerOptions WebIndented = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary><c>--solution</c>.</summary>
    public static Option<string?> Solution() =>
        new("--solution") { Description = "The folder projects are found under. Default: the nearest folder up with a .sln or .slnx, else this one." };

    /// <summary><c>--project</c>.</summary>
    public static Option<string?> Project() =>
        new("--project", "-p") { Description = "The project: its name or folder. Default: the one this folder is in, else the only one." };

    /// <summary><c>--json</c>.</summary>
    public static Option<bool> Json() =>
        new("--json") { Description = "The result as one JSON document on stdout, for scripts and models." };

    /// <summary><c>--yes</c>.</summary>
    public static Option<bool> Yes() =>
        new("--yes", "-y") { Description = "Answer yes to the question before it starts. Needed when no one is at a terminal." };

    /// <summary><c>--headless</c>.</summary>
    public static Option<bool> Headless() =>
        new("--headless") { Description = "No browser window. Default when no one is at a terminal." };

    /// <summary>The solution's folder: as given, else the nearest folder up with a solution file, else the current one.</summary>
    public static string SolutionFolder(string? given, string currentDirectory)
    {
        if (given is not null)
            return Path.GetFullPath(given, currentDirectory);
        for (var folder = new DirectoryInfo(currentDirectory); folder is not null; folder = folder.Parent)
        {
            if (folder.EnumerateFiles("*.sln").Any() || folder.EnumerateFiles("*.slnx").Any())
                return folder.FullName;
        }

        return currentDirectory;
    }

    /// <summary>Asks yes or no on the error stream; without a terminal, says to pass --yes instead.</summary>
    public static async Task<bool> ConfirmAsync(HostIo io, string question, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(io);
        if (!io.Interactive)
        {
            await io.Error.WriteLineAsync("No one to ask: pass --yes to go on.").ConfigureAwait(false);
            return false;
        }

        await io.Error.WriteAsync($"{question} [y/N] ").ConfigureAwait(false);
        var answer = (await io.In.ReadLineAsync(cancellationToken).ConfigureAwait(false))?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A value as indented JSON, with camel case names.</summary>
    public static string ToJson(object value) => JsonSerializer.Serialize(value, WebIndented);
}
