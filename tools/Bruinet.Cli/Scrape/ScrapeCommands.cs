using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using static Bruinet.Cli.CommonOptions;

namespace Bruinet.Cli.Scrape;

/// <summary>
/// <c>bruinet scrape</c>: the Scraper for a person, the loop and CI, through the service the views and MCP use.
/// Exit 0 when it ran within its guards, 1 when a guard stopped it or a check failed, 2 when it could not run.
/// </summary>
internal static class ScrapeCommands
{
    public static Command Build(CliIo io) => new("scrape", "The Scraper: record or explore an app, then draft and verify page objects (docs/guides/scraper.md).")
    {
        New(io), List(io), Status(io), Check(io), Explore(io), Record(io), Draft(io), Verify(io), Resolve(io), DraftTest(io), Config(io), InstallBrowser(io),
    };

    private static Command New(CliIo io)
    {
        var name = new Argument<string>("name") { Description = "The project's name, and its namespace's start." };
        var url = new Option<string>("--url") { Description = "Where recording and Explore start.", Required = true };
        var folder = new Option<string?>("--folder") { Description = "Its folder, relative to the solution's. Default: proposed from the name." };
        var platform = new Option<string>("--platform") { Description = "html or blazor.", DefaultValueFactory = _ => "html" };
        platform.AcceptOnlyFromAmong("html", "blazor");
        var testIds = new Option<string[]>("--test-id") { Description = "An attribute that is a test id, preferred first (repeatable).", AllowMultipleArgumentsPerToken = true };
        var references = new Option<string?>("--references") { Description = "projects or packages. Default: projects inside Bruinet's repository, else packages." };
        references.AcceptOnlyFromAmong("projects", "packages");
        var (solution, yes, json) = (Solution(), Yes(), Json());
        var command = new Command("new", "Make a UI test project with a scraper.config.md and an empty Pages folder.") { name, url, folder, platform, testIds, references, solution, yes, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var given = parsed.GetValue(name)!;
            var defaults = service.NewProjectDefaults(given);
            var request = new NewProject(
                given,
                parsed.GetValue(folder) ?? defaults.Folder,
                parsed.GetValue(platform)!,
                parsed.GetValue(url)!,
                parsed.GetValue(testIds) ?? [],
                parsed.GetValue(references) switch
                {
                    "projects" => BruinetReferences.Projects,
                    "packages" => BruinetReferences.Packages,
                    _ => defaults.References,
                });
            var problems = service.NewProjectProblems(request);
            if (problems.Count > 0)
            {
                foreach (var (field, problem) in problems)
                    await io.Error.WriteLineAsync($"{field}: {problem}").ConfigureAwait(false);
                return 2;
            }

            await io.Error.WriteLineAsync($"Make {request.Name} in {request.Folder}: {request.Platform}, starting at {request.StartUrl}, referencing Bruinet as {request.References.ToString().ToLowerInvariant()}.").ConfigureAwait(false);
            if (!parsed.GetValue(yes) && !await ConfirmAsync(io, "Go on?", cancellationToken).ConfigureAwait(false))
            {
                await io.Error.WriteLineAsync("Not made.").ConfigureAwait(false);
                return 2;
            }

            var project = service.NewProject(request);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(new { project.Name, project.Folder })).ConfigureAwait(false);
            }
            else
            {
                await io.Out.WriteLineAsync($"Made {project.Folder}").ConfigureAwait(false);
                await io.Out.WriteLineAsync($"Next: dotnet sln add \"{Path.Combine(project.Folder, project.Name + ".csproj")}\", then bruinet scrape check.").ConfigureAwait(false);
            }

