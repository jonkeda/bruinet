using Bruinet.Scraper.Service;

namespace Bruinet.Bdd.LanguageServer;

// The Scraper over the solution's scraped projects, for the MCP server's scrape_* tools. The work is ScraperService's.
public sealed partial class BddLanguageServer
{
    private ScraperService? _scraper;

    /// <summary>The Scraper over the solution's scraped projects; the MCP server shares it.</summary>
    internal ScraperService Scraper
    {
        get
        {
            lock (_gate)
            {
                return _scraper ??= new ScraperService(_workspace?.Root ?? Environment.CurrentDirectory, templates: new Bruinet.Cli.Generate.ControlTemplates());
            }
        }
    }

    /// <summary>The scraped projects and what each has recorded.</summary>
    public IReadOnlyList<ScraperProjectInfo> ScraperProjects()
    {
        var all = Scraper.Projects();
        return all.Select(project => new ScraperProjectInfo(
            project.Name,
            Scraper.Label(project, all),
            project.Folder,
            project.Config.Platform,
            project.Config.StartUrl,
            project.Sessions().Select(session => session.Id).ToList())).ToList();
    }

    private void DisposeScraper()
    {
        ScraperService? scraper;
        lock (_gate)
        {
            scraper = _scraper;
            _scraper = null;
        }

        scraper?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>A scraped project.</summary>
public sealed record ScraperProjectInfo(string Name, string Label, string Folder, string Platform, string StartUrl, IReadOnlyList<string> Sessions);
