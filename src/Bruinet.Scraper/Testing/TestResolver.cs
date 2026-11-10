using Bruinet.Bdd;
using System.Globalization;
using Bruinet.Core.Scraping;
using Bruinet.Core.Testing;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;

namespace Bruinet.Scraper.Testing;

/// <summary>
/// Resolves a test recording's actions, from files only (R3): each action's node, in the capture it is in,
/// to the page object of its page (by the page objects' headers), the deepest member whose locator matches
/// the node or an ancestor (<see cref="SnapshotSelector"/>), the verb the member's control has for the
/// action, and the verb's catalog phrase. What does not resolve is a gap with its reason.
/// </summary>
public static class TestResolver
{
    /// <summary>The page phrase a navigation becomes (R5), registered by Bruinet.Uat; checked by <c>validate_scenario</c>.</summary>
    public const string OnPagePhrase = "I should be on the {page} page";

    /// <summary>Resolves a session against the project's page objects as they are now.</summary>
    public static ResolvedTest Resolve(ScrapeProject project, RecordingSession session)
    {
        var classes = ProjectCode.Read(project);
        var pages = classes.Where(c => c.Kind == CodeKind.Page && c.Header?.Page is not null)
            .OrderBy(c => c.Project == project.Name ? 0 : 1)
            .GroupBy(c => c.Header!.Page!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var snapshots = new Dictionary<int, ScrapeSnapshot>();
        var steps = new List<ResolvedStep>();
        foreach (var e in session.Events.Where(e => e.IsAction))
        {
            var step = new ResolvedStep(e.Number, e.Kind, e.Page) { Value = e.Value, Placeholder = e.Placeholder, Checked = e.Checked, Check = e.Check };
            steps.Add(Resolve(e, step, pages, classes, session, snapshots));
        }
        var start = session.Events.FirstOrDefault(e => e.Kind == RecordingEvent.PageKind)?.Page;
        return new ResolvedTest(session.Id, session.Name, start, start is not null && pages.TryGetValue(start, out var first) ? first.Name : null, steps);
    }

    private static ResolvedStep Resolve(RecordingEvent e, ResolvedStep step, Dictionary<string, CodeClass> pages, IReadOnlyList<CodeClass> classes, RecordingSession session, Dictionary<int, ScrapeSnapshot> snapshots)
    {
        pages.TryGetValue(e.Page, out var page);
        if (page is not null)
            step = step with { PageObject = page.Name };
        switch (e.Kind)
        {
            case RecordingEvent.NavigateKind:
            case RecordingEvent.AssertKind when e.Check == AssertCheck.Page:
                return page is null
                    ? step with { Gap = new(GapKind.NoPageObject, $"No page object has the page '{e.Page}' in its header: draft it, then resolve again.") }
                    : step with { Verb = "On", Phrase = OnPagePhrase.Replace("{page}", StepNames.FromIdentifier(page.Name), StringComparison.Ordinal) };
            case RecordingEvent.KeyKind:
                return step with { Gap = new(GapKind.NoVerb, $"Event {e.Number} presses {e.Value}: no control verb presses a key. Say what it does in the test.") };
            case RecordingEvent.DialogKind:
                return step with { Gap = new(GapKind.NoVerb, $"Event {e.Number} is a browser dialog (\"{e.Value}\"), dismissed: no control verb answers it.") };
        }
        if (page is null)
            return step with { Gap = new(GapKind.NoPageObject, $"No page object has the page '{e.Page}' in its header: draft it, then resolve again.") };
        if (e.Node is not { } number || session.CaptureOf(e) is not { } capture)
            return step with { Gap = new(GapKind.NoNode, $"Event {e.Number}'s element was not captured{PathText(e)}: record it again, or add the member by hand.") };
        if (!snapshots.TryGetValue(capture.Number, out var snapshot))
            snapshots[capture.Number] = snapshot = session.ReadSnapshot(capture);
        if (number < 1 || number > snapshot.Nodes.Count)
            return step with { Gap = new(GapKind.NoNode, $"Event {e.Number} names node {number}, which capture {capture.Number} does not have.") };

        var root = snapshot.Nodes[0];
        if (Member(snapshot, snapshot[number], page, root, classes, "") is not { } found)
            return step with { Node = number, Gap = new(GapKind.NoMember, $"Event {e.Number} {Doing(e)} node {number} ({SnapshotOutline.Line(snapshot[number])}) on {page.Name}, which no member covers.") };
        step = step with { Node = number, Member = found.Path, Control = ControlVerbs.Short(found.Member.Type) };
        return Verb(e, step, ControlVerbs.Interfaces(found.Member.Type, classes));
    }

    // The deepest member whose element is the node or its nearest ancestor; into a container's parts.
    private static (string Path, CodeMember Member)? Member(ScrapeSnapshot snapshot, ScrapeNode node, CodeClass owner, ScrapeNode root, IReadOnlyList<CodeClass> classes, string prefix)
    {
        var byName = owner.Members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        (CodeMember Member, ScrapeNode Element, int Distance)? best = null;
        foreach (var member in owner.Members.Where(m => m.Locator is not null))
        {
            if (Scope(snapshot, member, byName, root) is not { } scope)
                continue;
            if (ControlChecker.Css(member.Locator!) is not { } css || SnapshotSelector.Parse(css) is not { } selector)
                continue;
            foreach (var element in selector.Under(snapshot, scope))
            {
                if (Distance(snapshot, node, element) is not { } distance)
                    continue;
                if (best is null || distance < best.Value.Distance)
                    best = (member, element, distance);
            }
        }
        if (best is not { } chosen)
            return null;
        var path = prefix + chosen.Member.Name;
        var container = classes.FirstOrDefault(c => c.Kind == CodeKind.Container && c.Name.Split('<')[0] == ControlVerbs.Short(chosen.Member.Type));
        if (container is not null && chosen.Distance > 0 && Member(snapshot, node, container, chosen.Element, classes, path + ".") is { } part)
            return part;
        return (path, chosen.Member);
    }

    // The element a member's locator is looked for under: the page, or the frame its scope names.
    private static ScrapeNode? Scope(ScrapeSnapshot snapshot, CodeMember member, IReadOnlyDictionary<string, CodeMember> byName, ScrapeNode root)
    {
        if (member.Scope is null or "this")
            return root;
        if (!byName.TryGetValue(member.Scope, out var frame) || frame.Locator is null || ControlChecker.Css(frame.Locator) is not { } css || SnapshotSelector.Parse(css) is not { } selector)
            return null;
        return selector.Under(snapshot, root).FirstOrDefault();
    }

    // How many levels up from the node the element is, or null when it is not the node or an ancestor.
    private static int? Distance(ScrapeSnapshot snapshot, ScrapeNode node, ScrapeNode element)
    {
        var distance = 0;
        for (var n = node; ; n = snapshot[n.Parent], distance++)
        {
            if (n.Number == element.Number)
                return distance;
            if (n.Parent == 0)
                return null;
        }
    }

    private static ResolvedStep Verb(RecordingEvent e, ResolvedStep step, IReadOnlySet<Type> can)
    {
        (Type Contract, string Verb, bool? Literal)? verb = e.Kind switch
        {
            RecordingEvent.ClickKind when can.Contains(ControlVerbs.Clickable) => (ControlVerbs.Clickable, "Click", null),
            RecordingEvent.InputKind when can.Contains(ControlVerbs.Editable) => (ControlVerbs.Editable, "Enter", null),
            RecordingEvent.SelectKind when can.Contains(ControlVerbs.Selector) => (ControlVerbs.Selector, "SelectByText", null),
            RecordingEvent.CheckKind when can.Contains(ControlVerbs.Toggle) => (ControlVerbs.Toggle, e.Checked == false ? "Uncheck" : "Check", null),
            RecordingEvent.AssertKind => e.Check switch
            {
                AssertCheck.Visible when can.Contains(ControlVerbs.Element) => (ControlVerbs.Element, "AssertVisible", true),
                AssertCheck.Text or AssertCheck.Value when can.Contains(ControlVerbs.Text) => (ControlVerbs.Text, "AssertText", null),
                AssertCheck.Checked when can.Contains(ControlVerbs.Toggle) => (ControlVerbs.Toggle, "AssertChecked", e.Checked ?? true),
                _ => null,
            },
            _ => null,
        };
        if (verb is not { } v)
            return step with { Gap = new(GapKind.NoVerb, $"Event {e.Number} {Doing(e)} {step.Member}, a {step.Control}, which has no verb for it: give the control one, or use another control.") };
        step = step with { Verb = v.Verb };
        if (ControlVerbs.Phrase(v.Contract, v.Verb, v.Literal) is not { } phrase)
            return step with { Gap = new(GapKind.NoPhrase, $"{step.Control}.{v.Verb} has no catalog phrase: the scenario cannot say this step.") };
        return step with { Phrase = Fill(phrase, step) };
    }

    private static string Fill(string phrase, ResolvedStep step) => phrase
        .Replace("{control}", StepNames.FromIdentifier(step.Member!.Split('.')[^1]), StringComparison.Ordinal)
        .Replace("{value}", step.Placeholder is { } placeholder ? $"<{placeholder}>" : $"\"{step.Value}\"", StringComparison.Ordinal);

    private static string Doing(RecordingEvent e) => e.Kind switch
    {
        RecordingEvent.ClickKind => "clicks",
        RecordingEvent.InputKind => "types into",
        RecordingEvent.SelectKind => "chooses in",
        RecordingEvent.CheckKind => "checks",
        RecordingEvent.AssertKind => string.Create(CultureInfo.InvariantCulture, $"asserts '{e.Check}' on"),
        _ => e.Kind + "s",
    };

    private static string PathText(RecordingEvent e) =>
        e.Path is { Count: > 0 } path
            ? " (" + string.Join(" > ", path.TakeLast(3).Select(s => s.TestId is { } id ? $"{s.Type}[{id}]" : s.Type)) + ")"
            : "";
}

/// <summary>A test recording resolved: the session, the first page and its page object, and a step per action.</summary>
/// <param name="Session">The session's id.</param>
/// <param name="Name">What the person called it.</param>
/// <param name="StartPage">The page key it starts on.</param>
/// <param name="StartPageObject">The page object of that page, or null.</param>
/// <param name="Steps">A step per action, in order.</param>
public sealed record ResolvedTest(string Session, string Name, string? StartPage, string? StartPageObject, IReadOnlyList<ResolvedStep> Steps)
{
    /// <summary>The steps that did not resolve.</summary>
    public IReadOnlyList<ResolvedStep> Gaps => Steps.Where(s => s.Gap is not null).ToArray();
}

/// <summary>An action resolved, as far as it went.</summary>
/// <param name="Event">Its event number in the session.</param>
/// <param name="Kind">The action's kind.</param>
/// <param name="Page">The page key it was on.</param>
public sealed record ResolvedStep(int Event, string Kind, string Page)
{
    /// <summary>The page object of its page.</summary>
    public string? PageObject { get; init; }

