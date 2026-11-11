using System.CommandLine;
using System.Text.Json;

namespace Bruinet.Cli;

/// <summary>The options every command spells the same way.</summary>
internal static class CommonOptions
{
    public static Option<string?> Solution() =>
        new("--solution") { Description = "The folder projects are found under. Default: the nearest folder up with a .sln or .slnx, else this one." };

    public static Option<string?> Project() =>
        new("--project", "-p") { Description = "The project: its name or folder. Default: the one this folder is in, else the only one." };

    public static Option<bool> Json() =>
        new("--json") { Description = "The result as one JSON document on stdout, for scripts and models." };

    public static Option<bool> Yes() =>
        new("--yes", "-y") { Description = "Answer yes to the question before it starts. Needed when no one is at a terminal." };

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

    /// <summary>A scenario's place, <c>file[:line]</c>: the file's full path, and the line when one is given.</summary>
    public static (string Path, int? Line) Place(string text, string currentDirectory)
    {
        var colon = text.LastIndexOf(':');
        // The last colon: a drive letter's (C:) is the first, and no line follows it.
        if (colon > 0 && !(colon == 1 && char.IsLetter(text[0]))
            && int.TryParse(text[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var line))
        {
            return (Path.GetFullPath(text[..colon], currentDirectory), line);
        }

        return (Path.GetFullPath(text, currentDirectory), null);
    }

    /// <summary>Asks yes or no on the error stream; without a terminal, says to pass --yes instead.</summary>
    public static async Task<bool> ConfirmAsync(CliIo io, string question, CancellationToken cancellationToken)
    {
        if (!io.Interactive)
        {
            await io.Error.WriteLineAsync("No one to ask: pass --yes to go on.").ConfigureAwait(false);
            return false;
        }

        await io.Error.WriteAsync($"{question} [y/N] ").ConfigureAwait(false);
        var answer = (await io.In.ReadLineAsync(cancellationToken).ConfigureAwait(false))?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    public static string ToJson(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
}
