using System.Globalization;
using System.Text;
using Bruinet.Uia;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Bruinet.Maui.FlaUI.Bridge;

/// <summary>
/// What an instrumented element offers a person who is not using a pointer.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the output that makes the programme an accessibility improvement rather than a way
/// to test around a defect.</b> If a gesture is the only way to reach a function, that is a
/// defect in the app, and the bridge does not fix it - it gives Bruinet a way to drive the broken
/// thing. A custom pattern only Bruinet knows about is worth nothing to somebody on a switch
/// device. Making an element gesture-testable should be the moment someone asks whether it is
/// gesture-<i>only</i>, and this is the list that asks.
/// </para>
/// <para>
/// <b>It asks the accessibility tree the same questions assistive technology asks.</b> Not the
/// bridge: the bridge is in the raw view and no screen reader will ever see it. For each element
/// the app has instrumented, the audit finds the <i>real</i> element by its
/// <c>AutomationId</c> and asks whether a keyboard can reach it and whether it carries
/// <c>InvokePattern</c>. An element the audit cannot find at all is the strongest finding of the
/// three, not a gap in the audit.
/// </para>
/// <para>
/// <b>It reports what is published now.</b> Elements publish themselves on load and withdraw on
/// unload, so a single run covers the pages that were open. Widening it is a matter of visiting
/// pages and merging - see <c>Merge</c>, and the audit test that walks the sample app.
/// </para>
/// </remarks>
public static class AccessibilityAudit
{
    /// <summary>
    /// Audits every element currently published on the app's bridge.
    /// </summary>
    /// <param name="root">The app's top-level window.</param>
    /// <param name="automation">The client session.</param>
    /// <param name="page">Where the app was when this ran, for the report.</param>
    /// <returns>One finding per published element.</returns>
    public static AccessibilityAuditReport Run(
        AutomationElement root, UIA3Automation automation, string page)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(automation);

        var findings = new List<AccessibilityFinding>();

        foreach (var target in BruinetBridgeLookup.Targets(root, automation))
        {
            // The bridge element's Name is the AutomationId of the element it acts for; its own
            // AutomationId is a decorated form nothing in the app answers to.
            var automationId = SafeName(target);
            if (automationId.Length == 0)
            {
                continue;
            }

            findings.Add(Examine(root, automationId, target.SupportedVerbs(), page));
        }

        return new AccessibilityAuditReport(findings);
    }

    /// <summary>
    /// Asks the real element what routes it offers, and decides what that makes it.
    /// </summary>
    private static AccessibilityFinding Examine(
        AutomationElement root,
        string automationId,
        IReadOnlyList<BruinetVerb> verbs,
        string page)
    {
        var actions = verbs
            .Where(verb => BruinetVerbs.KindOf(verb) == BruinetVerbKind.ElementAction)
            .ToArray();
        var element = root.FindFirstDescendant(f => f.ByAutomationId(automationId));

        if (element is null)
        {
            return new AccessibilityFinding(
                automationId, page, verbs, actions,
                ControlType: "not found",
                InTree: false,
                KeyboardFocusable: false,
                SupportsInvoke: false,
                AcceleratorKey: string.Empty,
                AccessKey: string.Empty);
        }

        return new AccessibilityFinding(
            automationId,
            page,
            verbs,
            actions,
            ControlType: Safe(() => element.Properties.ControlType.Value.ToString(), "unknown"),
            InTree: true,
            KeyboardFocusable: Safe(() => element.Properties.IsKeyboardFocusable.Value, false),
            SupportsInvoke: Safe(() => element.Patterns.Invoke.IsSupported, false),
            AcceleratorKey: Safe(() => element.Properties.AcceleratorKey.Value, string.Empty),
            AccessKey: Safe(() => element.Properties.AccessKey.Value, string.Empty));
    }

    private static T Safe<T>(Func<T> read, T whenUnavailable)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return whenUnavailable;
        }
    }

    private static string SafeName(AutomationElement element)
        => Safe(() => element.Properties.Name.ValueOrDefault ?? string.Empty, string.Empty);
}

