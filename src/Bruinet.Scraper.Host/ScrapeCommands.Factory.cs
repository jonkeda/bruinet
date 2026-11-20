using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Places;
using static Bruinet.Scraper.Host.HostOptions;

namespace Bruinet.Scraper.Host;

// bruinet scrape scraper: the loops that take an app's places to page objects and tests, group by group.
public static partial class ScrapeCommands
{
    private static Command Factory(HostIo io) => new("scraper", "The factory: an app's places to page objects and tests, a small group at a time. What is left, by group.")
    {
        FactorySetupCommand(io), FactoryModules(io), FactoryStatus(io), FactoryRun(io), FactoryNote(io), FactoryReplay(io), FactoryBench(io), FactoryKeep(io), FactoryReviewCommand(io), FactoryRecords(io), FactoryPatterns(io), FactoryBrief(io), FactoryPromote(io),
    };

    private static Command FactoryPromote(HostIo io)
    {
        var (solution, project) = (Solution(), Project());
        var control = new Argument<string>("control") { Description = "The control whose signs move, as the app's config names it under '## Control signs'." };
        var to = new Option<string?>("--to") { Description = "The library, as the config names it under '## Control libraries'. Default: the first that has a file of signs." };
        var command = new Command("promote", "Move a control's signs from this app's config up to a control library's file of signs: what was learned here and holds for the library is the library's, and the next app that uses it starts with it. Replay afterwards shows nothing reads differently.")
        {
            control, project, solution, to,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var scraped = service.Project(ProjectName(service, parsed.GetValue(project), io));
            var wanted = parsed.GetValue(to);
            var library = scraped.Config.ControlLibraries.FirstOrDefault(each => each.Signs.Length > 0 && (wanted is null || string.Equals(each.Library, wanted, StringComparison.OrdinalIgnoreCase)))
                ?? throw new Bruinet.Scraper.Service.ScraperProblem(wanted is null ? "The config names no control library with a file of signs." : $"The config names no control library '{wanted}' with a file of signs.");
            var configFile = Path.Combine(scraped.Home, Bruinet.Scraper.Projects.ScraperConfig.FileName);
            var libraryFile = Path.GetFullPath(Path.Combine(scraped.Home, library.Signs));
            try
            {
                var (config, signs, moved) = SignMove.Move(await File.ReadAllTextAsync(configFile, cancellationToken).ConfigureAwait(false), await File.ReadAllTextAsync(libraryFile, cancellationToken).ConfigureAwait(false), parsed.GetValue(control)!);
                await File.WriteAllTextAsync(libraryFile, signs, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(configFile, config, cancellationToken).ConfigureAwait(false);
                foreach (var row in moved)
                    await io.Out.WriteLineAsync(row).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"{moved.Count} rows moved to {libraryFile}. 'scraper replay' shows whether anything reads differently.").ConfigureAwait(false);
                return 0;
            }
            catch (FormatException problem)
            {
                throw new Bruinet.Scraper.Service.ScraperProblem(problem.Message);
            }
        });
        return command;
    }

    private static Command FactoryPatterns(HostIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var most = new Option<int>("--max") { Description = "How many patterns are looked at, most widespread first.", DefaultValueFactory = _ => 40 };
        var command = new Command("patterns", "The parts the kept captures are built from that repeat and that no control stands for: what a control would be worth making for, most widespread first. 'scraper brief <signature>' writes the brief for one.")
        {
            project, solution, most, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var toMake = Bruinet.Scraper.Code.DraftRun.ControlsToMake(service.Patterns(name, parsed.GetValue(most)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(toMake, PrettyJson)).ConfigureAwait(false);
                return 0;
            }

            await io.Out.WriteAsync(FactoryReview.Patterns(toMake)).ConfigureAwait(false);
            await io.Error.WriteLineAsync($"{toMake.Count} patterns repeat and have no control.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command FactoryBrief(HostIo io)
    {
        var (solution, project) = (Solution(), Project());
        var signature = new Argument<string>("signature") { Description = "The pattern's signature, as 'scraper patterns' writes it." };
        var called = new Option<string?>("--name") { Description = "The control's name. Default: the pattern's own." };
        var variant = new Option<string?>("--variant") { Description = "Which variant of the pattern, when it has several." };
        var lifted = new Option<bool>("--lifted") { Description = "The pattern is lifted to its roots." };
        var command = new Command("brief", "Write the brief for making a control from a pattern: where to write it, what to read, its occurrences, an example, its parts and the hints. For the agent or a person who makes the control.")
        {
            signature, project, solution, called, variant, lifted,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var brief = service.ControlBrief(name, parsed.GetValue(signature)!, parsed.GetValue(called), parsed.GetValue(variant), parsed.GetValue(lifted));
            await io.Out.WriteAsync(brief.Text).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    /// <summary>The file in the app's own Scraper place that lists the records the Scraper made for itself.</summary>
    public const string RecordsFile = "records.md";

    private static Command FactoryRecords(HostIo io)
    {
        var (solution, project) = (Solution(), Project());
        var make = new Option<string?>("--make") { Description = "Make one record of this kind, with the run's marker at the start of its name, and write it down." };
        var sweep = new Option<bool>("--sweep") { Description = "Delete every record of every kind whose name starts with a run's marker, this run's and what an earlier one left." };
        var check = new Option<bool>("--check") { Description = "Exit 1 when the app holds a record with a run's marker: something was made and not taken away." };
        var command = new Command("records", $"The records the Scraper may make for itself in this app, by the kinds its own Scraper project adds (a contact, a project): what the app holds with a run's marker now. With --make one is made and written down in {RecordsFile}; --sweep takes them all away.")
        {
            project, solution, make, sweep, check,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var scraped = service.Project(ProjectName(service, parsed.GetValue(project), io));
            var kinds = io.Services.Records;
            if (kinds.Count == 0)
            {
                await io.Error.WriteLineAsync("This app's Scraper project adds no kind of record (app.AddRecord in its Program.cs): nothing can be made, found or swept.").ConfigureAwait(false);
                return parsed.GetValue(check) ? 1 : 0;
            }

            var file = Path.Combine(scraped.Home, RecordsFile);
            if (parsed.GetValue(make) is { } wanted)
            {
                var kind = kinds.FirstOrDefault(each => string.Equals(each.Name, wanted, StringComparison.OrdinalIgnoreCase))
                    ?? throw new Bruinet.Scraper.Service.ScraperProblem($"No kind of record '{wanted}': the kinds are {string.Join(", ", kinds.Select(each => each.Name))}.");
                if (FactorySetup.Read(scraped.Home).Writes == WritesAllowed.ReadOnly)
                    throw new Bruinet.Scraper.Service.ScraperProblem($"{FactorySetup.FileName} says Writes is read-only: no record is made.");
                var marker = Bruinet.Core.Testing.RunMarker.Current;
                var address = await kind.MakeAsync(marker, cancellationToken).ConfigureAwait(false);
                var head = File.Exists(file) ? "" : "# The Scraper's own records\n\nWhat `scraper records --make` made in the app, each with the run's marker at the start of its name. `scraper records --sweep` deletes them and empties this list.\n\n| Kind | Address | Marker | Made |\n| --- | --- | --- | --- |\n";
                await File.AppendAllTextAsync(file, head + string.Create(CultureInfo.InvariantCulture, $"| {kind.Name} | `{address}` | {marker} | {DateTimeOffset.Now:yyyy-MM-dd HH:mm} |\n"), cancellationToken).ConfigureAwait(false);
                await io.Out.WriteLineAsync(address).ConfigureAwait(false);
            }

            if (parsed.GetValue(sweep))
            {
                foreach (var kind in kinds)
                    await io.Error.WriteLineAsync($"{kind.Name}: {await kind.DeleteAsync(Bruinet.Core.Testing.RunMarker.Prefix, cancellationToken).ConfigureAwait(false)} deleted.").ConfigureAwait(false);
                if (File.Exists(file))
                    File.Delete(file);
            }

            var left = 0;
            foreach (var kind in kinds)
            {
                var found = await kind.FindAsync(Bruinet.Core.Testing.RunMarker.Prefix, cancellationToken).ConfigureAwait(false);
                left += found.Count;
                await io.Out.WriteLineAsync($"{kind.Name}: {found.Count} with a run's marker{(found.Count == 0 ? "" : ": " + string.Join(", ", found.Take(10)))}").ConfigureAwait(false);
            }

            return parsed.GetValue(check) && left > 0 ? 1 : 0;
        });
        return command;
    }

    private static Command FactoryReviewCommand(HostIo io)
    {
        var (solution, project, list) = (Solution(), Project(), ListFile());
        var command = new Command("review", "Write review.md: why each place that still owes a class was not kept, the last reason per place, gathered by reason and by what the evidence has in common. The brief a fix starts from.")
        {
            project, solution, list,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var scraped = service.Project(name);
            var owed = service.OwedPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            var file = await WriteReviewAsync(scraped.Home, FactoryState.Load(scraped.Home), owed, cancellationToken, Bruinet.Scraper.Code.DraftRun.ControlsToMake(service.Patterns(name, 40))).ConfigureAwait(false);
            await io.Error.WriteLineAsync($"Wrote {file}.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private const string PartsHeading = "## Parts no control stands for";

    // The review, with its section on the parts no control stands for: written anew when the patterns are given (it
    // takes a while to find them), kept as it was otherwise.
    private static async Task<string> WriteReviewAsync(string home, FactoryState state, OwedReport owed, CancellationToken cancellationToken, IReadOnlyList<Bruinet.Scraper.Code.Pattern>? toMake = null)
    {
        var keys = owed.Groups.SelectMany(group => group.PagesWithoutClass.Concat(group.DialogFormsNotDrafted).Concat(group.RecordPages)).ToHashSet(StringComparer.Ordinal);
        var file = Path.Combine(home, FactoryReview.FileName);
        var parts = "";
        if (toMake is not null)
        {
            parts = $"\n{PartsHeading}\n\nWhat the kept captures are built from that repeats and has no control, most widespread first ({DateTimeOffset.Now:yyyy-MM-dd}; `scraper review` finds them again). A control is worth making where tests would name the part: `scraper brief <signature>` writes its brief.\n\n{FactoryReview.Patterns(toMake)}";
        }
        else if (File.Exists(file) && (await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)).Replace("\r\n", "\n", StringComparison.Ordinal) is var old && old.IndexOf("\n" + PartsHeading, StringComparison.Ordinal) is >= 0 and var at)
        {
            parts = old[at..];
        }

        await File.WriteAllTextAsync(file, FactoryReview.ToMarkdown(state, keys).TrimEnd('\n') + "\n" + parts, cancellationToken).ConfigureAwait(false);
        return file;
    }

    private static Command FactoryKeep(HostIo io)
    {
        var (solution, project, list, json) = (Solution(), Project(), ListFile(), Json());
        var baseline = new Option<string?>("--baseline") { Description = "Tests that must pass with the change, as a dotnet test filter (Module=Projects): they run on the app, and one that fails refuses the change." };
        var mark = new Option<bool>("--mark") { Description = "When the change may be kept: write what the places owe now as the mark a later change is held against." };
        var command = new Command("keep", "Judge the change in the working tree before it is kept: every changed file against what the setup's Improve answer allows, and what replaying the kept places gives with it. Changes nothing. Exit 1 when a file is outside the scope, or replay shows a field that lost its control.")
        {
            project, solution, list, json, baseline, mark,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var folder = SolutionFolder(parsed.GetValue(solution), io.CurrentDirectory);
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var scraped = service.Project(name);
            var setup = FactorySetup.Read(scraped.Home);
            var changed = await ChangedFilesAsync(folder, cancellationToken).ConfigureAwait(false);
            var refused = ChangeScope.Refused(setup.Improve, scraped, changed);
            var places = PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io));
            var replay = service.ReplayPlaces(name, places);
            // What the places owe may not get worse: held against the mark the last batch, or a person, left.
            var state = FactoryState.Load(scraped.Home);
            var counts = OwedCounts.Of(service.OwedPlaces(name, places));
            var worse = state.Mark is null ? [] : counts.WorseThan(state.Mark);
            var broken = new List<string>();
            if (parsed.GetValue(baseline) is { Length: > 0 } filter)
            {
                var ran = await Bruinet.Scraper.Testing.TestRunner.RunEachAsync(Directory.EnumerateFiles(scraped.Folder, "*.csproj").First(), filter, cancellationToken).ConfigureAwait(false);
                broken.AddRange(ran.Problem is not null ? [ran.Problem] : ran.Tests.Where(test => !test.Passed).Select(test => $"{test.Name}: {test.Message}"));
            }

            var keep = refused.Count == 0 && replay.ControlsLost == 0 && worse.Count == 0 && broken.Count == 0;
            foreach (var line in worse)
                await io.Out.WriteLineAsync($"worse: {line}").ConfigureAwait(false);
            foreach (var line in broken)
                await io.Out.WriteLineAsync($"fails: {line}").ConfigureAwait(false);
            if (keep && parsed.GetValue(mark))
            {
                state.Mark = counts;
                state.Save(scraped.Home);
            }

            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { Keep = keep, Improve = FactorySetup.Name(setup.Improve), Changed = changed, Refused = refused, Replay = new { replay.Same, replay.ControlsGained, replay.ControlsLost, Fields = replay.Fields.Count, Classes = replay.Changed.Count, replay.WouldDraft } }, PrettyJson)).ConfigureAwait(false);
                return keep ? 0 : 1;
            }

            foreach (var file in refused)
                await io.Out.WriteLineAsync($"outside '{FactorySetup.Name(setup.Improve)}': {Path.GetRelativePath(folder, file)}").ConfigureAwait(false);
            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{changed.Count} files changed, {refused.Count} outside what Improve '{FactorySetup.Name(setup.Improve)}' allows. Replay with the change: {replay.ControlsGained} fields gained a control, {replay.ControlsLost} lost one, {replay.Changed.Count} classes would be drafted differently, {replay.WouldDraft} places that owe a class can be drafted. {(keep ? "It may be kept." : "It may not be kept as it is.")}")).ConfigureAwait(false);
            return keep ? 0 : 1;
        });
        return command;
    }

    // The files that differ from the last commit, and those git does not know yet: full paths, from the repository the
    // folder is in and from every repository directly inside that one.
    private static async Task<IReadOnlyList<string>> ChangedFilesAsync(string folder, CancellationToken cancellationToken)
    {
        var top = (await GitAsync(folder, cancellationToken, "rev-parse", "--show-toplevel").ConfigureAwait(false))?.Trim();
        if (string.IsNullOrEmpty(top))
            return [];
        top = Path.GetFullPath(top);
        var files = new List<string>();
        foreach (var repository in new[] { top }.Concat(Directory.EnumerateDirectories(top).Where(child => File.Exists(Path.Combine(child, ".git")) || Directory.Exists(Path.Combine(child, ".git")))))
        {
            var output = await GitAsync(repository, cancellationToken, "status", "--porcelain", "--untracked-files=all").ConfigureAwait(false);
            foreach (var line in (output ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.Length > 3))
            {
                // "XY path", and for a rename "XY old -> new": the new path is what changed.
                var path = line[3..].Trim().Trim('"');
                var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
                var full = Path.GetFullPath(Path.Combine(repository, arrow >= 0 ? path[(arrow + 4)..].Trim('"') : path));
                // A repository inside this one shows as one changed line: its own files are listed from it.
                if (!Directory.Exists(full))
                    files.Add(full);
            }
        }

        return [.. files.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }

    // What a git command printed; null when git is not there or the folder is no repository.
    private static async Task<string?> GitAsync(string folder, CancellationToken cancellationToken, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        try
        {
            using var process = System.Diagnostics.Process.Start(start);
            if (process is null)
                return null;
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static Command FactoryRun(HostIo io)
    {
        var (solution, project, list, headless, json) = (Solution(), Project(), ListFile(), Headless(), Json());
        var groups = new Argument<string[]>("groups") { Description = "The groups to run (/day-off). Default: the groups that owe something, in the order of modules.md.", Arity = ArgumentArity.ZeroOrMore };
        var module = new Option<string?>("--module") { Description = "Only the groups of this module." };
        var batch = Positive("--batch", "At most this many groups in this run. Default: 10.");
        var noWalk = new Option<bool>("--no-walk") { Description = "Do not walk: draft from the captures there are." };
        var noTest = new Option<bool>("--no-test") { Description = "Do not run the drafted tests on the app: stop after the build." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Say which groups would run, and run none." };
        var save = new Option<bool>("--save") { Description = "Draft no class: draft a save test for each dialog on a record's page that has a drafted class and no save test yet. The test saves the form on a record it makes for itself and deletes that record again. Not where the setup's Writes is read-only." };
        var again = new Option<bool>("--again") { Description = "Take up again the places an earlier run tried and did not keep. Default: they are parked, each with its reason in the ledger, until what they wait for is there." };
        var baseline = new Option<string?>("--baseline") { Description = "Tests that must still pass after each loop, as a dotnet test filter (Module=Projects): they run before a loop writes and again at its end, and a loop that breaks one is put back whole." };
        var command = new Command("run", "Run loops of the factory: each group through walk, read, draft, build and test. What does not build or does not pass twice is put back, with a reason; the loop is written into the state and the ledger. Stops when a reason comes three times in the batch, when two loops in a row keep nothing, or when the suite does not build. Exit 1 when it stopped by a rule.")
        {
            groups, project, solution, list, module, batch, noWalk, noTest, dryRun, again, save, baseline, headless, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var scraped = service.Project(name);
            var file = PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io);
            var places = PlacesList.Load(file);
            var owed = service.OwedPlaces(name, places);
            var modulesFile = Path.Combine(scraped.Home, ModuleCut.FileName);
            var modules = ModuleCut.Keep(ModuleCut.Of(places, scraped.Config, owed), File.Exists(modulesFile) ? ModuleCut.Parse(await File.ReadAllTextAsync(modulesFile, cancellationToken).ConfigureAwait(false)) : []);
            var setup = FactorySetup.Read(scraped.Home);
            var given = parsed.GetValue(groups) ?? [];
            var state = FactoryState.Load(scraped.Home);
            // What an earlier run tried and did not keep is parked: it waits for a fix, not for another try.
            var taken = parsed.GetValue(again) ? new HashSet<string>(StringComparer.Ordinal) : state.Loops.SelectMany(loop => loop.Places).ToHashSet(StringComparer.Ordinal);
            var chosen = GroupsToRun(modules, owed, setup, given, parsed.GetValue(module))
                .Where(group => given.Contains(group, StringComparer.Ordinal) || owed.Groups.Any(each => each.Group == group && each.PagesWithoutClass.Concat(each.DialogFormsNotDrafted).Concat(each.RecordPages).Any(place => !taken.Contains(place))))
                .ToList();
            var most = parsed.GetValue(batch) ?? 10;
            var queue = chosen.Take(most).ToList();
            if (queue.Count == 0)
            {
                await io.Error.WriteLineAsync(given.Length == 0 && !setup.RunsAll && setup.Run.Count == 0 && parsed.GetValue(module) is null
                    ? $"Nothing runs: {FactorySetup.FileName} has no answer for Run. Say which with 'scraper setup --set Run=all', name a module with --module, or give the groups."
                    : "No group owes a place that was not tried before. The ledger says why each parked place was not kept; --again takes them up again.").ConfigureAwait(false);
                return 0;
            }

            if (parsed.GetValue(dryRun))
            {
                foreach (var group in queue)
                    await io.Out.WriteLineAsync(group).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"{queue.Count} groups would run of {chosen.Count} that owe something. Nothing was run.").ConfigureAwait(false);
                return 0;
            }

            var number = state.NextBatch;
            var ran = new List<LoopResult>();
            string? stopped = null;
            for (var at = 0; at < queue.Count; at++)
            {
                var group = queue[at];
                cancellationToken.ThrowIfCancellationRequested();
                // A large group goes in loops of a few places: its pages are walked once, in its first loop.
                var first = !ran.Any(each => each.Group == group);
                var loop = await service.RunLoopAsync(name, file, group, new Bruinet.Scraper.Service.LoopOptions { Walk = first && !parsed.GetValue(noWalk), Test = !parsed.GetValue(noTest), Visible = !parsed.GetValue(headless) && io.Interactive, Batch = number, Baseline = parsed.GetValue(baseline), Save = parsed.GetValue(save), Skip = taken, Progress = new Said(io.Error) }, cancellationToken).ConfigureAwait(false);
                taken.UnionWith(loop.Places);
                // The same group again while it has places this run did not take up.
                if (loop.Places.Count > 0 && service.OwedPlaces(name, PlacesList.Load(file)).Groups.FirstOrDefault(each => each.Group == group) is { } still
                    && still.PagesWithoutClass.Concat(still.DialogFormsNotDrafted).Concat(still.RecordPages).Any(place => !taken.Contains(place)))
                {
                    queue.Insert(at + 1, group);
                }

                loop = state.Add(loop);
                ran.Add(loop);
                // After every loop: a run that stops loses nothing it finished.
                state.Save(scraped.Home);
                await File.WriteAllTextAsync(Path.Combine(scraped.Home, FactoryLedger.FileName), FactoryLedger.ToMarkdown(state), cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Loop {loop.Number} {group}: {loop.Kept.Count} kept, {loop.PutBack.Count} put back, first run {loop.PassedFirst} of {loop.Tests}, {loop.Minutes} min{(loop.Reasons.Count == 0 ? "" : "; " + string.Join(", ", loop.Reasons.Select(reason => reason.Code).Distinct()))}")).ConfigureAwait(false);
                stopped = StopRules.Check(ran);
                if (stopped is not null)
                    break;
            }

            // What the places owe now is the mark a later change is held against (scraper keep).
            state.Mark = OwedCounts.Of(service.OwedPlaces(name, PlacesList.Load(file)));
            state.Save(scraped.Home);
            // The batch's brief: what is still owed, by reason.
            await WriteReviewAsync(scraped.Home, state, service.OwedPlaces(name, PlacesList.Load(file)), cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { Batch = number, Loops = ran, Stopped = stopped }, PrettyJson)).ConfigureAwait(false);
            await io.Error.WriteLineAsync(stopped is null
                ? string.Create(CultureInfo.InvariantCulture, $"Batch {number}: {ran.Count} loops, {ran.Sum(loop => loop.Kept.Count)} files kept, {ran.Sum(loop => loop.PutBack.Count)} put back. {Math.Max(0, chosen.Count - ran.Count)} groups still owe something.")
                : $"Batch {number} stopped after {ran.Count} loops: {stopped}.").ConfigureAwait(false);
            return stopped is null ? 0 : 1;
        });
        return command;
    }

    // The groups that owe something, by the order of their modules; a person's choice of modules, or of groups, narrows it.
    private static List<string> GroupsToRun(IReadOnlyList<AppModule> modules, OwedReport owed, FactorySetup setup, string[] given, string? onlyModule)
    {
        // A group the list does not have is a slip of the hand (or a shell that rewrote /project into a path): nothing runs.
        if (given.Where(group => owed.Groups.All(each => each.Group != group)).ToList() is { Count: > 0 } strange)
            throw new Bruinet.Scraper.Service.ScraperProblem($"The list of places has no group {string.Join(", ", strange)}. 'scraper status' names the groups.");
        var chosen = ModuleCut.ToRun(modules)
            .Where(each => onlyModule is null || string.Equals(each.Name, onlyModule, StringComparison.OrdinalIgnoreCase))
            .Where(each => given.Length > 0 || onlyModule is not null || setup.RunsAll || setup.Run.Contains(each.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(each => each.Groups)
            .Where(group => given.Length > 0 ? given.Contains(group, StringComparer.Ordinal) : owed.Groups.Any(each => each.Group == group && each.Count > 0))
            .ToList();
        chosen.AddRange(given.Where(group => !chosen.Contains(group, StringComparer.Ordinal)));
        return chosen;
    }

    private static Command FactoryNote(HostIo io)
    {
        var (solution, project) = (Solution(), Project());
        var text = new Argument<string>("text") { Description = "What to say." };
        var loop = new Option<int?>("--loop") { Description = "The loop it is about." };
        var command = new Command("note", "Add a line to the factory's ledger: what a person or an agent changed, or decided, about a loop.")
        {
            text, project, solution, loop,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var scraped = service.Project(ProjectName(service, parsed.GetValue(project), io));
            var state = FactoryState.Load(scraped.Home);
            state.Notes.Add(new FactoryNote(parsed.GetValue(loop) ?? 0, DateTimeOffset.Now, parsed.GetValue(text)!));
            state.Save(scraped.Home);
            await File.WriteAllTextAsync(Path.Combine(scraped.Home, FactoryLedger.FileName), FactoryLedger.ToMarkdown(state), cancellationToken).ConfigureAwait(false);
            await io.Error.WriteLineAsync($"Noted in {Path.Combine(scraped.Home, FactoryLedger.FileName)}.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command FactorySetupCommand(HostIo io)
    {
        var (solution, project) = (Solution(), Project());
        var set = new Option<string[]>("--set") { Description = "An answer to write, as Question=answer (repeatable): Writes=own-records, Run=all, Improve=suite, Ask=batch, Commit=batch, Specs=person, Walkers=4." };
        var command = new Command("setup", "What a person answered about this app before the factory runs (setup.md in the app's own Scraper place): how careful, how much, how free. Prints it; with --set, writes an answer. A question without an answer takes the careful value.")
        {
            project, solution, set,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var scraped = service.Project(ProjectName(service, parsed.GetValue(project), io));
            var file = Path.Combine(scraped.Home, FactorySetup.FileName);
            var setup = FactorySetup.Read(scraped.Home);
            var answers = parsed.GetValue(set) ?? [];
            foreach (var answer in answers)
            {
                var at = answer.IndexOf('=', StringComparison.Ordinal);
                if (at <= 0)
                    throw new ArgumentException($"--set takes Question=answer, not '{answer}'.");
                setup = setup.With(answer[..at].Trim(), answer[(at + 1)..].Trim());
            }

            if (answers.Length > 0)
            {
                await File.WriteAllTextAsync(file, setup.ToMarkdown(), cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"Written: {file}").ConfigureAwait(false);
            }
            else if (!File.Exists(file))
            {
                await io.Error.WriteLineAsync($"No {FactorySetup.FileName} in {scraped.Home}: these are the careful values. Write an answer with --set.").ConfigureAwait(false);
            }

            await io.Out.WriteAsync(setup.ToMarkdown()).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command FactoryModules(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var write = new Option<bool>("--write") { Description = $"Write the modules as {ModuleCut.FileName} in the app's own Scraper place, keeping the Order and Do a person wrote there." };
        var command = new Command("modules", "Cut the app into modules from the list of places, without walking it: each module's groups, pages known, kinds and what it owes. A person may reorder the written file and leave modules out.")
        {
            project, solution, list, write, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var scraped = service.Project(name);
            var places = PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io));
            var file = Path.Combine(scraped.Home, ModuleCut.FileName);
            var modules = ModuleCut.Keep(ModuleCut.Of(places, scraped.Config, service.OwedPlaces(name, places)), File.Exists(file) ? ModuleCut.Parse(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)) : []);
            if (parsed.GetValue(write))
            {
                await File.WriteAllTextAsync(file, ModuleCut.ToMarkdown(modules), cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync($"Written: {file}").ConfigureAwait(false);
            }

            await io.Out.WriteAsync(parsed.GetValue(json) ? System.Text.Json.JsonSerializer.Serialize(modules, PrettyJson) + Environment.NewLine : ModuleCut.ToMarkdown(modules)).ConfigureAwait(false);
            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{modules.Count} modules, {modules.Sum(module => module.Pages)} pages known, {places.Places.Count(place => place.Kind == PlaceKind.Page && place.State != PlaceState.Known)} of them opened so far.")).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command FactoryReplay(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("replay", "Read every kept place again and draft again every class that was drafted from one, with the signs, holders and templates as they are now, and say what differs from the list and the suite. Writes nothing and opens nothing: the measure of a change. Exit 1 when something differs.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.ReplayPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return report.Same ? 0 : 1;
            }

            foreach (var field in report.Fields)
                await io.Out.WriteLineAsync($"field  {field.Place}: {field.Before ?? "(not read)"} -> {field.After ?? "(not read)"}").ConfigureAwait(false);
            foreach (var change in report.Changed)
            {
                var what = string.Join("; ", new[] { change.Added.Count > 0 ? "now also " + string.Join(", ", change.Added) : "", change.Removed.Count > 0 ? "no longer " + string.Join(", ", change.Removed) : "", change.Note }.Where(part => part.Length > 0));
                await io.Out.WriteLineAsync($"class  {change.Class} ({change.Place}): {what}").ConfigureAwait(false);
            }

            foreach (var reason in report.CannotDraft.GroupBy(each => each.Reason, StringComparer.Ordinal).OrderByDescending(group => group.Count()))
                await io.Out.WriteLineAsync($"cannot draft {reason.Count()}: {reason.Key} (for example {reason.First().Place})").ConfigureAwait(false);
            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{report.Places} places read again and {report.Classes} classes drafted again in {report.Seconds} s: {(report.Same ? "nothing changed" : $"{report.Fields.Count} fields read differently ({report.ControlsGained} gained a control, {report.ControlsLost} lost one), {report.Changed.Count} classes would be drafted differently")}. Of the places that owe a class, {report.WouldDraft} can be drafted now and {report.CannotDraft.Count} cannot.")).ConfigureAwait(false);
            return report.Same ? 0 : 1;
        });
        return command;
    }

    private static Command FactoryBench(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("bench", "Hold the examples (## Examples of the config: a place and the class a person wrote for it) against what their places read as now, and say for each difference who is right. Exit 1 when an example names something the page shows and the reading misses.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.BenchPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return report.Missed > 0 || report.Problems.Count > 0 ? 1 : 0;
            }

            foreach (var difference in report.Differences)
            {
                var says = difference.Verdict switch
                {
                    BenchVerdict.DraftIsRight => "the place shows it, the example does not name it",
                    BenchVerdict.ExampleIsRight => "the example names it, the page shows it, the reading misses it",
                    _ => "the example names it, the capture does not show it",
                };
                await io.Out.WriteLineAsync($"{difference.Class} ({difference.Place}): '{difference.Label}': {says}").ConfigureAwait(false);
            }

            foreach (var problem in report.Problems)
                await io.Out.WriteLineAsync(problem).ConfigureAwait(false);
            await io.Error.WriteLineAsync(report.Examples == 0
                ? "No examples: name a place and its class under ## Examples in the config."
                : string.Create(CultureInfo.InvariantCulture, $"{report.Examples} examples: {report.Missed} missed by the reading, {report.Differences.Count(d => d.Verdict == BenchVerdict.DraftIsRight)} where the place shows more than the example, {report.Differences.Count(d => d.Verdict == BenchVerdict.NotOnThePage)} the capture does not show.")).ConfigureAwait(false);
            return report.Missed > 0 || report.Problems.Count > 0 ? 1 : 0;
        });
        return command;
    }

    private static Command FactoryStatus(HostIo io)
    {
        var (solution, project, json, list) = (Solution(), Project(), Json(), ListFile());
        var command = new Command("status", "What the places of the app still owe, by group: pages and dialog forms without a class, pages of one record, fields without a control, forms not saved. Exit 1 when something is owed.")
        {
            project, solution, list, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var report = service.OwedPlaces(name, PlacesList.Load(PlacesFile(service, parsed.GetValue(project), parsed.GetValue(list), io)));
            var owed = report.PagesWithoutClass + report.DialogFormsNotDrafted + report.RecordPages;
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report, PrettyJson)).ConfigureAwait(false);
                return owed > 0 ? 1 : 0;
            }

            await io.Out.WriteLineAsync("| Group | Kinds | Pages | Pages without a class | Dialog forms not drafted | Record pages (need a record) | Fields without a control | Forms not saved |").ConfigureAwait(false);
            await io.Out.WriteLineAsync("| --- | --- | --- | --- | --- | --- | --- | --- |").ConfigureAwait(false);
            foreach (var group in report.Groups)
            {
                await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                    $"| `{group.Group}` | {string.Join(", ", group.Kinds)} | {group.Pages} | {group.PagesWithoutClass.Count} | {group.DialogFormsNotDrafted.Count} | {group.RecordPages.Count} | {group.FieldsWithoutControl} | {group.FormsNotSaved} |")).ConfigureAwait(false);
            }

            await io.Error.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{report.Places} places, {report.Kept} with a kept capture. Owed: {report.PagesWithoutClass} pages without a class, {report.DialogFormsNotDrafted} dialog forms not drafted, {report.RecordPages} pages of one record. {report.FieldsWithControl} of {report.Fields} fields have a made control. {report.FormsNotSaved} forms not saved.")).ConfigureAwait(false);
            return owed > 0 ? 1 : 0;
        });
        return command;
    }
}