    /// <summary>The node it acted on, in its capture.</summary>
    public int? Node { get; init; }

    /// <summary>The member, from the page object: <c>FirstName</c>, or <c>Filters.Status</c> for a container's part.</summary>
    public string? Member { get; init; }

    /// <summary>The member's control type, without type arguments.</summary>
    public string? Control { get; init; }

    /// <summary>The control's verb (<c>Click</c>, <c>Enter</c>, <c>SelectByText</c>, <c>Check</c>, an assert), or <c>On</c> for a page check.</summary>
    public string? Verb { get; init; }

    /// <summary>The value typed, chosen or expected.</summary>
    public string? Value { get; init; }

    /// <summary>The placeholder for a secret's value (R7).</summary>
    public string? Placeholder { get; init; }

    /// <summary>A check's state.</summary>
    public bool? Checked { get; init; }

    /// <summary>An assert's check.</summary>
    public string? Check { get; init; }

    /// <summary>The catalog phrase, filled in.</summary>
    public string? Phrase { get; init; }

    /// <summary>Why it did not resolve all the way, or null.</summary>
    public StepGap? Gap { get; init; }
}

/// <summary>Why a step did not resolve, and what to do.</summary>
public sealed record StepGap(GapKind Kind, string Message);

/// <summary>Where resolving a step stopped.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<GapKind>))]
public enum GapKind
{
    /// <summary>The page has no page object.</summary>
    NoPageObject,

    /// <summary>The element is in no capture.</summary>
    NoNode,

    /// <summary>No member's locator finds the element or an ancestor.</summary>
    NoMember,

    /// <summary>The member's control has no verb for the action, or no control verb does it.</summary>
    NoVerb,

    /// <summary>The verb has no catalog phrase.</summary>
    NoPhrase,
}
