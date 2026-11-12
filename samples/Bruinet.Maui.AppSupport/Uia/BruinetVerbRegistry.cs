#if BRUINET_UIA_BRIDGE
using Bruinet.Uia;
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// The elements that declared verbs and were published, with their resolved plans, kept apart from
/// any transport: UI Automation carries them on Windows, and the DevFlow agent's
/// extension (dev.bruinet.bridge) carries them on any platform (BruinetDevFlowBridge). Behind the same two
/// gates as the bridge: compiled into a Debug build only, and filled only while the bridge is on.
/// </summary>
internal static class BruinetVerbRegistry
{
    private static readonly object Gate = new();
    private static readonly List<Entry> Entries = [];

    /// <summary>Records a published element; an element published again replaces its entry.</summary>
    internal static void Register(VisualElement element, VerbPlan plan)
    {
        lock (Gate)
        {
            Entries.RemoveAll(entry => !entry.Element.TryGetTarget(out var known) || ReferenceEquals(known, element));
            Entries.Add(new Entry(new WeakReference<VisualElement>(element), element.AutomationId, plan));
        }
    }

    /// <summary>Forgets an element that was unloaded.</summary>
    internal static void Withdraw(VisualElement element)
    {
        lock (Gate)
        {
            Entries.RemoveAll(entry => !entry.Element.TryGetTarget(out var known) || ReferenceEquals(known, element));
        }
    }

    /// <summary>
    /// The live elements that declare <paramref name="verb"/>, newest first: with
    /// <paramref name="automationId"/>, those carrying it; without, every one (an app-wide verb such
    /// as <c>InvokeMenuItem</c> is answered by whichever page declares it).
    /// </summary>
    internal static IReadOnlyList<(VisualElement Element, VerbPlan Plan)> Targets(string? automationId, BruinetVerb verb)
    {
        lock (Gate)
        {
            var targets = new List<(VisualElement, VerbPlan)>();
            for (var index = Entries.Count - 1; index >= 0; index--)
            {
                var entry = Entries[index];
                if (!entry.Element.TryGetTarget(out var element))
                    continue;
                if (automationId is not null && !string.Equals(entry.AutomationId, automationId, StringComparison.Ordinal))
                    continue;
                if (entry.Plan.Capabilities.Contains(verb))
                    targets.Add((element, entry.Plan));
            }

            return targets;
        }
    }

    private sealed record Entry(WeakReference<VisualElement> Element, string AutomationId, VerbPlan Plan);
}
#endif
