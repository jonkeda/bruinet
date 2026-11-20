using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Places;
using Bruinet.Scraper.Service;
using static Bruinet.Scraper.Host.HostOptions;

namespace Bruinet.Scraper.Host;

/// <summary><c>bruinet scrape places</c>: the list of an app's places, and the walk that fills it.</summary>
public static partial class ScrapeCommands
{
    private static Option<string?> ListFile() =>
        new("--list") { Description = $"The list's file. Default: .testing/{PlacesList.FileName} of the suite the project is in." };

    private static Command Places(HostIo io) => new("places", "The places of the app (pages, tabs, dialogs, menus, buttons that only call the back end), kept between runs: show the list, or fill it with seed, routes, walk and add.")
    {
        PlacesShow(io), PlacesSeed(io), PlacesRoutes(io), PlacesWalk(io), PlacesAdd(io), PlacesRead(io), PlacesFields(io), PlacesElements(io), PlacesDraft(io), PlacesDrift(io), PlacesNotShown(io),
    };

    private static Command PlacesNotShown(HostIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var place = new Argument<string>("place") { Description = "The place's key, as the list writes it." };
        var labels = new Argument<string[]>("labels") { Description = "The buttons, sections or cards a test did not find on the app as the place opens, as the list names them.", Arity = ArgumentArity.ZeroOrMore };
        var clear = new Option<bool>("--clear") { Description = "Take every such label off the place again." };
        var command = new Command("not-shown", "Record on a place what a drafted test did not find on the app: a draft of the place leaves it out and says so. The record stays through a new reading.")
        {
            place, labels, project, solution, list, clear,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var key = parsed.GetValue(place)!;
            if (places[key] is not { } found)
                throw new Bruinet.Scraper.Service.ScraperProblem($"The list has no place '{key}'.");
            string[] now = parsed.GetValue(clear) ? [] : [.. found.NotShown.Concat(parsed.GetValue(labels) ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            places.Set(found with { NotShown = now });
            places.Save(file);
            await io.Error.WriteLineAsync(now.Length == 0 ? $"{key}: nothing is left out." : $"{key}: a draft leaves out {string.Join(", ", now)}.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesDraft(HostIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var place = new Argument<string>("place") { Description = "The place's key, as the list writes it (\"/day-off › + Day off\")." };
        var kind = new Option<string>("--as") { Description = "What to draft: form (a member per field and button), row (a member per column of the list), form-test (a test that holds the form against its class, saving nothing), or save-test (for a dialog on a record's page: a test that saves it on a record of its own), or tabs-part (for a page: the tabs it shares with the other pages of its address, as one class).", DefaultValueFactory = _ => "form" };
        var name = new Option<string?>("--name") { Description = "The class's name. Default: from the place." };
        var page = new Option<string?>("--page") { Description = "For a dialog: the class of the page it is on. Default: the page class that names the page's address." };
        var write = new Option<bool>("--write") { Description = "Write the draft into the pages folder of the place's module, as <Class>.cs. A class that is there is merged into: a drafted one gets the draft's members, a person's is only added to. A test is written over the test the Scraper drafted before, and not over a person's." };
        var replace = new Option<bool>("--replace") { Description = "With --write: write the draft over the class the Scraper drafted before, as it is. A member the place no longer gives goes. Not for a class a person added to." };
        var command = new Command("draft", "Draft a class from a place of the list, in the suite's shape (draft-templates.md beside scraper.config.md): a form, a list's row, or the test that holds a form against its class.")
        {
            place, project, solution, list, kind, name, page, write, replace,
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
                "save-test" => Bruinet.Scraper.Code.PlaceDraftKind.SaveTest,
                "tabs-part" => Bruinet.Scraper.Code.PlaceDraftKind.TabsPart,
                var other => throw new ArgumentException($"--as is form, row, form-test, save-test or tabs-part, not '{other}'."),
            };
            var (draft, file) = service.DraftPlace(projectName, places, parsed.GetValue(place)!, what, parsed.GetValue(name), parsed.GetValue(page));
            if (parsed.GetValue(write) && parsed.GetValue(replace) && file is not null && File.Exists(file) && Bruinet.Scraper.Code.ScrapedHeader.Find(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false))?.Page is not null)
            {
                await File.WriteAllTextAsync(file, draft.Code, cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"{draft.ClassName}: written over {file}.").ConfigureAwait(false);
            }
            else if (parsed.GetValue(write) && file is not null)
            {
                var merged = Bruinet.Scraper.Service.ScraperService.WriteDraft(file, draft);
                await io.Error.WriteLineAsync(merged is null
                    ? $"{draft.ClassName}: {draft.Members.Count} members written to {file}."
                    : $"{draft.ClassName}: merged into {file}: {merged.Added.Count} added, {merged.Replaced.Count} replaced{(merged.Kept.Count > 0 ? $"; kept though the place does not give them: {string.Join(", ", merged.Kept)}" : "")}.").ConfigureAwait(false);
            }
            else if (parsed.GetValue(write) && what is Bruinet.Scraper.Code.PlaceDraftKind.FormTest or Bruinet.Scraper.Code.PlaceDraftKind.SaveTest)
            {
                var (testFile, written) = service.WriteTest(projectName, draft);
                await io.Error.WriteLineAsync(written ? $"{draft.ClassName}: written to {testFile}." : $"{draft.ClassName}: not written. {testFile} does not start with the Scraper's line: it is a person's.").ConfigureAwait(false);
            }
            else
                await io.Out.WriteAsync(draft.Code).ConfigureAwait(false);
            foreach (var left in draft.NotDrafted)
                await io.Error.WriteLineAsync($"Not drafted: {left}").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesDrift(HostIo io)
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

    private static Command PlacesRead(HostIo io)
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

    private static Command PlacesFields(HostIo io)
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

    private static Command PlacesElements(HostIo io)
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

    private static Command PlacesShow(HostIo io)
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

    private static Command PlacesSeed(HostIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var map = new Option<string[]>("--map") { Description = "A Markdown file that names the app's addresses in backticks (repeatable). Default: .testing/170-Map.md when the suite has it.", AllowMultipleArgumentsPerToken = false };
        var command = new Command("seed", "Add what is known without opening the app: the map's addresses, the addresses of the suite's page objects, the pages the recordings captured, and the address the app starts at.")
        {
            project, solution, list, map,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var suite = FindSuite(service.Project(name).Folder);
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

            var fromSuite = suite is null || io.Services.SuiteAddresses is not { } addresses ? 0
                : addresses(suite).Select(address => PlacesList.Known(address, PlacesSeeds.Suite)).Count(places.Add);
            var fromRecordings = service.SeedPlaces(name, places);
            // Where the app starts: the one address every project has, and all a new one has.
            var fromStart = Uri.TryCreate(service.Project(name).Config.StartUrl, UriKind.Absolute, out var start) && places.Add(PlacesList.Known(start.PathAndQuery + start.Fragment, "config")) ? 1 : 0;
            places.Save(file);
            await io.Error.WriteLineAsync($"New: {fromMap} from the map, {fromSuite} from the suite's page objects, {fromRecordings} from the recordings, {fromStart} from the config's start address. {places.Places.Count} places in {WriteDocument(places, file)}; {places.Frontier.Count()} still to open.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command PlacesRoutes(HostIo io)
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

    private static Command PlacesWalk(HostIo io)
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
            // The setup's first answer: on an environment that is not safe to write on, nothing but links and tabs, and every write held back.
            if (Bruinet.Scraper.Factory.FactorySetup.Read(service.Project(name).Home) is { Writes: Bruinet.Scraper.Factory.WritesAllowed.ReadOnly } && File.Exists(Path.Combine(service.Project(name).Home, Bruinet.Scraper.Factory.FactorySetup.FileName)))
            {
                options = options with { LinksAndTabs = true, HoldAll = true };
                await io.Error.WriteLineAsync($"{Bruinet.Scraper.Factory.FactorySetup.FileName} says Writes read-only: links and tabs only, every write held back.").ConfigureAwait(false);
            }

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

    private static Command PlacesAdd(HostIo io)
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

    // The list's file: as given; else in the app's own Scraper project when it has one; else in the .testing folder
    // of the suite the project is in; else in the project.
    private static string PlacesFile(ScraperService service, string? project, string? given, HostIo io)
    {
        if (given is { Length: > 0 })
            return Path.GetFullPath(given, io.CurrentDirectory);
        return PlacesFileOf(service.Project(ProjectName(service, project, io)));
    }

    /// <summary>
    /// Where a project's list of places is: in the app's own Scraper project when it has one; else in the
    /// <c>.testing</c> folder of the suite the project is in; else in the project.
    /// </summary>
    public static string PlacesFileOf(Bruinet.Scraper.Projects.ScrapeProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!string.Equals(project.Home, project.Folder, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(project.Home, PlacesList.FileName);
        return FindSuite(project.Folder) is { } suite ? Path.Combine(suite, ".testing", PlacesList.FileName) : Path.Combine(project.Folder, PlacesList.FileName);
    }

    // The suite a folder is in: the nearest folder up that has a .testing folder.
    private static string? FindSuite(string from)
    {
        for (var folder = new DirectoryInfo(from); folder is not null; folder = folder.Parent)
        {
            if (Directory.Exists(Path.Combine(folder.FullName, ".testing")))
                return folder.FullName;
        }

        return null;
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
