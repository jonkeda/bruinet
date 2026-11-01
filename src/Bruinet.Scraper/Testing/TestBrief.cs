using System.Text;
using Bruinet.Scraper.Code;
using Bruinet.Scraper.Html;
using Bruinet.Scraper.Projects;
using Bruinet.Scraper.Service;
using System.Globalization;

namespace Bruinet.Scraper.Testing;

/// <summary>
/// A gap of a resolved test recording as a brief for the agent (R4): what the person did, the element in
/// its capture, what the gap needs by its kind (a member, with the member the rules would write; a verb; a
/// page object), the one file to change, and how to check. The agent fills the gap in the page objects or
/// controls, never in the drafted test, and never invents a scenario phrase (AD-017).
/// </summary>
public static class TestBrief
{
    /// <summary>The most briefs one prompt carries.</summary>
    public const int PerPrompt = 5;

    /// <summary>The brief of the gap at an event.</summary>
    /// <exception cref="ArgumentException">The event is no gap of the test.</exception>
    public static string Write(ScrapeProject project, RecordingSession session, ResolvedTest test, int @event)
    {
        var step = test.Steps.FirstOrDefault(s => s.Event == @event && s.Gap is not null)
            ?? throw new ArgumentException($"Event {@event} is no gap of {test.Session}; the gaps are {string.Join(", ", test.Gaps.Select(g => g.Event))}.");
        var e = session.Events[@event - 1];
        var classes = ProjectCode.Read(project);
        var page = step.PageObject is null ? null : classes.FirstOrDefault(c => c.Kind == CodeKind.Page && c.Name == step.PageObject);
        var md = new StringBuilder();
        md.Append("# Gap: event ").Append(@event).Append(" of ").Append(test.Name).Append(" (").Append(test.Session).Append(")\n\n");
        md.Append(step.Gap!.Message).Append("\n\n## What the person did\n\n");
        md.Append("- ").Append(e.Kind);
        if (e.Check is not null)
            md.Append(", check ").Append(e.Check);
        if (e.Value is not null)
            md.Append(", value \"").Append(e.Value).Append('"');
        if (e.Placeholder is not null)
            md.Append(", the secret <").Append(e.Placeholder).Append('>');
        if (e.Checked is not null)
            md.Append(e.Checked == true ? ", checked" : ", unchecked");
        md.Append("\n- on the page `").Append(e.Page).Append('`');
        if (page is not null)
            md.Append(", whose page object is `").Append(page.Name).Append("` (").Append(Relative(project, page.File)).Append(')');
        md.Append('\n');

        var capture = e.Node is null ? null : session.CaptureOf(e);
        var snapshot = capture is null ? null : session.ReadSnapshot(capture);
        if (snapshot is not null && e.Node is { } number && number >= 1 && number <= snapshot.Nodes.Count)
        {
            var node = snapshot[number];
            var focus = node.Parent != 0 ? snapshot[node.Parent] : node;
            md.Append("\n## The element\n\nNode ").Append(number).Append(" of ").Append(capture!.Snapshot).Append(", under its parent:\n\n```\n")
                .Append(SnapshotOutline.Render(snapshot, focus.Number, 30, number).TrimEnd()).Append("\n```\n");
        }
        else if (e.Path is { Count: > 0 } path)
        {
            md.Append("\n## The element\n\nNot in a capture. Its path from the root:\n\n```\n")
                .Append(string.Join(" > ", path.Select(p => p.Type + (p.TestId is { } id ? $"[{id}]" : "") + (p.Role is { } role ? $"[role={role}]" : "") + (p.Name is { } name ? $"[aria-label={name}]" : ""))))
                .Append("\n```\n");
        }

        md.Append("\n## What to do\n\n");
        switch (step.Gap.Kind)
        {
            case GapKind.NoMember when page is not null && snapshot is not null:
                var suggestion = ControlAdvisor.Suggest(snapshot, snapshot[e.Node!.Value], project.Config.TestIds, project.Config.Platform == "blazor");
                var file = PersonsPart(project, page);
                md.Append("Add a member for the element to `").Append(page.Name).Append("`, in ").Append(file).Append(". The rules would write:\n\n```csharp\n")
                    .Append(CultureInfo.InvariantCulture, $"public {suggestion.Control}<{page.Name}> {suggestion.Name} => new({suggestion.Scope ?? "this"}, {suggestion.Locator});")
                    .Append("\n```\n\n").Append(suggestion.Stable ? "The locator is stable: " : "The locator is not stable: ").Append(suggestion.Why)
                    .Append(". Name it as the app does; use a control that has the verb the step needs (")
                    .Append(Needs(e)).Append(").\n");
                break;
            case GapKind.NoVerb when step.Member is not null:
                md.Append('`').Append(step.Member).Append("` is a `").Append(step.Control).Append("`, which has no verb to ").Append(Needs(e))
                    .Append(". Make the member a control that has one, or give the control the verb (the html-control skill). Do not change the drafted test.\n");
                break;
            case GapKind.NoVerb:
                md.Append("No control verb does this. Say it in your part of the test, or leave it marked; it is not a page object's member.\n");
                break;
            case GapKind.NoPageObject:
                md.Append("Draft the page object for `").Append(e.Page).Append("` (draft_page_objects with that page), then resolve again.\n");
                break;
            case GapKind.NoNode:
                md.Append("The element was not captured. Record the test again (the element is captured when acted on), or add the member by hand from its path.\n");
                break;
            case GapKind.NoPhrase:
                md.Append("The scenario cannot say this step: no catalog phrase does. Leave the gap marked; never invent a phrase (AD-017).\n");
                break;
            default:
                md.Append("Resolve it by hand: the facts above are all that is known.\n");
                break;
        }
        md.Append("\n## Then\n\n1. `check_code`: the project builds.\n2. `resolve_actions` for ").Append(test.Session)
            .Append(": the gap is gone.\n3. `draft_test` for it, then `run_test` once. Change only the page objects and controls; a drafted test is drafted again over itself.\n");
        return md.ToString();
    }

