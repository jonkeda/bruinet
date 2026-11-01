using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Testing;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Scraper.Service;

public sealed partial class ScraperService
{
    /// <summary>
    /// A project's test recordings (sessions with actions), each resolved as the page objects are now, with
    /// the drafts made from it and whether its recording changed since.
    /// </summary>
    public IReadOnlyList<RecordedTest> Tests(string? projectName)
    {
        var project = Project(projectName);
        var drafts = Drafts(project);
        return project.Sessions()
            .Where(s => s.Events.Any(e => e.IsAction))
            .Select(session =>
            {
                var hash = session.Hash();
                var resolved = TestResolver.Resolve(project, session);
                var mine = drafts.Where(d => d.Header.Session == session.Id)
                    .Select(d => new DraftedTest(d.Path, d.Target, d.Class, d.Header.Hash != hash))
                    .ToArray();
                return new RecordedTest(session.Id, session.Name, resolved.Steps.Count, resolved, mine);
            })
            .OrderByDescending(t => t.Session, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>A recording session's events: its pages and, in a test recording, its actions.</summary>
    /// <exception cref="ScraperProblem">No such session.</exception>
    public IReadOnlyList<RecordingEvent> RecordedActions(string? projectName, string session) => SessionOf(Project(projectName), session).Events;

    /// <summary>A gap of a test recording as a brief for the agent (<see cref="TestBrief"/>); the first gaps' briefs when no event is given.</summary>
    /// <exception cref="ScraperProblem">No such session, or the event is no gap.</exception>
    public IReadOnlyList<string> TestBriefs(string? projectName, string session, int? @event = null)
    {
        var project = Project(projectName);
        var recorded = SessionOf(project, session);
        var test = TestResolver.Resolve(project, recorded);
        try
        {
            return @event is { } number ? [TestBrief.Write(project, recorded, test, number)] : TestBrief.WriteFirst(project, recorded, test);
        }
        catch (ArgumentException e)
        {
            throw new ScraperProblem(e.Message);
        }
    }

    private static RecordingSession SessionOf(ScrapeProject project, string session) =>
        project.Session(session) ?? throw new ScraperProblem($"{project.Name} has no recording session '{session}'. Its sessions are {string.Join(", ", project.Sessions().Select(s => s.Id))}.");

    /// <summary>A project's <c>.csproj</c>, which an xUnit draft runs in.</summary>
    /// <exception cref="ScraperProblem">It has none.</exception>
    public string ProjectFile(string? projectName)
    {
        var project = Project(projectName);
        return Directory.EnumerateFiles(project.Folder, "*.csproj").FirstOrDefault()
            ?? throw new ScraperProblem($"{project.Folder} has no project file.");
    }

    // The drafted files in the project's recorded-test folders, by the recording they name.
    private static IReadOnlyList<(string Path, TestTarget Target, string? Class, RecordedHeader Header)> Drafts(ScrapeProject project)
    {
        var found = new List<(string, TestTarget, string?, RecordedHeader)>();
        foreach (var (folder, pattern, target) in new[] { (TestDrafter.TestsFolder, "*" + TestDrafter.Extension, TestTarget.Xunit), (TestDrafter.FeaturesFolder, "*.feature.md", TestTarget.Scenario) })
        {
            var full = Path.Combine(project.Folder, folder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full))
                continue;
            foreach (var file in Directory.EnumerateFiles(full, pattern))
            {
                var text = File.ReadAllText(file);
                if (RecordedHeader.Find(text) is { } header)
                    found.Add((file, target, target == TestTarget.Xunit ? ClassOf(text) : null, header));
            }
        }
        return found;
    }

    // An xUnit draft's class, with its namespace: what its run filters on.
    private static string? ClassOf(string code)
    {
        var type = CSharpSyntaxTree.ParseText(code).GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (type is null)
            return null;
        var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        return ns is null ? type.Identifier.Text : $"{ns}.{type.Identifier.Text}";
    }

    private WorkflowStep TestsStep(string projectName)
    {
        var tests = Tests(projectName);
        var open = tests.Count(t => t.Resolved.Gaps.Count > 0 || t.Drafts.Count == 0 || t.Drafts.Any(d => d.Stale));
        return tests.Count == 0 ? Step(8, StepState.Todo, "")
            : open > 0 ? Step(8, StepState.Attention, $"{open} to finish", open)
            : Step(8, StepState.Done, $"{tests.Count} test{S(tests.Count)}");
    }
}

/// <summary>A test recording: its session, how many actions, how they resolve, and its drafts.</summary>
public sealed record RecordedTest(string Session, string Name, int Actions, ResolvedTest Resolved, IReadOnlyList<DraftedTest> Drafts);

/// <summary>A draft made from a test recording: its file, its target, its class (an xUnit draft's), and whether the recording changed since.</summary>
public sealed record DraftedTest(string Path, TestTarget Target, string? Class, bool Stale);
