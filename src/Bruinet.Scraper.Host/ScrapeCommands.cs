using System.CommandLine;
using System.Globalization;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using static Bruinet.Scraper.Host.HostOptions;

namespace Bruinet.Scraper.Host;

/// <summary>
/// <c>bruinet scrape</c>: the Scraper for a person, the loop and CI, through the service MCP uses.
/// Exit 0 when it ran within its guards, 1 when a guard stopped it or a check failed, 2 when it could not run.
/// </summary>
public static partial class ScrapeCommands
{
    /// <summary>The <c>scrape</c> command with its subcommands.</summary>
    public static Command Build(HostIo io) => new("scrape", "The Scraper: explore or walk an app, then draft and verify page objects (docs/guides/scraper.md).")
    {
        New(io), List(io), Check(io), Explore(io), Draft(io), Verify(io), Find(io), Look(io), Config(io), Places(io), Factory(io), InstallBrowser(io),
    };

    private static Command New(HostIo io)
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

    private static Command List(HostIo io)
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

    private static Command Check(HostIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var headless = Headless();
        var command = new Command("check", "Open the start URL: does the app answer, signed in, on a host Explore may go to.") { project, solution, headless, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var check = await service.CheckAppAsync(ProjectName(service, parsed.GetValue(project), io), !parsed.GetValue(headless) && io.Interactive, cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
                await io.Out.WriteLineAsync(ToJson(check)).ConfigureAwait(false);
            else
                await io.Out.WriteLineAsync($"{check.Url}: {check.Detail}").ConfigureAwait(false);
            return check.Status is >= 200 and < 400 && !check.SignIn && check.Refused is null ? 0 : 1;
        });
        return command;
    }

