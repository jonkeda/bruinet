using Bruinet.Bdd;
using Bruinet.Core.Scraping;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Service;

// The view's workflow: where a project is (progress), whether the app can be reached (check), the
// controls made and their check, and the page objects drafted by choice and renamed.
public sealed partial class ScraperService
{
    /// <summary>The workflow's seven steps for a project: each one's state and count, and what to do next.</summary>
    public WorkflowProgress Progress(string? projectName)
    {
        var project = Project(projectName);
        var state = WorkflowState.Read(project);
        var index = project.Index();
        var captures = index.Pages.Sum(p => p.Captures.Count);
        var objects = PageObjects(project.Name);
        var changed = objects.Count(o => o.Value.Changed == true);
        var without = objects.Count(o => o.Value.PageObject is null);
        var toMake = DraftRun.ControlsToMake(Patterns(project.Name));
        var controls = ControlChecker.Check(project, state.AcceptedControls.ToHashSet(StringComparer.Ordinal));
        var toFix = controls.Count(c => !c.Done);

        var check = state.LastCheck;
        var connect = check is null
            ? (index.Pages.Count > 0 ? Step(2, StepState.Done, "pages recorded") : Step(2, StepState.Todo, ""))
            : check.Passed ? Step(2, StepState.Done, check.Status is { } s ? $"answered {s}" : "answered")
            : Step(2, StepState.Attention, check.Refused is not null ? "host refused" : check.SignIn ? "sign in" : "no answer");
        var collect = index.Pages.Count == 0 ? Step(3, StepState.Todo, "")
            : changed > 0 ? Step(3, StepState.Attention, $"{changed} page{S(changed)} changed", changed)
            : Step(3, StepState.Done, $"{index.Pages.Count} page{S(index.Pages.Count)}, {captures} capture{S(captures)}");
        var review = index.Pages.Count == 0 ? Step(4, StepState.Todo, "") : Step(4, StepState.Done, $"{index.Pages.Count} page{S(index.Pages.Count)}");
        var controlsStep = index.Pages.Count == 0 ? Step(5, StepState.Todo, "")
            : toFix > 0 ? Step(5, StepState.Attention, $"{controls.Count} made, {toFix} to fix", toFix)
            : toMake.Count > 0 && !state.SkipControls ? Step(5, StepState.Todo, $"{toMake.Count} to make", toMake.Count)
            : Step(5, StepState.Done, state.SkipControls && toMake.Count > 0 ? "skipped" : $"{controls.Count} control{S(controls.Count)}");
        var pagesStep = index.Pages.Count == 0 ? Step(6, StepState.Todo, "")
            : changed > 0 ? Step(6, StepState.Attention, $"{changed} changed since", changed)
            : without > 0 ? Step(6, StepState.Todo, $"{without} without one", without)
            : Step(6, StepState.Done, $"{objects.Count} page object{S(objects.Count)}");
        var verify = state.LastVerify is not { } last ? Step(7, StepState.Todo, "")
            : last.Passed ? Step(7, StepState.Done, $"{last.Found} of {last.Members} found")
            : Step(7, StepState.Attention, $"{last.Problems} not found", last.Problems);
        var steps = new List<WorkflowStep> { Step(1, StepState.Done, project.Config.Platform), connect, collect, review, controlsStep, pagesStep, verify, TestsStep(project.Name) };

        // The next step: the first that needs attention or is to do; changed pages go back to collecting.
        var next = steps.FirstOrDefault(s => s.State == StepState.Attention) ?? steps.FirstOrDefault(s => s.State == StepState.Todo);
        if (next is not null)
            steps[next.Step - 1] = next with { State = next.State == StepState.Attention ? StepState.Attention : StepState.Next };
        return new WorkflowProgress(project.Name, steps, next is null ? null : new NextAction(next.Step, NextText(next, toMake.Count, toFix, changed, without, check)))
        {
            LastCheck = check,
            LastVerify = state.LastVerify,
            SkipControls = state.SkipControls,
        };
    }

