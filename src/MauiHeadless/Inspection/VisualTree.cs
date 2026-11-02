using Microsoft.Maui.Controls;

namespace MauiHeadless.Inspection;

/// <summary>Reads the tree of <see cref="VisualNode"/>s under an element.</summary>
public static class VisualTree
{
    /// <summary>Reads the tree under <paramref name="root"/> as it is now.</summary>
    /// <param name="root">The element to start at.</param>
    /// <param name="adapters">The adapters to ask first; <see cref="DefaultElementAdapter"/> is always last.</param>
    public static VisualNode Read(Element root, IReadOnlyList<IElementAdapter>? adapters = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        adapters ??= MauiTestContext.Of(root)?.Adapters ?? [];
        var seen = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        return ReadNode(root, null, AncestorsVisible(root), AncestorsEnabled(root), adapters, seen);
    }

    /// <summary>The adapter that reads <paramref name="element"/>.</summary>
    public static IElementAdapter AdapterFor(Element element, IReadOnlyList<IElementAdapter> adapters)
    {
        foreach (var adapter in adapters)
        {
            if (adapter.CanAdapt(element))
                return adapter;
        }

        return DefaultElementAdapter.Instance;
    }

    /// <summary>Whether <paramref name="element"/> and every ancestor are visible.</summary>
    public static bool IsEffectivelyVisible(Element element)
        => OwnVisible(element) && AncestorsVisible(element);

    /// <summary>Whether <paramref name="element"/> and every ancestor are enabled.</summary>
    public static bool IsEffectivelyEnabled(Element element)
        => OwnEnabled(element) && AncestorsEnabled(element);

    /// <summary>
    /// Whether <paramref name="element"/> is a row an items view realized for one of its items: not its header,
    /// footer, empty view or a group's header or footer. A platform reports it as a list item.
    /// </summary>
    public static bool IsItemRow(Element element)
        => element.Parent is ItemsView && ItemRealizer.TryGetItem(element, out _);

    private static VisualNode ReadNode(
        Element element, VisualNode? parent, bool parentVisible, bool parentEnabled,
        IReadOnlyList<IElementAdapter> adapters, HashSet<Element> seen)
    {
        seen.Add(element);
        var adapter = AdapterFor(element, adapters);
        var visible = parentVisible && OwnVisible(element);
        var enabled = parentEnabled && OwnEnabled(element);
        var node = new VisualNode(element, parent, adapter.GetText(element), visible, enabled);
        foreach (var child in adapter.GetChildren(element))
        {
            if (!seen.Contains(child))
                node.Add(ReadNode(child, node, visible, enabled, adapters, seen));
        }

        return node;
    }

    // A carousel shows one card: the others are realized but off screen.
    private static bool OwnVisible(Element element)
        => (element is not VisualElement visual || visual.IsVisible) && !ItemRealizer.IsCardOffScreen(element);

    private static bool OwnEnabled(Element element) => element switch
    {
        VisualElement visual => visual.IsEnabled,
        MenuItem item => item.IsEnabled,
        _ => true,
    };

    private static bool AncestorsVisible(Element element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (!OwnVisible(parent))
                return false;
        }

        return true;
    }

    private static bool AncestorsEnabled(Element element)
    {
        for (var parent = element.Parent; parent is not null; parent = parent.Parent)
        {
            if (!OwnEnabled(parent))
                return false;
        }

        return true;
    }
}