/// <summary>What one instrumented element offers besides the pointer.</summary>
/// <param name="AutomationId">The element, as the app and the test both name it.</param>
/// <param name="Page">Where the app was when this was measured.</param>
/// <param name="Verbs">Everything the element declared.</param>
/// <param name="ActionVerbs">The subset a person does <i>to this element</i>.</param>
/// <param name="ControlType">Its UI Automation control type, or why there is none.</param>
/// <param name="InTree">Whether the control view contains it at all.</param>
/// <param name="KeyboardFocusable">Whether a keyboard can put focus on it.</param>
/// <param name="SupportsInvoke">Whether it carries <c>InvokePattern</c>.</param>
/// <param name="AcceleratorKey">Its accelerator, if it advertises one.</param>
/// <param name="AccessKey">Its access key, if it advertises one.</param>
public sealed record AccessibilityFinding(
    string AutomationId,
    string Page,
    IReadOnlyList<BruinetVerb> Verbs,
    IReadOnlyList<BruinetVerb> ActionVerbs,
    string ControlType,
    bool InTree,
    bool KeyboardFocusable,
    bool SupportsInvoke,
    string AcceleratorKey,
    string AccessKey)
{
    /// <summary>Whether anything but a pointer reaches this element.</summary>
    /// <remarks>
    /// <b>Focusable counts, and it is the loosest of the four on purpose.</b> A focusable element
    /// is one a keyboard user can at least arrive at, even where what happens next is the app's
    /// business. Counting it means the backlog holds only elements a keyboard cannot even reach -
    /// which keeps the list short enough to act on, and understates the problem rather than
    /// inflating it.
    /// </remarks>
    public bool HasAlternativeRoute
        => KeyboardFocusable
           || SupportsInvoke
           || AcceleratorKey.Length > 0
           || AccessKey.Length > 0;

    /// <summary>Whether any declared verb is a gesture rather than a semantic action.</summary>
    /// <remarks>
    /// Kept separate from the verdict because it is the sharpest case: a gesture is inherently a
    /// pointer affordance, so a gesture-only control is the defect this programme set out to
    /// name. A semantic verb with no keyboard route is still worth listing, but it is a weaker
    /// signal - the app may simply not have exposed a route the platform already offers.
    /// </remarks>
    public bool DeclaresAGesture
        => Verbs.Any(verb => BruinetVerbs.RangeOf((int)verb) == BruinetVerbRange.Gestures);

    /// <summary>Whether every action it declares is aimed at the app rather than at it.</summary>
    /// <remarks>
    /// A page declaring <c>NavigateBack</c> is the case: it is where the request is posted, not
    /// what a person presses, so "can a keyboard focus this page" answers nothing.
    /// </remarks>
    public bool IsAnAddressOnly
        => ActionVerbs.Count == 0
           && Verbs.Any(verb => BruinetVerbs.KindOf(verb) == BruinetVerbKind.AppAction);

    /// <summary>What this element is, for the report.</summary>
    public AccessibilityVerdict Verdict => ActionVerbs.Count == 0
        ? IsAnAddressOnly
            ? AccessibilityVerdict.AddressedToTheApp
            : AccessibilityVerdict.NotAnAffordance
        : !InTree
            ? AccessibilityVerdict.Unreachable
            : HasAlternativeRoute
                ? AccessibilityVerdict.HasAnotherRoute
                : AccessibilityVerdict.PointerOnly;
}

/// <summary>What the audit concluded about one element.</summary>
public enum AccessibilityVerdict
{
    /// <summary>
    /// Declares only reads, so there is nothing a person does to it. Not a defect.
    /// </summary>
    /// <remarks>
    /// The commonest verdict, and the reason the audit needs the action/read split at all: the
    /// sample app declares <c>GetState</c> on labels and images, and a report that called those
    /// accessibility defects would be almost entirely noise.
    /// </remarks>
    NotAnAffordance = 0,

    /// <summary>
    /// Carries only app-level actions, so the element is an address rather than an affordance.
    /// Not a defect.
    /// </summary>
    /// <remarks>
    /// <b>Eight of the first audit's fifteen findings.</b> Every instrumented page declares
    /// <c>NavigateBack</c> and <c>InvokeMenuItem</c>, and no page is keyboard focusable, so the
    /// report said so eight times - true, meaningless, and more than half the backlog. Whether
    /// going back is reachable by keyboard is a real question, but it is a question about the
    /// back affordance, which no page is.
    /// </remarks>
    AddressedToTheApp = 1,

    /// <summary>A keyboard route or <c>InvokePattern</c> exists. Not a defect.</summary>
    HasAnotherRoute = 2,

    /// <summary>In the tree, does something to itself, and only a pointer can do it.</summary>
    PointerOnly = 3,

    /// <summary>
    /// Does something, and the control view does not contain it at all.
    /// </summary>
    /// <remarks>
    /// Worse than <see cref="PointerOnly"/>: not merely unreachable by keyboard but invisible to
    /// every assistive technology, which is also why the bridge had to reach it by a route of its
    /// own. This is the finding most worth acting on and the one nothing else in the suite
    /// reports.
    /// </remarks>
    Unreachable = 4,
}