            return 0;
        });
        return command;
    }

    private static Command List(CliIo io)
    {
        var (solution, json) = (Solution(), Json());
        var command = new Command("list", "The projects with a scraper.config.md.") { solution, json };
        command.Action(io, async (parsed, _) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var projects = service.Projects();
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(projects.Select(p => new { p.Name, Folder = service.Relative(p), p.Config.Platform, p.Config.StartUrl }))).ConfigureAwait(false);
                return 0;
            }

            if (projects.Count == 0)
                await io.Error.WriteLineAsync($"No project with a {ScraperConfig.FileName} here. Make one with bruinet scrape new.").ConfigureAwait(false);
            foreach (var project in projects)
                await io.Out.WriteLineAsync($"{service.Label(project, projects),-30} {service.Relative(project),-40} {project.Config.StartUrl}").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command Status(CliIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var command = new Command("status", "Where a project is in the eight steps, and what to do next.") { project, solution, json };
        command.Action(io, async (parsed, _) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var progress = service.Progress(ProjectName(service, parsed.GetValue(project), io));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(progress)).ConfigureAwait(false);
                return 0;
            }

            await io.Out.WriteLineAsync(progress.Project).ConfigureAwait(false);
            foreach (var step in progress.Steps)
            {
                var mark = step.State switch { StepState.Done => "done", StepState.Next => "next", StepState.Attention => "look", _ => "" };
                await io.Out.WriteLineAsync($"  {mark,-4} {step.Step} {step.Name,-13} {step.Summary}").ConfigureAwait(false);
            }

            await io.Out.WriteLineAsync(progress.Next is { } next ? $"Next: {next.Text} (step {next.Step})" : "Next: nothing; every step is done.").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command Check(CliIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var visible = new Option<bool>("--visible") { Description = "Show the browser, to sign in." };
        var command = new Command("check", "Open the start URL: does the app answer, signed in, on a host Explore may go to.") { project, solution, visible, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var check = await service.CheckAppAsync(ProjectName(service, parsed.GetValue(project), io), parsed.GetValue(visible), cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
                await io.Out.WriteLineAsync(ToJson(check)).ConfigureAwait(false);
            else
                await io.Out.WriteLineAsync($"{check.Url}: {check.Detail}").ConfigureAwait(false);
            return check.Status is >= 200 and < 400 && !check.SignIn && check.Refused is null ? 0 : 1;
        });
        return command;
    }

    private static Command Explore(CliIo io)
    {
        var (solution, project, json, yes, headless) = (Solution(), Project(), Json(), Yes(), Headless());
        var url = new Option<string?>("--url") { Description = "Where to start. Default: the config's StartUrl." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Follow links only, and list what it would click." };
        var maxPages = Positive("--max-pages", "At most this many pages.");
        var maxDepth = Positive("--max-depth", "At most this many clicks from the start.");
        var maxMinutes = Positive("--max-minutes", "Stop after this many minutes.");
        var delayMs = Positive("--delay-ms", "Wait this long between clicks.");
        var command = new Command("explore", "Explore the app by itself, under the project's guards, capturing each page.")
        {
            project, solution, url, dryRun, maxPages, maxDepth, maxMinutes, delayMs, headless, yes, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var unattended = parsed.GetValue(yes) || !io.Interactive;
            var noWindow = parsed.GetValue(headless) || !io.Interactive;
            var options = new ExploreOptions
            {
                DryRun = parsed.GetValue(dryRun),
                Url = parsed.GetValue(url),
                MaxPages = parsed.GetValue(maxPages),
                MaxDepth = parsed.GetValue(maxDepth),
                MaxMinutes = parsed.GetValue(maxMinutes),
                DelayMs = parsed.GetValue(delayMs),
                Visible = !noWindow,
                // Unattended, or with no window to sign in in, a sign-in page ends the run.
                StopAtSignIn = unattended || noWindow,
                WaitForSignIn = unattended || noWindow ? null : async ct =>
                {
                    await io.Error.WriteLineAsync("A sign-in page: sign in in the browser window, then press Enter here.").ConfigureAwait(false);
                    await io.In.ReadLineAsync(ct).ConfigureAwait(false);
                },
                Progress = new Lines(io.Error),
            };

            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            await io.Error.WriteLineAsync(service.ExploreSummary(name, options)).ConfigureAwait(false);
            if (!parsed.GetValue(yes) && !await ConfirmAsync(io, "Go on?", cancellationToken).ConfigureAwait(false))
            {
                await io.Error.WriteLineAsync("Not started.").ConfigureAwait(false);
                return 2;
            }

            var result = await service.ExploreAsync(name, options, cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(result)).ConfigureAwait(false);
            }
            else
            {
                await io.Out.WriteLineAsync($"{result.Stop}: {result.StopDetail}").ConfigureAwait(false);
                await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{result.Captures.Count} captures, {result.Clicks} clicks, {result.Skipped.Count} skipped, {result.Blocked.Count} blocked in {result.Took.TotalSeconds:0.0} s")).ConfigureAwait(false);
                if (result.Report is not null)
                    await io.Out.WriteLineAsync($"Report: {result.Report}").ConfigureAwait(false);
            }

            return result.WithinGuards ? 0 : 1;
        });
        return command;
    }

    private static Command Record(CliIo io)
    {
        var (solution, project) = (Solution(), Project());
        var url = new Option<string?>("--url") { Description = "Where to start. Default: the config's StartUrl." };
        var session = new Option<string>("--session") { Description = "The session's name.", DefaultValueFactory = _ => "recording" };
        var byHand = new Option<bool>("--by-hand") { Description = "Capture only when asked (c and Enter), not each page the app moves to." };
        var command = new Command("record", "Record by hand in a browser window: browse, then press Enter here to stop.") { project, solution, url, session, byHand };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            if (!io.Interactive)
            {
                await io.Error.WriteLineAsync("Recording needs a person at a terminal; use bruinet scrape explore for a run on its own.").ConfigureAwait(false);
                return 2;
            }

            await using var service = Service(io, parsed.GetValue(solution));
            var state = await service.StartRecordingAsync(ProjectName(service, parsed.GetValue(project), io), parsed.GetValue(session)!, parsed.GetValue(url), autoCapture: !parsed.GetValue(byHand), visible: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            await io.Error.WriteLineAsync($"Recording {state.Project} into {state.Folder}. Browse in the window; c and Enter captures this page, Enter on its own stops.").ConfigureAwait(false);
            while (await io.In.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line && line.Trim().Length > 0)
            {
                if (!string.Equals(line.Trim(), "c", StringComparison.OrdinalIgnoreCase))
                    continue;
                var capture = await service.CapturePageAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                await io.Error.WriteLineAsync(capture is null ? "Nothing captured." : $"captured {capture.Page}").ConfigureAwait(false);
            }

            var stopped = await service.StopRecordingAsync(cancellationToken).ConfigureAwait(false);
            await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{stopped?.Captures ?? 0} captures in {stopped?.Folder ?? state.Folder}")).ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    // 0 when every draft is written or unchanged, 1 when one was put back.
    private static Command Draft(CliIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var page = new Option<string[]>("--page") { Description = "Only this recorded page (repeatable). Default: every page.", AllowMultipleArgumentsPerToken = true };
        var command = new Command("draft", "Draft page objects from the recorded pages.") { project, solution, page, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var name = ProjectName(service, parsed.GetValue(project), io);
            var pages = parsed.GetValue(page) ?? [];
            var result = pages.Length == 0
                ? await service.DraftAsync(name, page: null, cancellationToken).ConfigureAwait(false)
                : await service.DraftAsync(name, pages, cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(result)).ConfigureAwait(false);
            }
            else
            {
                foreach (var drafted in result.Pages)
                {
                    await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{drafted.Outcome,-9} {drafted.ClassName} ({drafted.Key}): {drafted.Members} members, {drafted.Unstable.Count} left out{(drafted.Reason is { } why ? $"; {why}" : "")}")).ConfigureAwait(false);
                    foreach (var removed in drafted.Removed)
                        await io.Out.WriteLineAsync($"          removed {removed.Name}, last drafted from {removed.LastSeenIn}").ConfigureAwait(false);
                    foreach (var problem in drafted.Problems.Take(5))
                        await io.Out.WriteLineAsync($"          line {problem.Line}: {problem.Code} {problem.Message}").ConfigureAwait(false);
                }

                foreach (var pattern in result.MakeControls)
                    await io.Out.WriteLineAsync($"Make a control: {pattern.Signature} ({pattern.Occurrences} times on {pattern.Pages} pages)").ConfigureAwait(false);
            }

            return result.Pages.Any(p => p.Outcome == DraftOutcome.Reverted) ? 1 : 0;
        });
        return command;
    }

    // 0 when every action resolved, 1 when there are gaps.
    private static Command Resolve(CliIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var session = new Argument<string>("session") { Description = "The test recording's session id (Recordings/<session>)." };
        var command = new Command("resolve", "Resolve a test recording's actions to page-object members, verbs and catalog phrases, and list its gaps.") { session, project, solution, json };
        command.Action(io, async (parsed, _) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var test = service.ResolveActions(ProjectName(service, parsed.GetValue(project), io), parsed.GetValue(session)!);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(test)).ConfigureAwait(false);
            }
            else
            {
                await io.Out.WriteLineAsync($"{test.Name} ({test.Session}), from {test.StartPage} ({test.StartPageObject ?? "no page object"})").ConfigureAwait(false);
                foreach (var step in test.Steps)
                {
                    var said = step.Phrase ?? (step.Member is { } member ? $"{member}.{step.Verb}" : step.Kind);
                    await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"  {step.Event,3} {(step.Gap is null ? "   " : "gap")} {said}")).ConfigureAwait(false);
                    if (step.Gap is { } gap)
                        await io.Out.WriteLineAsync($"          {gap.Kind}: {gap.Message}").ConfigureAwait(false);
                }
            }

            return test.Gaps.Count > 0 ? 1 : 0;
        });
        return command;
    }

    // 0 when drafted with no gap, 1 when a target could not be drafted, a file was not written, or gaps are marked.
    private static Command DraftTest(CliIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var session = new Argument<string>("session") { Description = "The test recording's session id (Recordings/<session>)." };
        var target = new Option<string?>("--target") { Description = "xunit, scenario or both. Default: the config's ## Tests Target." };
        var name = new Option<string?>("--name") { Description = "The test's name. Default: the session's." };
        var command = new Command("draft-test", "Draft a test recording as an xUnit test, a scenario, or both, by rules.") { session, project, solution, target, name, json };
        command.Action(io, async (parsed, _) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var chosen = parsed.GetValue(target) is { } text
                ? TestSettings.ParseTarget(text) ?? throw new ScraperProblem($"--target is xunit, scenario or both, not '{text}'.")
                : (TestTarget?)null;
            var result = service.DraftTest(ProjectName(service, parsed.GetValue(project), io), parsed.GetValue(session)!, chosen, parsed.GetValue(name));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(result)).ConfigureAwait(false);
            }
            else
            {
                foreach (var file in result.Files)
                    await io.Out.WriteLineAsync($"{(file.Written ? "written" : "kept"),-8} {file.Path}{(file.Note is { } note ? $": {note}" : "")}").ConfigureAwait(false);
                foreach (var problem in result.Problems)
                    await io.Out.WriteLineAsync($"not drafted: {problem}").ConfigureAwait(false);
                foreach (var gap in result.Gaps)
                    await io.Out.WriteLineAsync($"gap, event {gap.Event}: {gap.Gap!.Message}").ConfigureAwait(false);
            }

            return result.Problems.Count > 0 || result.Gaps.Count > 0 || result.Files.Any(f => !f.Written && f.Note is { } note && !note.StartsWith("Yours", StringComparison.Ordinal)) ? 1 : 0;
        });
        return command;
    }

    // 0 when every member of every page object is found once, 1 otherwise.
    private static Command Verify(CliIo io)
    {
        var (solution, project, json, headless) = (Solution(), Project(), Json(), Headless());
        var command = new Command("verify", "Look the page objects' members up in the running app.") { project, solution, headless, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var result = await service.VerifyAsync(ProjectName(service, parsed.GetValue(project), io), visible: !parsed.GetValue(headless) && io.Interactive, cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(result)).ConfigureAwait(false);
            }
            else
            {
                foreach (var page in result.Pages)
                {
                    var failed = page.Members.Where(m => m.Verdict is MemberVerdict.NotFound or MemberVerdict.Several).ToArray();
                    await io.Out.WriteLineAsync(page.Problem is { } problem
                        ? $"FAIL {page.ClassName} ({page.Key}): {problem}"
                        : string.Create(CultureInfo.InvariantCulture, $"{(failed.Length == 0 ? "ok  " : "FAIL")} {page.ClassName} ({page.Key}): {page.Members.Count - failed.Length} of {page.Members.Count} found once")).ConfigureAwait(false);
                    foreach (var member in failed)
                        await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"     {member.Name}: {member.Locator} found {member.Count} times")).ConfigureAwait(false);
                }
            }

            return result.Passed ? 0 : 1;
        });
        return command;
    }

    private static Command Config(CliIo io)
    {
        var get = new Command("get", "Print the project's scraper.config.md.");
        var (getSolution, getProject) = (Solution(), Project());
        get.Add(getProject);
        get.Add(getSolution);
        get.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(getSolution));
            var project = service.Project(ProjectName(service, parsed.GetValue(getProject), io));
            await io.Out.WriteAsync(await File.ReadAllTextAsync(Path.Combine(project.Folder, ScraperConfig.FileName), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
            return 0;
        });

        return new Command("config", "Read or change the project's scraper.config.md.")
        {
            get,
            Change(io, "set", "Set a field: bruinet scrape config set Explore MaxPages 20.", "field and value"),
            Change(io, "add", "Add a row: bruinet scrape config add \"Explore hosts\" shop.example.", "the row's cells"),
            Change(io, "remove", "Remove a row by its first cell: bruinet scrape config remove \"Explore hosts\" shop.example.", "the row's first cell"),
        };
    }

    private static Command Change(CliIo io, string action, string description, string valuesAre)
    {
        var section = new Argument<string>("section") { Description = "The section, as its ## heading: App, Explore, Explore hosts, Never click and so on" };
        var values = new Argument<string[]>("values") { Description = $"For {action}: {valuesAre}.", Arity = ArgumentArity.OneOrMore };
        var (solution, project) = (Solution(), Project());
        var command = new Command(action, description) { section, values, project, solution };
        command.Action(io, async (parsed, _) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var edits = service.EditConfig(ProjectName(service, parsed.GetValue(project), io), [new ConfigChange(action, parsed.GetValue(section)!, parsed.GetValue(values)!)]);
            foreach (var edit in edits)
                await io.Out.WriteLineAsync(edit.Changed ? edit.Description : $"Unchanged: {edit.Description}").ConfigureAwait(false);
            return 0;
        });
        return command;
    }

    private static Command InstallBrowser(CliIo io)
    {
        var command = new Command("install-browser", "Install the Chromium the Scraper drives (once per machine).");
        command.Action(io, async (_, _) =>
        {
            await io.Error.WriteLineAsync("Installing Chromium for Playwright.").ConfigureAwait(false);
            return await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"])).ConfigureAwait(false) == 0 ? 0 : 2;
        });
        return command;
    }

    private static ScraperService Service(CliIo io, string? solution) =>
        new(SolutionFolder(solution, io.CurrentDirectory), io.SourceFor, new Generate.ControlTemplates());

    // Not given: the project this folder is in (by its folder, so a name two projects share still picks one), else the service's own choice.
    private static string? ProjectName(ScraperService service, string? given, CliIo io)
    {
        if (given is not null)
            return given;
        var here = Path.TrimEndingDirectorySeparator(Path.GetFullPath(io.CurrentDirectory)) + Path.DirectorySeparatorChar;
        var inside = service.Projects()
            .Where(p => here.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(p.Folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .MaxBy(p => p.Folder.Length);
        return inside is null ? null : service.Relative(inside);
    }

    private static Option<int?> Positive(string name, string description)
    {
        var option = new Option<int?>(name) { Description = description };
        option.Validators.Add(result =>
        {
            // A token that is not a number is the parser's error already.
            if (result.Tokens is [var token] && int.TryParse(token.Value, CultureInfo.InvariantCulture, out var number) && number <= 0)
                result.AddError($"{name} takes a whole number above 0.");
        });
        return option;
    }

    // Progress as a line each, as it happens.
    private sealed class Lines(TextWriter errors) : IProgress<ExploreProgress>
    {
        public void Report(ExploreProgress p) =>
            errors.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[{p.Captures} captured, {p.Clicks} clicks, {p.Skipped} skipped, {p.Blocked} blocked] {p.Doing}"));
    }
}
