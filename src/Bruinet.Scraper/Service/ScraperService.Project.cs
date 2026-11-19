using Bruinet.Bdd;
using Bruinet.Scraper.Snapshots;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Service;

// Whether the app can be reached (check), the controls made and their check, and the page objects drafted by
// choice and renamed.
public sealed partial class ScraperService
{
    /// <summary>Opens the start URL in a browser of its own, with the project's profile: did it answer, signed in, on a host Explore may go to.</summary>
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
                await source.StartAsync(new ScrapeStart("about:blank", project.ProfileFolder, visible) { SignInState = project.SignInStatePath }, cancellationToken).ConfigureAwait(false);
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
        return check;
    }

    /// <summary>The project's made controls, each checked against the recordings.</summary>
    public IReadOnlyList<ControlCheck> ControlChecks(string? projectName)
    {
        var project = Project(projectName);
        return ControlChecker.Check(project, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>Several changes to the config, written once when all of them read.</summary>
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

    private static string Namespace(SyntaxNode node) =>
        node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";

}

/// <summary>A page object renamed: the old and new name, the files changed, and the files elsewhere that use the old name.</summary>
public sealed record RenameResult(string From, string To, IReadOnlyList<string> Files, IReadOnlyList<string> UsedElsewhere);

/// <summary>Whether the app could be reached: answered, signed in, on a host Explore may go to.</summary>
/// <param name="At">When it was checked.</param>
/// <param name="Url">The address checked.</param>
/// <param name="Status">The HTTP status, or null when nothing answered.</param>
/// <param name="Location">Where the browser ended up.</param>
/// <param name="SignIn">A sign-in page came instead.</param>
/// <param name="Refused">Why Explore may not go there, or null.</param>
/// <param name="Detail">What the check found, in a sentence.</param>
public sealed record AppCheck(DateTimeOffset At, string Url, int? Status, string? Location, bool SignIn, string? Refused, string Detail)
{
    /// <summary>Answered, signed in, and allowed.</summary>
    public bool Passed => Status is >= 200 and < 400 && !SignIn && Refused is null;
}