    /// <summary>Opens the start URL in a browser of its own, with the project's profile: did it answer, signed in, on a host Explore may go to. Remembered for the workflow.</summary>
    public async Task<AppCheck> CheckAppAsync(string? projectName, bool visible = false, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        if (_recording is not null)
            throw new ScraperProblem("A recording is in progress, and it holds the project's browser profile: stop it first.");
        if (_sourceFor(project.Config.Platform) is not PlaywrightScrapeSource source)
            throw new ScraperProblem($"{project.Config.Platform} apps cannot be checked yet: html and blazor can.");
        var url = project.Config.StartUrl;
        AppCheck check;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var start))
        {
            check = new AppCheck(DateTimeOffset.Now, url, null, null, false, null, $"'{url}' is not an address: set the start URL.");
        }
        else
        {
            var refused = RequestGuard.RefuseHost(start, project.Config.Explore);
            await using (source.ConfigureAwait(false))
            {
                await source.StartAsync(new ScrapeStart("about:blank", project.ProfileFolder, visible), cancellationToken).ConfigureAwait(false);
                await source.GuardAsync(new RequestGuard(project.Config.Explore with { Hosts = [.. project.Config.Explore.Hosts, start.Host] })).ConfigureAwait(false);
                int? status = null;
                string? detail = null;
                try
                {
                    var response = await source.Page!.GotoAsync(url, PlaywrightScrapeSource.Opening).ConfigureAwait(false);
                    status = response?.Status;
                }
                catch (Exception e) when (e is Microsoft.Playwright.PlaywrightException or TimeoutException)
                {
                    detail = $"It did not answer: {e.Message.Split('\n')[0]}";
                }
                if (status is null)
                {
                    check = new AppCheck(DateTimeOffset.Now, url, null, null, false, refused, detail ?? "It did not answer.");
                }
                else
                {
                    var snapshot = await source.CaptureAsync(cancellationToken).ConfigureAwait(false);
                    var hasPassword = snapshot.Nodes.Any(n => n.Type == "input" && n.Attribute("type") == "password" && n.Bounds is not null);
                    var signIn = hasPassword && (!project.IsOwnPage(snapshot.Location) || PageKey.Of(snapshot.Location) != PageKey.Of(url)
                        || (project.Config.Explore.LoginUrl.Length > 0 && snapshot.Location.Contains(project.Config.Explore.LoginUrl, StringComparison.OrdinalIgnoreCase)));
                    detail = status >= 400 ? $"It answered {status}."
                        : signIn ? $"A sign-in page came ({snapshot.Location}): sign in in the browser once, the profile keeps it."
                        : refused is not null ? $"It answered {status}, but {refused}"
                        : $"It answered {status}, signed in, and Explore may go there.";
                    check = new AppCheck(DateTimeOffset.Now, url, status, snapshot.Location, signIn, refused, detail);
                }
            }
        }
        var state = WorkflowState.Read(project);
        (state with { LastCheck = check }).Write(project);
        return check;
    }

    /// <summary>The project's made controls, each checked against the recordings.</summary>
    public IReadOnlyList<ControlCheck> ControlChecks(string? projectName)
    {
        var project = Project(projectName);
        return ControlChecker.Check(project, WorkflowState.Read(project).AcceptedControls.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>A C# file of the project, by its path relative to the project: a control's code to review.</summary>
    public string ReadCode(string? projectName, string path)
    {
        var project = Project(projectName);
        var full = Path.GetFullPath(Path.Combine(project.Folder, path));
        if (!full.StartsWith(project.Folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            throw new ScraperProblem($"{path} is not a C# file of {project.Name}.");
        return File.ReadAllText(full);
    }

    /// <summary>Accepts a control as it is (or takes that back), so the workflow does not ask for it again.</summary>
    /// <summary>The agent and model the Scraper's prompts run with: the person's choice, else the config's.</summary>
    public ChatChoice ChatChoice(string? projectName)
    {
        var project = Project(projectName);
        var state = WorkflowState.Read(project);
        return new ChatChoice(state.Agent ?? project.Config.ChatAgent, state.Model ?? project.Config.ChatModel);
    }

    /// <summary>Remembers the person's agent and model, in the project's state (not committed).</summary>
    public void SetChatChoice(string? projectName, string? agent, string? model)
    {
        var project = Project(projectName);
        (WorkflowState.Read(project) with { Agent = agent, Model = model }).Write(project);
    }

    public void AcceptControl(string? projectName, string control, bool accepted = true)
    {
        var project = Project(projectName);
        var state = WorkflowState.Read(project);
        var list = state.AcceptedControls.Where(c => c != control).ToList();
        if (accepted)
            list.Add(control);
        (state with { AcceptedControls = list }).Write(project);
    }

    /// <summary>Page objects without controls: the workflow stops asking for them (or asks again).</summary>
    public void SkipControls(string? projectName, bool skip = true)
    {
        var project = Project(projectName);
        (WorkflowState.Read(project) with { SkipControls = skip }).Write(project);
    }

    /// <summary>Several changes to the config, written once when all of them read: the settings form's Save.</summary>
    public IReadOnlyList<ConfigEdit> EditConfig(string? projectName, IReadOnlyList<ConfigChange> changes)
    {
        var project = Project(projectName);
        var path = Path.Combine(project.Folder, ScraperConfig.FileName);
        var text = File.ReadAllText(path);
        var edits = new List<ConfigEdit>();
        try
        {
            foreach (var change in changes)
            {
                var edit = ScraperConfigWriter.Apply(text, change);
                edits.Add(edit);
                text = edit.Text;
            }
            ScraperConfig.Parse(text);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            throw new ScraperProblem($"{project.Name}'s config was not changed: {e.Message}");
        }
        if (edits.Any(e => e.Changed))
            File.WriteAllText(path, text);
        return edits;
    }

    /// <summary>Drafts the page objects of the pages with these keys only.</summary>
    public async Task<DraftResult> DraftAsync(string? projectName, IReadOnlyCollection<string> pages, CancellationToken cancellationToken = default)
    {
        var project = Project(projectName);
        DraftResult result;
        try
        {
            result = await DraftRun.RunAsync(project, pages, _templates, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException e)
        {
            throw new ScraperProblem(e.Message);
        }
        return result with { MakeControls = DraftRun.ControlsToMake(Patterns(project.Name)) };
    }

    /// <summary>
    /// Renames a page's page object: the class in its draft and in the person's part, and the files named
    /// after it. A later draft keeps the name (it is found by its header's page). Code elsewhere that uses
    /// the class is not changed: the IDE's rename does that.
    /// </summary>
    public RenameResult RenamePageObject(string? projectName, string page, string name)
    {
        var project = Project(projectName);
        if (!SyntaxFacts.IsValidIdentifier(name))
            throw new ScraperProblem($"'{name}' is not a class name.");
        var code = ProjectCode.Read(project).Where(c => c.Project == project.Name).ToArray();
        var target = code.FirstOrDefault(c => c.Kind == CodeKind.Page && c.Header?.Page == page)
            ?? throw new ScraperProblem($"No page object for '{page}' to rename.");
        var old = target.Name;
        if (old == name)
            return new RenameResult(old, name, [], []);
        if (code.Any(c => c.Name.Split('<')[0] == name))
            throw new ScraperProblem($"{project.Name} has a class {name} already.");

        var changed = new List<string>();
        var usedElsewhere = new List<string>();
        foreach (var file in Directory.EnumerateFiles(project.Folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(project.Folder, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ScrapeProject.RecordingsFolderName)
                continue;
            var text = File.ReadAllText(file);
            var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
            var declares = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(c => c.Identifier.Text == old && Namespace(c) == target.Namespace);
            var tokens = root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && t.Text == old).ToArray();
            if (tokens.Length == 0)
                continue;
            if (!declares)
            {
                usedElsewhere.Add(relative);
                continue;
            }
            var renamed = root.ReplaceTokens(tokens, (t, _) => SyntaxFactory.Identifier(t.LeadingTrivia, name, t.TrailingTrivia));
            var target2 = Path.GetFileName(file) is var fileName && fileName.StartsWith(old + ".", StringComparison.Ordinal)
                ? Path.Combine(Path.GetDirectoryName(file)!, name + fileName[old.Length..])
                : file;
            File.WriteAllText(target2, renamed.ToFullString());
            if (target2 != file)
                File.Delete(file);
            changed.Add(Path.GetRelativePath(project.Folder, target2));
        }
        return new RenameResult(old, name, changed, usedElsewhere);
    }

    // Verify's result for the workflow: found once of all (members made by a control's own code do not count against it).
    private static void Remember(ScrapeProject project, VerifyResult result)
    {
        var members = result.Pages.SelectMany(p => p.Members).ToArray();
        var problems = members.Count(m => m.Verdict is MemberVerdict.NotFound or MemberVerdict.Several) + result.Pages.Count(p => p.Problem is not null);
        var summary = new VerifySummary(DateTimeOffset.Now, result.Passed, members.Count(m => m.Verdict == MemberVerdict.Once), members.Length, problems);
        (WorkflowState.Read(project) with { LastVerify = summary }).Write(project);
    }

    private static string Namespace(SyntaxNode node) =>
        node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";

    private static WorkflowStep Step(int step, StepState state, string summary, int? count = null) => new(step, StepNames[step - 1], state, summary, count);

    private static readonly string[] StepNames = ["Project", "Connect", "Collect pages", "Review", "Controls", "Page objects", "Verify", "Tests"];

    private static string S(int n) => n == 1 ? "" : "s";

    private static string NextText(WorkflowStep step, int toMake, int toFix, int changed, int without, AppCheck? check) => step.Step switch
    {
        2 when check is { SignIn: true } => "Sign in to the app in the browser",
        2 when check is { Refused: not null } => "Allow the app's host for Explore",
        2 when check is not null => "Check the app again: it did not answer",
        2 => "Check that the Scraper can reach the app",
        3 when changed > 0 => $"Re-record the {changed} changed page{S(changed)}, then update the page object{S(changed)}",
        3 => "Explore the app to collect its pages",
        4 => "Look at what was recorded",
        5 when toFix > 0 => $"Review the {toFix} control{S(toFix)} to fix",
        5 => $"Make the {toMake} control{S(toMake)} Draft asked for",
        6 when changed > 0 => $"Update the {changed} page object{S(changed)} whose page changed",
        6 => $"Draft page objects for the {without} page{S(without)} without one",
        7 when step.State == StepState.Attention => "Fix or re-record the members Verify did not find",
        7 => "Verify the page objects against the running app",
        8 when step.State == StepState.Attention => $"Finish the {step.Count} recorded test{S(step.Count ?? 0)}: fill the gaps, draft, run once",
        _ => "Record a test: use the app, assert what it shows",
    };
}

/// <summary>Where a step stands.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StepState>))]
public enum StepState
{
    /// <summary>Not done yet.</summary>
    Todo,

    /// <summary>The step to do next.</summary>
    Next,

    /// <summary>Done.</summary>
    Done,

    /// <summary>Something to look at: a guard stopped it, a page changed, members not found.</summary>
    Attention,
}

/// <summary>One step of the workflow: its number and name, its state, a short summary, and a count for its badge.</summary>
public sealed record WorkflowStep(int Step, string Name, StepState State, string Summary, int? Count);

/// <summary>The step to go to next, and what to do there.</summary>
public sealed record NextAction(int Step, string Text);

/// <summary>A project's workflow: its steps and what to do next (null when everything is done).</summary>
public sealed record WorkflowProgress(string Project, IReadOnlyList<WorkflowStep> Steps, NextAction? Next)
{
    /// <summary>The last app check, for step 2.</summary>
    public AppCheck? LastCheck { get; init; }

    /// <summary>The last verify, for step 7.</summary>
    public VerifySummary? LastVerify { get; init; }

    /// <summary>The person chose page objects without controls.</summary>
    public bool SkipControls { get; init; }
}

/// <summary>A page object renamed: the old and new name, the files changed, and the files elsewhere that use the old name.</summary>
public sealed record RenameResult(string From, string To, IReadOnlyList<string> Files, IReadOnlyList<string> UsedElsewhere);