    private static Command Explore(HostIo io)
    {
        var (solution, project, json, yes, headless) = (Solution(), Project(), Json(), Yes(), Headless());
        var url = new Option<string[]>("--url") { Description = "Where to start. Default: the config's StartUrl. Several: a run for each, one after another, each kept within its own path unless --within says otherwise.", AllowMultipleArgumentsPerToken = false };
        var dryRun = new Option<bool>("--dry-run") { Description = "Follow links only, and list what it would click." };
        var maxPages = Positive("--max-pages", "At most this many pages.");
        var maxDepth = Positive("--max-depth", "At most this many clicks from the start.");
        var maxMinutes = Positive("--max-minutes", "Stop after this many minutes.");
        var delayMs = Positive("--delay-ms", "Wait this long between clicks.");
        var within = new Option<string?>("--within") { Description = "Keep to links under this path or route (/contact, /#/orders) for this run. Default: the config's Within." };
        var command = new Command("explore", "Explore the app by itself, under the project's guards, capturing each page.")
        {
            project, solution, url, within, dryRun, maxPages, maxDepth, maxMinutes, delayMs, headless, yes, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            var unattended = parsed.GetValue(yes) || !io.Interactive;
            var noWindow = parsed.GetValue(headless) || !io.Interactive;
            var starts = parsed.GetValue(url) is { Length: > 0 } given ? given : [null!];
            var options = new ExploreOptions
            {
                DryRun = parsed.GetValue(dryRun),
                Url = starts[0],
                MaxPages = parsed.GetValue(maxPages),
                MaxDepth = parsed.GetValue(maxDepth),
                MaxMinutes = parsed.GetValue(maxMinutes),
                DelayMs = parsed.GetValue(delayMs),
                Within = parsed.GetValue(within),
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

            // Several starts: a run for each, each within its own path, so a module is explored route by route with
            // one command and no change to the config between them.
            var withinGuards = true;
            var results = new List<ExploreResult>();
            foreach (var start in starts)
            {
                var run = starts.Length == 1 ? options : options with { Url = start, Within = options.Within ?? WithinOf(start) };
                if (starts.Length > 1)
                    await io.Error.WriteLineAsync($"--- {start}").ConfigureAwait(false);
                var result = await service.ExploreAsync(name, run, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                withinGuards &= result.WithinGuards;
                if (parsed.GetValue(json))
                    continue;
                await io.Out.WriteLineAsync($"{result.Stop}: {result.StopDetail}").ConfigureAwait(false);
                await io.Out.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"{result.Captures.Count} captures, {result.Clicks} clicks, {result.Skipped.Count} skipped, {result.Blocked.Count} blocked in {result.Took.TotalSeconds:0.0} s")).ConfigureAwait(false);
                if (result.Report is not null)
                    await io.Out.WriteLineAsync($"Report: {result.Report}").ConfigureAwait(false);
            }

            if (parsed.GetValue(json))
                await io.Out.WriteLineAsync(results.Count == 1 ? ToJson(results[0]) : ToJson(results)).ConfigureAwait(false);
            return withinGuards ? 0 : 1;
        });
        return command;
    }

    private static Command Draft(HostIo io)
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

    // The path a start is kept within when several are given: its own path, without a route or a query.
    internal static string? WithinOf(string? start)
        => start is null ? null
            : Uri.TryCreate(start, UriKind.Absolute, out var full) ? full.AbsolutePath
            : start.Split('#', '?')[0] is { Length: > 1 } path && path.StartsWith('/') ? path
            : null;

    // 0 when found, 1 when nothing holds the text.
    private static Command Find(HostIo io)
    {
        var (solution, project, json) = (Solution(), Project(), Json());
        var text = new Argument<string>("text") { Description = "What to look for, in any case: a text, a placeholder, an attribute's value." };
        var run = new Option<string?>("--run") { Description = "Only this run's folder under Recordings. Default: every run." };
        var command = new Command("find", "Find a text in the recorded pages, shadow roots included, with the path to each node.") { text, project, solution, run, json };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var scrapeProject = service.Project(ProjectName(service, parsed.GetValue(project), io));
            var hits = Bruinet.Scraper.Recordings.RecordingSearch.Find(scrapeProject.RecordingsFolder, parsed.GetValue(text)!, parsed.GetValue(run));
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(hits)).ConfigureAwait(false);
            }
            else
            {
                foreach (var hit in hits)
                {
                    await io.Out.WriteLineAsync($"{hit.File}#{hit.Node} {hit.Field}=\"{hit.Value}\"").ConfigureAwait(false);
                    await io.Out.WriteLineAsync($"    {hit.Path}").ConfigureAwait(false);
                }

                await io.Out.WriteLineAsync($"{hits.Count} found").ConfigureAwait(false);
            }

            return hits.Count > 0 ? 0 : 1;
        });
        return command;
    }

    // 0 when the page opened and every click landed, 1 when something stopped it.
    private static Command Look(HostIo io)
    {
        var (solution, project, json, headless) = (Solution(), Project(), Json(), Headless());
        var url = new Option<string?>("--url") { Description = "The address, full or a path on the start URL's site. Default: the config's StartUrl." };
        var click = new Option<string[]>("--click") { Description = "Click this before looking, as the page says it (\"+ Contact\"), field: and the label of a field (field:Status, which opens its list), or css: and a selector. Several, in order.", AllowMultipleArgumentsPerToken = false };
        var waitMs = Positive("--wait-ms", "How long the page gets to settle after it opens and after each click. Default: 2000.");
        var asAccount = new Option<string?>("--as") { Description = "Look as this account: its saved sign-in beside the project's own (Reader is Reader.json there). Default: the project's own." };
        var calls = new Option<bool>("--calls") { Description = "Also list the calls the page made (method, address, status, and what a writing call sent): after the last click, or on opening when nothing is clicked. No header is shown." };
        var command = new Command("look", "Open a page as the project's saved sign-in, click what is asked, and say what it shows: headings, buttons, fields, messages. Under the project's guards; nothing is typed or saved.")
        {
            project, solution, url, click, asAccount, waitMs, calls, headless, json,
        };
        command.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(solution));
            var options = new Bruinet.Scraper.Service.LookOptions
            {
                As = parsed.GetValue(asAccount),
                Url = parsed.GetValue(url),
                Clicks = parsed.GetValue(click) ?? [],
                Visible = !parsed.GetValue(headless) && io.Interactive,
                WaitMs = parsed.GetValue(waitMs) ?? 2000,
                Calls = parsed.GetValue(calls),
            };
            var look = await service.LookAsync(ProjectName(service, parsed.GetValue(project), io), options, cancellationToken).ConfigureAwait(false);
            if (parsed.GetValue(json))
            {
                await io.Out.WriteLineAsync(ToJson(look)).ConfigureAwait(false);
            }
            else
            {
                foreach (var step in look.Steps)
                    await io.Out.WriteLineAsync(step.Refused is null ? $"clicked '{step.Clicked}'" : $"'{step.Asked}': {step.Refused}").ConfigureAwait(false);
                if (look.Facts is not null)
                    await io.Out.WriteLineAsync(look.Facts.ToText()).ConfigureAwait(false);
                foreach (var call in look.Calls)
                    await io.Out.WriteLineAsync("Call: " + call.ToText()).ConfigureAwait(false);
                if (look.Problem is not null)
                    await io.Error.WriteLineAsync(look.Problem).ConfigureAwait(false);
            }

            return look.Problem is null ? 0 : 1;
        });
        return command;
    }

    // 0 when every action resolved, 1 when there are gaps.
    private static Command Verify(HostIo io)
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

    private static Command Config(HostIo io)
    {
        var get = new Command("get", "Print the project's scraper.config.md.");
        var (getSolution, getProject) = (Solution(), Project());
        get.Add(getProject);
        get.Add(getSolution);
        get.Action(io, async (parsed, cancellationToken) =>
        {
            await using var service = Service(io, parsed.GetValue(getSolution));
            var project = service.Project(ProjectName(service, parsed.GetValue(getProject), io));
            await io.Out.WriteAsync(await File.ReadAllTextAsync(Path.Combine(project.Home, ScraperConfig.FileName), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
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

    private static Command Change(HostIo io, string action, string description, string valuesAre)
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

    private static Command InstallBrowser(HostIo io)
    {
        var command = new Command("install-browser", "Install the Chromium the Scraper drives (once per machine).");
        command.Action(io, async (_, _) =>
        {
            await io.Error.WriteLineAsync("Installing Chromium for Playwright.").ConfigureAwait(false);
            return await Task.Run(() => Microsoft.Playwright.Program.Main(["install", "chromium"])).ConfigureAwait(false) == 0 ? 0 : 2;
        });
        return command;
    }

    private static ScraperService Service(HostIo io, string? solution) =>
        new(SolutionFolder(solution, io.CurrentDirectory), io.SourceFor, io.Services.Templates);

    // Not given: the project this folder is in (by its folder, so a name two projects share still picks one), else the service's own choice.
    private static string? ProjectName(ScraperService service, string? given, HostIo io)
    {
        if (given is not null)
            return given;
        var here = Path.TrimEndingDirectorySeparator(Path.GetFullPath(io.CurrentDirectory)) + Path.DirectorySeparatorChar;
        var inside = service.Projects()
            .Where(p => Under(here, p.Folder) || Under(here, p.Home))
            .MaxBy(p => Under(here, p.Home) ? p.Home.Length : p.Folder.Length);
        return inside is null ? null : service.Relative(inside);
    }

    private static bool Under(string here, string folder) =>
        here.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

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
