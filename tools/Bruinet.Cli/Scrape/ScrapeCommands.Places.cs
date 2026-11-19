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

    private static Command Places(CliIo io) => new("places", "The places of the app (pages, tabs, dialogs, menus, buttons that only call the back end), kept between runs: show the list, or fill it with seed, routes, walk and add.")
    {
        PlacesShow(io), PlacesSeed(io), PlacesRoutes(io), PlacesWalk(io), PlacesAdd(io), PlacesRead(io), PlacesFields(io), PlacesElements(io), PlacesDraft(io), PlacesDrift(io),
    };

    private static Command PlacesDraft(CliIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var place = new Argument<string>("place") { Description = "The place's key, as the list writes it (\"/day-off › + Day off\")." };
        var kind = new Option<string>("--as") { Description = "What to draft: form (a member per field and button), row (a member per column of the list), or form-test (a test that holds the form against its class, saving nothing).", DefaultValueFactory = _ => "form" };
        var name = new Option<string?>("--name") { Description = "The class's name. Default: from the place." };
        var page = new Option<string?>("--page") { Description = "For a dialog: the class of the page it is on. Default: the page class that names the page's address." };
        var write = new Option<bool>("--write") { Description = "Write the draft into the pages folder of the place's module, as <Class>.scraped.cs. A test is only printed." };
        var command = new Command("draft", "Draft a class from a place of the list, in the suite's shape (draft-templates.md beside scraper.config.md): a form, a list's row, or the test that holds a form against its class.")
        {
            place, project, solution, list, kind, name, page, write,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var projectName = ProjectName(service, parsed.GetValue(project), io);
            var places = PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io));
            var what = parsed.GetValue(kind) switch
            {
                "form" => Bruinet.Scraper.Code.PlaceDraftKind.Form,
                "row" => Bruinet.Scraper.Code.PlaceDraftKind.Row,
                "form-test" => Bruinet.Scraper.Code.PlaceDraftKind.FormTest,
                var other => throw new ArgumentException($"--as is form, row or form-test, not '{other}'."),
            };
            var (draft, file) = service.DraftPlace(projectName, places, parsed.GetValue(place)!, what, parsed.GetValue(name), parsed.GetValue(page));
            if (parsed.GetValue(write) && file is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                await File.WriteAllTextAsync(file, draft.Code, cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"{draft.ClassName}: {draft.Members.Count} members written to {file}.").ConfigureAwait(false);
            }
            else
                await io.Out.WriteAsync(draft.Code).ConfigureAwait(false);
            foreach (var left in draft.NotDrafted)
                await io.Error.WriteLineAsync($"Not drafted: {left}").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesDrift(CliIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var check = new Option<bool>("--check") { Description = "Exit 1 when a class no longer fits its place." };
        var command = new Command("drift", "Hold the classes drafted from places against the list as it is now: a field or column that went, and one that came.")
        {
            project, solution, list, check,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var projectName = ProjectName(service, parsed.GetValue(project), io);
            var drifts = service.PlaceDrifts(projectName, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            foreach (var drift in drifts)
            {
                await io.Out.WriteLineAsync($"{drift.Class} ({drift.Place}){(drift.Note.Length > 0 ? ": " + drift.Note : "")}").ConfigureAwait(false);
                if (drift.NotOnPlace.Count > 0)
                    await io.Out.WriteLineAsync($"  names, not on the place: {string.Join(", ", drift.NotOnPlace)}").ConfigureAwait(false);
                if (drift.NotInClass.Count > 0)
                    await io.Out.WriteLineAsync($"  on the place, not named: {string.Join(", ", drift.NotInClass)}").ConfigureAwait(false);
            }

            await io.Error.WriteLineAsync(drifts.Count == 0 ? "Every class drafted from a place fits its place." : $"{drifts.Count} classes no longer fit their place.").ConfigureAwait(false);
            return parsed.GetValue(check) && drifts.Count > 0 ? 1 : 0;
        });
        return command;
    }

    private static Command PlacesRead(CliIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var command = new Command("read", "Read the places again from the captures the walk kept, with the control signs and components the project names now: each field's control, and the component a dialog or a menu is. Opens nothing.")
        {
            project, solution, list,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var kept = service.KeptPlaces(name, places).Count;
            var changed = service.ReadPlacesAgain(name, places);
            places.Save(file);
            WriteDocument(places, file);
            await io.Error.WriteLineAsync($"{kept} of {places.Places.Count} places have a kept capture; {changed} read differently now.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesFields(CliIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("fields", "Count the fields of the list by control: what no sign recognizes and what is still to make first.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var counts = PlacesReading.Fields(PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(counts, PrettyJson)).ConfigureAwait(false);
                return 0;
            }

            await io.Out.WriteLineAsync("| Control | State | Fields | Places | For example |").ConfigureAwait(false);
            await io.Out.WriteLineAsync("| --- | --- | --- | --- | --- |").ConfigureAwait(false);
            foreach (var count in counts)
                await io.Out.WriteLineAsync($"| {count.Control} | {(!count.Recognized ? "no sign fits" : count.Made ? "made" : "to make")} | {count.Fields} | {count.Places} | {count.Example} |").ConfigureAwait(false);
            var all = counts.Sum(count => count.Fields);
            await io.Error.WriteLineAsync($"{all} fields: {counts.Where(count => count.Recognized && count.Made).Sum(count => count.Fields)} a made control, {counts.Where(count => count.Recognized && !count.Made).Sum(count => count.Fields)} a control to make, {counts.Where(count => !count.Recognized).Sum(count => count.Fields)} no sign fits.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesElements(CliIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("elements", "Count the app's own elements (custom elements) over the kept captures, with the control or component that stands for each: what nothing stands for first.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var counts = service.PlaceElements(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(counts, PrettyJson)).ConfigureAwait(false);
                return 0;
            }

            await io.Out.WriteLineAsync("| Element | Stands for it | Times | Pages | For example |").ConfigureAwait(false);
            await io.Out.WriteLineAsync("| --- | --- | --- | --- | --- |").ConfigureAwait(false);
            foreach (var count in counts)
                await io.Out.WriteLineAsync($"| {count.Tag} | {(count.CoveredBy is null ? "nothing" : count.Made ? count.CoveredBy : count.CoveredBy + " (to make)")} | {count.Times} | {count.Places} | {count.Example} |").ConfigureAwait(false);
            await io.Error.WriteLineAsync($"{counts.Count} elements of the app's own: {counts.Count(count => count.CoveredBy is not null && count.Made)} have a control or component, {counts.Count(count => count.CoveredBy is not null && !count.Made)} one to make, {counts.Count(count => count.CoveredBy is null)} nothing.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static readonly System.Text.Json.JsonSerializerOptions PrettyJson = new() { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

    private static Command PlacesShow(CliIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var write = new Option<bool>("--write") { Description = $"Write the list as a document, {PlacesDocument.FileName} beside it." };
        var command = new Command("show", "Print the list of the app's places as a document, or write it.")
        {
            project, solution, list, write, json,
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
        public void Report(string doing) => errors.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:HH:mm:ss} {doing}"));
    }
}
