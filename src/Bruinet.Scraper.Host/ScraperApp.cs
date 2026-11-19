using System.CommandLine;
using System.CommandLine.Help;

namespace Bruinet.Scraper.Host;

/// <summary>
/// The Scraper for one app: what the app's own Scraper project runs. The project's <c>Program</c> is one call,
/// <c>return await ScraperApp.RunAsync(args, app =&gt; app.AddRecord(new ContactRecord()));</c>, and its arguments
/// are those of <c>bruinet scrape</c> without the first two words.
/// </summary>
public sealed class ScraperApp
{
    private readonly List<IRecordKind> _records = [];
    private readonly List<IPlaceReader> _readers = [];

    private ScraperApp()
    {
    }

    /// <summary>Adds a kind of record the Scraper may make for itself.</summary>
    public ScraperApp AddRecord(IRecordKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        _records.Add(kind);
        return this;
    }

    /// <summary>Adds a reader for a widget of the app's own.</summary>
    public ScraperApp AddReader(IPlaceReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _readers.Add(reader);
        return this;
    }

    /// <summary>Runs one command of the Scraper with the app's records and readers; returns its exit code.</summary>
    /// <param name="args">The command line, from the Scraper's command on: <c>places walk --only /contact</c>.</param>
    /// <param name="configure">Adds what the app has.</param>
    /// <param name="io">Where it reads and writes; the console when null.</param>
    /// <param name="cancellationToken">Stops the command.</param>
    public static async Task<int> RunAsync(string[] args, Action<ScraperApp>? configure = null, HostIo? io = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var app = new ScraperApp();
        configure?.Invoke(app);
        var console = io ?? HostIo.Console();
        var host = console with
        {
            Services = console.Services with
            {
                Records = [.. console.Services.Records, .. app._records],
                Readers = [.. console.Services.Readers, .. app._readers],
            },
        };

        var root = new RootCommand("The Scraper for this app: explore or walk it, then draft and verify page objects.");
        foreach (var command in ScrapeCommands.Build(host).Subcommands)
            root.Subcommands.Add(command);
        root.SetAction(bare =>
        {
            new HelpAction().Invoke(bare);
            return 0;
        });

        var parsed = root.Parse(args);
        if (parsed.Errors.Count > 0)
        {
            foreach (var error in parsed.Errors)
                await host.Error.WriteLineAsync(error.Message).ConfigureAwait(false);
            return 2;
        }

        return await parsed.InvokeAsync(new InvocationConfiguration { Output = host.Out, Error = host.Error, EnableDefaultExceptionHandler = false }, cancellationToken).ConfigureAwait(false);
    }
}
