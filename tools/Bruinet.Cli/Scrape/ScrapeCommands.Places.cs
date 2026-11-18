using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Service;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Scrape;

/// <summary><c>bruinet scrape places</c>: the list of an app's places, and the walk that fills it.</summary>
internal static partial class ScrapeCommands
{
    private static Option<string?> ListFile() =>
        new("--list") { Description = $"The list's file. Default: .testing/{PlacesList.FileName} of the suite the project is in." };

    private static Command Places(CliIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var write = new Option<bool>("--write") { Description = $"Write the list as a document, {PlacesDocument.FileName} beside it." };
        var command = new Command("places", "The places of the app (pages, tabs, dialogs, menus, buttons that only call the back end), kept between runs: print the list, or fill it with seed, routes, walk and add.")
        {
            project, solution, list, write, json,
            PlacesSeed(io), PlacesRoutes(io), PlacesWalk(io), PlacesAdd(io),
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            if (parsed.GetValue(json))
                await io.Out.WriteAsync(places.ToJson()).ConfigureAwait(false);
            else if (parsed.GetValue(write))
                await io.Error.WriteLineAsync($"{places.Places.Count} places written to {WriteDocument(places, file)}; {places.Frontier.Count()} still to open.").ConfigureAwait(false);
            else
                await io.Out.WriteAsync(PlacesDocument.Markdown(places)).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesSeed(CliIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var map = new Option<string[]>("--map") { Description = "A Markdown file that names the app's addresses in backticks (repeatable). Default: .testing/170-Map.md when the suite has it.", AllowMultipleArgumentsPerToken = false };
        var command = new Command("seed", "Add what is known without opening the app: the map's addresses, the addresses of the suite's page objects, and the pages the recordings captured.")
        {
            project, solution, list, map,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var suite = Suite.PagesChecklist.FindSuite(service.Project(name).Folder);
            var maps = parsed.GetValue(map) is { Length: > 0 } given
                ? given.Select(path => Path.GetFullPath(path, io.CurrentDirectory)).ToArray()
                : suite is not null && File.Exists(Path.Combine(suite, ".testing", "170-Map.md")) ? [Path.Combine(suite, ".testing", "170-Map.md")] : [];
            var fromMap = 0;
            foreach (var path in maps)
            {
                if (!File.Exists(path))
                    throw new ArgumentException($"The map {path} does not exist.");
                fromMap += PlacesSeeds.FromMarkdown(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Count(places.Add);
            }

            var fromSuite = suite is null ? 0 : Suite.SuitePages.Read(suite, null).Pages.Where(page => page.Address is not null)
                .Select(page => PlacesList.Known(page.Address!, PlacesSeeds.Suite)).Count(places.Add);
            var fromRecordings = service.SeedPlaces(name, places);
            places.Save(file);
            await io.Error.WriteLineAsync($"New: {fromMap} from the map, {fromSuite} from the suite's page objects, {fromRecordings} from the recordings. {places.Places.Count} places in {WriteDocument(places, file)}; {places.Frontier.Count()} still to open.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesRoutes(CliIo io)
    {
        var (solution, project, list, headless) = (Solution(), Project(), ListFile(), Headless());
        var asAccount = new Option<string?>("--as") { Description = "Read as this account: its saved sign-in beside the project's own. Default: the project's own." };
        var command = new Command("routes", "Read the routes the app's own scripts name: each page of the list is opened and the scripts only it loads are read as text. Nothing is pressed.")
        {
            project, solution, list, asAccount, headless,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var read = await service.ReadRoutesAsync(name, places, parsed.GetValue(asAccount), !parsed.GetValue(headless) && io.Interactive, new Said(io.Error), cancellationToken).ConfigureAwait(false);
            places.Save(file);
            WriteDocument(places, file);
            await io.Out.WriteLineAsync($"{read.Pages} pages opened, {read.Scripts} scripts read, {read.Routes} routes named, {read.New} of them new to the list.").ConfigureAwait(false);
            if (read.Problem is not null)
                await io.Error.WriteLineAsync(read.Problem).ConfigureAwait(false);
            return read.Problem is null ? 0 : 1;
        });
        return command;
    }

    private static Command PlacesWalk(CliIo io)
    {
        var (solution, project, list, headless, json) = (Solution(), Project(), ListFile(), Headless(), Json());
        var asAccount = new Option<string?>("--as") { Description = "Walk as this account: its saved sign-in beside the project's own. Every write is held back unless --may-write. Default: the project's own." };
        var mayWrite = new Option<bool>("--may-write") { Description = "With --as: the account may write, so a press on a record the walk owns (--own) goes through." };
        var own = new Option<string[]>("--own") { Description = "The address of a record the walk owns, made by a test with the run marker in its name (repeatable): on its pages a press's writes go through. Anywhere else they are held back.", AllowMultipleArgumentsPerToken = false };
        var only = new Option<string?>("--only") { Description = "Only pages whose key starts with this (/contact)." };
        var again = new Option<bool>("--again") { Description = "Open pages that were seen before too." };
        var linksAndTabs = new Option<bool>("--links-and-tabs") { Description = "Follow links and tabs only: no other button is pressed." };
        var maxMinutes = Positive("--max-minutes", "Stop after this many minutes. Default: 20.");
        var maxPlaces = Positive("--max-places", "Open at most this many pages.");
        var maxPresses = Positive("--max-presses", "At most this many presses on one page. Default: 25.");
        var maxDepth = Positive("--max-depth", "How deep to press into what a press opened. Default: 2: a dialog's own buttons.");
        var command = new Command("walk", "Walk the app for its places, from the list's frontier: open each page, read it, and press what a person can press, each once. How much is pressed follows the config's Environment; a write is held back unless the record is the walk's own. Fills in no form, saves nothing.")
        {
            project, solution, list, asAccount, mayWrite, own, only, again, linksAndTabs, maxMinutes, maxPlaces, maxPresses, maxDepth, headless, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var account = parsed.GetValue(asAccount);
            var defaults = new WalkOptions();
            var options = defaults with
            {
                MaxMinutes = parsed.GetValue(maxMinutes) ?? defaults.MaxMinutes,
                MaxPlaces = parsed.GetValue(maxPlaces),
                MaxPresses = parsed.GetValue(maxPresses) ?? defaults.MaxPresses,
                MaxDepth = parsed.GetValue(maxDepth) ?? defaults.MaxDepth,
                Only = parsed.GetValue(only),
                Again = parsed.GetValue(again),
                Own = parsed.GetValue(own) ?? [],
                HoldAll = account is { Length: > 0 } && !parsed.GetValue(mayWrite),
                LinksAndTabs = parsed.GetValue(linksAndTabs),
                Progress = new Said(io.Error),
            };
            var config = service.Project(name).Config;
            await io.Error.WriteLineAsync($"Walk {service.Project(name).Name} as {(account is { Length: > 0 } ? account : "the project's own account")}: Environment {config.Environment.ToString().ToLowerInvariant()}, {(options.LinksAndTabs ? "links and tabs only" : config.Environment == Bruinet.Scraper.Projects.AppEnvironment.Production ? "only what is known to show more is pressed" : "every button is pressed but what the config forbids")}; writes {(options.HoldAll || options.Own.Count == 0 ? "all held back" : $"held back but on {string.Join(", ", options.Own)}")}; {options.MaxMinutes} min.").ConfigureAwait(false);
            var result = await service.WalkPlacesAsync(name, file, options, account, !parsed.GetValue(headless) && io.Interactive, cancellationToken).ConfigureAwait(false);
            var places = PlacesList.Load(file);
            var document = WriteDocument(places, file);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(result)).ConfigureAwait(false);
            }
            else
            {
                await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{result.Stop}: {result.Opened} pages opened, {result.Presses} presses, {result.Held} writes held back, {result.Found} places found, {result.Left} still to open.")).ConfigureAwait(false);
                foreach (var note in result.Notes)
                    await io.Out.WriteLineAsync(note).ConfigureAwait(false);
                await io.Out.WriteLineAsync($"List: {document}").ConfigureAwait(false);
            }

            return result.Stop == WalkStop.SignIn ? 1 : 0;
        });
        return command;
    }

    private static Command PlacesAdd(CliIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var fromRun = new Option<string>("--from-run") { Description = "A file tests wrote, a saved form as JSON per line (place, required, landed, record, note).", Required = true };
        var command = new Command("add", "Add what tests found when they saved forms through their page objects: the form is saved, where it landed is a page, and its record is one the walk may own.")
        {
            project, solution, list, fromRun,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var run = Path.GetFullPath(parsed.GetValue(fromRun)!, io.CurrentDirectory);
            if (!File.Exists(run))
                throw new ArgumentException($"The run's file {run} does not exist.");
            var places = PlacesList.Load(file);
            var owned = PlacesSeeds.AddRun(places, await File.ReadAllTextAsync(run, cancellationToken).ConfigureAwait(false));
            places.Save(file);
            WriteDocument(places, file);
            await io.Error.WriteLineAsync($"{places.Places.Count(place => place.State == PlaceState.Saved)} forms saved in the list; {places.Frontier.Count()} still to open.").ConfigureAwait(false);
            foreach (var record in owned)
                await io.Out.WriteLineAsync(record).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    // The list's file: as given, else in the .testing folder of the suite the project is in, else in the project.
    private static string PlacesFile(ScraperService service, string? project, string? given, CliIo io)
    {
        if (given is { Length: > 0 })
            return Path.GetFullPath(given, io.CurrentDirectory);
        var folder = service.Project(ProjectName(service, project, io)).Folder;
        return Suite.PagesChecklist.FindSuite(folder) is { } suite ? Path.Combine(suite, ".testing", PlacesList.FileName) : Path.Combine(folder, PlacesList.FileName);
    }

    // Writes the document beside the list, with the line ends the document has.
    private static string WriteDocument(PlacesList places, string listFile)
    {
        var file = Path.Combine(Path.GetDirectoryName(listFile)!, PlacesDocument.FileName);
        var markdown = PlacesDocument.Markdown(places);
        var crlf = File.Exists(file) && File.ReadAllText(file).Contains("\r\n", StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, crlf ? markdown.Replace("\n", "\r\n", StringComparison.Ordinal) : markdown);
        return file;
    }

    // What the walk is doing, a line each, as it happens.
    private sealed class Said(TextWriter errors) : IProgress<string>
    {
        public void Report(string doing) => errors.WriteLine(doing);
    }
}
