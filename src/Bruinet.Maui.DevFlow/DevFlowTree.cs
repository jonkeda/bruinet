using System.Text;
using Microsoft.Maui.DevFlow.Driver;

namespace Bruinet.Maui.DevFlow;

/// <summary>One element of a tree the agent returned, with its parent and whether a user could see it.</summary>
internal sealed class DevFlowNode
{
    private readonly List<DevFlowNode> _children = [];

    public DevFlowNode(ElementInfo info, DevFlowNode? parent)
    {
        Info = info;
        Parent = parent;
    }

    public ElementInfo Info { get; }

    public DevFlowNode? Parent { get; }

    public IReadOnlyList<DevFlowNode> Children => _children;

    /// <summary>
    /// On screen as a user sees it: itself and every ancestor visible, and not on a page another page
    /// covers. The agent's tree keeps a NavigationPage's covered pages, visible by their own flag,
    /// where a platform's tree drops them.
    /// </summary>
    public bool Shown { get; set; }

    public string Id => Info.Id;

    public string Type => Info.Type;

    public string? AutomationId => Info.AutomationId;

    public string? Text => Info.Text;

    /// <summary>A node the agent made up for platform chrome (the title, the back button), not a MAUI element.</summary>
    public bool IsSynthetic => Info.FullType.StartsWith("Microsoft.Maui.DevFlow.", StringComparison.Ordinal);

    internal void Add(DevFlowNode child) => _children.Add(child);

    public IEnumerable<DevFlowNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in _children)
        {
            foreach (var deeper in child.DescendantsAndSelf())
                yield return deeper;
        }
    }

    public bool IsUnder(DevFlowNode ancestor)
    {
        for (var walker = Parent; walker is not null; walker = walker.Parent)
        {
            if (ReferenceEquals(walker, ancestor))
                return true;
        }

        return false;
    }
}

/// <summary>A snapshot of the app's visual tree, every window, as nodes with parents.</summary>
internal sealed class DevFlowTree
{
    private readonly Dictionary<string, DevFlowNode> _byId = new(StringComparer.Ordinal);

    /// <param name="windows">The agent's tree, one root per window.</param>
    /// <param name="modalsCover">
    /// Whether a modal page takes the page under it out of the tree, as Android's accessibility tree
    /// does; on Windows it covers the page without taking it away, and the page is still found.
    /// A page pushed on a NavigationPage is taken away on every platform.
    /// </param>
    public DevFlowTree(IReadOnlyList<ElementInfo> windows, bool modalsCover = false)
    {
        ArgumentNullException.ThrowIfNull(windows);
        _modalsCover = modalsCover;
        Windows = windows.Select(window => Build(window, null)).ToList();
        foreach (var window in Windows)
            MarkShown(window, parentShown: true);
    }

    private readonly bool _modalsCover;

    public IReadOnlyList<DevFlowNode> Windows { get; }

    /// <summary>Every node, windows first, in reading order.</summary>
    public IEnumerable<DevFlowNode> All => Windows.SelectMany(window => window.DescendantsAndSelf());

    public DevFlowNode? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>The app's own window: the first that is not a dialog.</summary>
    public DevFlowNode? MainWindow => Windows.FirstOrDefault(window => !IsDialog(window));

    private static bool IsDialog(DevFlowNode window) => window.Id.Contains(":dialog:", StringComparison.Ordinal);

    /// <summary>
    /// The dialog on top: on Windows the agent shows an alert, a confirmation or a prompt as a window
    /// of its own, read from the native ContentDialog (its ids carry <c>:dialog:</c>).
    /// </summary>
    public DevFlowNode? ActiveDialog()
        => Windows.LastOrDefault(window => IsDialog(window) && window.Shown);

    /// <summary>The page on top: the last page of the first window's navigation, modal pages included.</summary>
    public DevFlowNode? TopPage()
    {
        DevFlowNode? top = null;
        var level = MainWindow;
        while (level is not null)
        {
            var pages = level.Children.Where(child => !child.IsSynthetic).ToList();
            if (pages.Count == 0)
                break;
            var next = HoldsPages(level) ? pages[^1] : pages[0];
            if (!IsPage(next))
                break;
            top = next;
            level = next;
        }

        return top;
    }

    /// <summary>How many pages the first window's navigation holds, modal ones included.</summary>
    public int NavigationDepth()
    {
        var depth = 0;
        var level = MainWindow;
        while (level is not null)
        {
            var pages = level.Children.Where(child => !child.IsSynthetic && IsPage(child)).ToList();
            if (pages.Count == 0)
                break;
            if (HoldsPages(level) && level.Type != "Window")
                depth += pages.Count - 1;
            level = HoldsPages(level) ? pages[^1] : pages[0];
        }

        return depth + 1;
    }

    /// <summary>The tree as indented lines, for a failure's page source.</summary>
    public string Format()
    {
        var text = new StringBuilder();
        foreach (var node in All)
        {
            var depth = 0;
            for (var walker = node.Parent; walker is not null; walker = walker.Parent)
                depth++;
            text.Append(' ', depth * 2).Append(node.Type);
            if (!string.IsNullOrEmpty(node.AutomationId))
                text.Append(" #").Append(node.AutomationId);
            if (!string.IsNullOrEmpty(node.Text))
                text.Append(" \"").Append(node.Text).Append('"');
            if (!node.Shown)
                text.Append(" (hidden)");
            text.AppendLine();
        }

        return text.ToString();
    }

    private DevFlowNode Build(ElementInfo info, DevFlowNode? parent)
    {
        var node = new DevFlowNode(info, parent);
        _byId.TryAdd(info.Id, node);
        foreach (var child in info.Children ?? [])
            node.Add(Build(child, node));
        return node;
    }

    private void MarkShown(DevFlowNode node, bool parentShown)
    {
        // A window reports itself invisible; its page says what shows.
        node.Shown = parentShown && (node.Info.IsVisible || node.Type == "Window");
        DevFlowNode? onTop = null;
        if (HoldsPages(node) && (node.Type != "Window" || _modalsCover))
            onTop = node.Children.LastOrDefault(child => !child.IsSynthetic && IsPage(child));

        foreach (var child in node.Children)
        {
            var covered = onTop is not null && IsPage(child) && !child.IsSynthetic && !ReferenceEquals(child, onTop);
            MarkShown(child, node.Shown && !covered);
        }
    }

    // A window holds its page and the modal pages over it; a NavigationPage its stack.
    private static bool HoldsPages(DevFlowNode node)
        => node.Type is "Window" || node.Type.EndsWith("NavigationPage", StringComparison.Ordinal)
           || node.Info.FullType.EndsWith(".NavigationPage", StringComparison.Ordinal);

    // The agent does not say which elements are pages; MAUI's page types, and the app's, end in Page or Shell.
    internal static bool IsPage(DevFlowNode node)
        => node.Type.EndsWith("Page", StringComparison.Ordinal) || node.Type.EndsWith("Shell", StringComparison.Ordinal)
           || node.Info.FullType.EndsWith("Page", StringComparison.Ordinal);
}
