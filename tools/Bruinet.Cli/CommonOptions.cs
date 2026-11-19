using System.CommandLine;

namespace Bruinet.Cli;

/// <summary>The options every command spells the same way.</summary>
internal static class CommonOptions
{
    public static Option<string?> Solution() => Bruinet.Scraper.Host.HostOptions.Solution();

    public static Option<string?> Project() => Bruinet.Scraper.Host.HostOptions.Project();

    public static Option<bool> Json() => Bruinet.Scraper.Host.HostOptions.Json();

    public static Option<bool> Yes() => Bruinet.Scraper.Host.HostOptions.Yes();

    public static Option<bool> Headless() => Bruinet.Scraper.Host.HostOptions.Headless();

    /// <summary>The solution's folder: as given, else the nearest folder up with a solution file, else the current one.</summary>
    public static string SolutionFolder(string? given, string currentDirectory) => Bruinet.Scraper.Host.HostOptions.SolutionFolder(given, currentDirectory);

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
    public static Task<bool> ConfirmAsync(CliIo io, string question, CancellationToken cancellationToken) =>
        Bruinet.Scraper.Host.HostOptions.ConfirmAsync(io, question, cancellationToken);

    public static string ToJson(object value) => Bruinet.Scraper.Host.HostOptions.ToJson(value);
}
