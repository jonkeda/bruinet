using Bruinet.Cli;
using Bruinet.Scraper.Host;

// bruinet: Bruinet's one command line (AD-028). `bruinet --help` lists the commands.
// In an app's own Scraper project, `bruinet scrape …` is that project's to run, with the app's records and readers.
if (args is ["scrape", .. var scrape] && ScraperAppProject.Find(Environment.CurrentDirectory) is { } app)
    return await ScraperAppProject.RunAsync(app, scrape);

return await BruinetCommand.RunAsync(args, CliIo.Console());
