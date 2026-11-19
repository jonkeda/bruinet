using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Service;

public sealed partial class ScraperService
{
    /// <summary>A recording session's events, in order: the page captures Explore or the walk made.</summary>
    /// <exception cref="ScraperProblem">No such session.</exception>
    public IReadOnlyList<RecordingEvent> RecordedActions(string? projectName, string session)
    {
        var project = Project(projectName);
        return (project.Session(session) ?? throw new ScraperProblem($"{project.Name} has no recording session '{session}'.")).Events;
    }
}
