using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Factory;

/// <summary>
/// What a change the factory keeps by itself may touch, as the setup's <c>Improve</c> answer says
/// (<see cref="ImproveScope"/>): a rule the tool holds, not a promise of the agent that made the change.
/// </summary>
public static class ChangeScope
{
    /// <summary>The files of a change that the scope does not allow; none when the change may be kept.</summary>
    /// <param name="scope">The setup's answer.</param>
    /// <param name="project">The app's project.</param>
    /// <param name="changed">The files the change touched, as full paths.</param>
    public static IReadOnlyList<string> Refused(ImproveScope scope, ScrapeProject project, IEnumerable<string> changed)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(changed);
        return [.. changed.Select(Path.GetFullPath).Where(file => !Allows(scope, project, file)).Order(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>True when the scope allows a change to one file.</summary>
    public static bool Allows(ImproveScope scope, ScrapeProject project, string file)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(file);
        var full = Path.GetFullPath(file);
        var signs = project.Config.ControlLibraries.Where(library => library.Signs.Length > 0)
            .Select(library => Path.GetFullPath(Path.Combine(project.Home, library.Signs))).ToList();
        return scope switch
        {
            ImproveScope.Off => false,
            // What describes the app: a document of its own place, and a library's file of signs.
            ImproveScope.Data => IsData(project, full) || signs.Contains(full, StringComparer.OrdinalIgnoreCase),
            // Also code: the suite's, and a control library's (the folder its signs file is in).
            ImproveScope.Suite => Under(full, project.Home) || Under(full, project.Folder) || signs.Any(sign => Under(full, Path.GetDirectoryName(sign)!)),
            _ => true,
        };
    }

    // A Markdown or JSON file of the Scraper's own place, not what a run keeps of the app (its captures).
    private static bool IsData(ScrapeProject project, string file) =>
        Under(file, project.Home) && Path.GetExtension(file).ToLowerInvariant() is ".md" or ".json"
        && !Under(file, project.RecordingsFolder)
        // When the Scraper's place is the suite itself, the suite's own documents are no data of the Scraper's.
        && (!string.Equals(project.Home, project.Folder, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetDirectoryName(file), project.Home, StringComparison.OrdinalIgnoreCase));

    private static bool Under(string file, string folder) =>
        file.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