/// <summary>Every finding from one or more audit passes, and what they add up to.</summary>
public sealed class AccessibilityAuditReport
{
    private readonly List<AccessibilityFinding> _findings;

    internal AccessibilityAuditReport(IEnumerable<AccessibilityFinding> findings)
        => _findings = [.. findings];

    /// <summary>Every element examined, in the order they were found.</summary>
    public IReadOnlyList<AccessibilityFinding> Findings => _findings;

    /// <summary>
    /// The backlog: elements that do something and offer no route but the pointer.
    /// </summary>
    /// <remarks>
    /// Unreachable first, then pointer-only, because the first group is both worse and smaller.
    /// </remarks>
    public IReadOnlyList<AccessibilityFinding> Backlog =>
    [
        .. _findings
            .Where(f => f.Verdict is AccessibilityVerdict.Unreachable
                        or AccessibilityVerdict.PointerOnly)
            .OrderByDescending(f => f.Verdict)
            .ThenBy(f => f.AutomationId, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Adds another pass's findings, keeping one entry per element.
    /// </summary>
    /// <remarks>
    /// <b>First reading wins.</b> An element seen on two pages is the same element with the same
    /// declaration; a later pass that found it while the page was mid-teardown would only be able
    /// to report it as worse than it is.
    /// </remarks>
    /// <param name="other">The later pass.</param>
    public void Merge(AccessibilityAuditReport other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var seen = _findings.Select(f => f.AutomationId).ToHashSet(StringComparer.Ordinal);

        foreach (var finding in other.Findings.Where(f => seen.Add(f.AutomationId)))
        {
            _findings.Add(finding);
        }
    }

    /// <summary>
    /// The report as Markdown: the backlog first, then everything examined.
    /// </summary>
    /// <remarks>
    /// <b>The elements that are fine are listed too.</b> A backlog on its own cannot be read: a
    /// short one means either a healthy app or an audit that visited two pages, and only the
    /// second table tells them apart.
    /// </remarks>
    /// <returns>The report.</returns>
    public string ToMarkdown()
    {
        var text = new StringBuilder();

        text.AppendLine("# Gesture accessibility audit");
        text.AppendLine();
        text.AppendLine(
            "Every element the app under test instrumented for Bruinet, and what it offers "
            + "somebody who is not using a pointer. Elements declaring only reads are listed but "
            + "are not defects: nobody *does* a `GetState`.");
        text.AppendLine();
        text.AppendLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Examined {_findings.Count}; {Backlog.Count} need a route that is not the pointer."));
        text.AppendLine();

        text.AppendLine("## Backlog");
        text.AppendLine();

        if (Backlog.Count == 0)
        {
            text.AppendLine(
                "Nothing. Every instrumented element that does something is reachable another "
                + "way.");
        }
        else
        {
            text.AppendLine("| Element | Page | Verdict | Gesture? | Does | Control type |");
            text.AppendLine("|---|---|---|---|---|---|");

            foreach (var finding in Backlog)
            {
                text.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| `{finding.AutomationId}` | {finding.Page} | {Describe(finding.Verdict)} "
                    + $"| {(finding.DeclaresAGesture ? "yes" : "no")} "
                    + $"| {string.Join(", ", finding.ActionVerbs)} | {finding.ControlType} |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Everything examined");
        text.AppendLine();
        text.AppendLine("| Element | Page | Verdict | Keyboard | Invoke | Declares |");
        text.AppendLine("|---|---|---|---|---|---|");

        foreach (var finding in _findings.OrderBy(f => f.AutomationId, StringComparer.Ordinal))
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"| `{finding.AutomationId}` | {finding.Page} | {Describe(finding.Verdict)} "
                + $"| {(finding.KeyboardFocusable ? "focusable" : "no")} "
                + $"| {(finding.SupportsInvoke ? "yes" : "no")} "
                + $"| {string.Join(", ", finding.Verbs)} |");
        }

        return text.ToString();
    }

    private static string Describe(AccessibilityVerdict verdict) => verdict switch
    {
        AccessibilityVerdict.Unreachable => "**not in the accessibility tree**",
        AccessibilityVerdict.PointerOnly => "**pointer only**",
        AccessibilityVerdict.HasAnotherRoute => "reachable",
        AccessibilityVerdict.AddressedToTheApp => "an address, not an affordance",
        _ => "read-only",
    };
}
