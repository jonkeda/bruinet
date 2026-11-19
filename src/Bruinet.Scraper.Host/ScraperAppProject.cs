using System.Diagnostics;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Host;

/// <summary>
/// An app's own Scraper project, as <c>bruinet scrape</c> meets it: in that project's folder the command is the
/// project's to run, with the app's records and readers, so <c>bruinet</c> hands it over
/// (<c>dotnet run --project … -- …</c>) and gives its exit code back.
/// </summary>
public static class ScraperAppProject
{
    /// <summary>Set for the process the command was handed to, so it is not handed on again.</summary>
    public const string HandedOverVariable = "BRUINET_SCRAPER_APP";

    /// <summary>
    /// The Scraper project a folder is in: the nearest folder up that has a <c>scraper.config.md</c> and a project
    /// file that names the Scraper's host. Null when there is none, or when this process is that project's own.
    /// </summary>
    public static string? Find(string currentDirectory)
    {
        if (Environment.GetEnvironmentVariable(HandedOverVariable) == "1")
            return null;
        for (var folder = new DirectoryInfo(currentDirectory); folder is not null; folder = folder.Parent)
        {
            if (!File.Exists(Path.Combine(folder.FullName, ScraperConfig.FileName)))
                continue;
            return folder.EnumerateFiles("*.csproj").Select(file => file.FullName).Order(StringComparer.Ordinal).FirstOrDefault(NamesTheHost);
        }

        return null;
    }

    /// <summary>Runs the project with the Scraper's arguments (those after <c>scrape</c>), on this process's console.</summary>
    /// <returns>The project's exit code; 2 when it could not be started.</returns>
    public static async Task<int> RunAsync(string projectFile, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        foreach (var argument in (string[])["run", "--project", projectFile, "--", .. arguments])
            start.ArgumentList.Add(argument);
        start.Environment[HandedOverVariable] = "1";
        using var process = Process.Start(start);
        if (process is null)
            return 2;
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return process.ExitCode;
    }

    // A project is the app's Scraper when it references the host: by its project file, its package, or a suite's own switch for it.
    private static bool NamesTheHost(string projectFile)
    {
        var text = File.ReadAllText(projectFile);
        return text.Contains("Bruinet.Scraper.Host", StringComparison.Ordinal) || text.Contains("<IncludeBruinetScraper>true</IncludeBruinetScraper>", StringComparison.OrdinalIgnoreCase);
    }
}