    /// <summary>Up to <see cref="PerPrompt"/> briefs, the test's first gaps.</summary>
    public static IReadOnlyList<string> WriteFirst(ScrapeProject project, RecordingSession session, ResolvedTest test) =>
        test.Gaps.Take(PerPrompt).Select(g => Write(project, session, test, g.Event)).ToArray();

    // A drafted page object's members go in the person's part beside its draft.
    private static string PersonsPart(ScrapeProject project, CodeClass page) =>
        page.File.EndsWith(PageDrafter.Extension, StringComparison.OrdinalIgnoreCase)
            ? $"its person's part `{Relative(project, page.File[..^PageDrafter.Extension.Length] + ".cs")}` (a `partial class {page.Name.Split('<')[0]}`; make it when it is not there), never the draft"
            : $"`{Relative(project, page.File)}`";

    private static string Needs(RecordingEvent e) => e.Kind switch
    {
        RecordingEvent.ClickKind => "click it (IClickableControlObject)",
        RecordingEvent.InputKind => "enter a value (IEditableTextControlObject)",
        RecordingEvent.SelectKind => "select by text (ISelectorControlObject)",
        RecordingEvent.CheckKind => "check it (IToggleControlObject)",
        RecordingEvent.AssertKind => e.Check switch
        {
            AssertCheck.Text or AssertCheck.Value => "assert its text (ITextControlObject)",
            AssertCheck.Checked => "assert it is checked (IToggleControlObject)",
            _ => "assert it is visible",
        },
        _ => e.Kind,
    };

    private static string Relative(ScrapeProject project, string file) => Path.GetRelativePath(project.Folder, file).Replace('\\', '/');
}
