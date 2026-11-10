using Bruinet.Maui.Controls.Display;

namespace Bruinet.Maui.Controls.Container;

/// <summary>
/// One node of a <see cref="TreeView{TParent, TSelf}"/>: its label and its expand/collapse toggle,
/// both found within the tree by the automation ids the app gave them.
/// A node is not a scope (its row has no automation peer), and a node under a collapsed
/// ancestor does not exist. On Windows the toggle is invoked and expansion read from its name.
/// </summary>
/// <typeparam name="TTree">The tree scope this node belongs to.</typeparam>
public class TreeNode<TTree>
    where TTree : IMauiScope<TTree>
{
    private readonly IMauiScope<TTree> _tree;
    private readonly string _nodeAutomationId;
    private readonly string _toggleAutomationId;
    private readonly string _expandedStateName;
    private readonly string _collapsedStateName;

    /// <summary>Creates a node addressed by its label and toggle ids within <paramref name="tree"/>.</summary>
    public TreeNode(
        IMauiScope<TTree> tree,
        string nodeAutomationId,
        string toggleAutomationId,
        string expandedStateName,
        string collapsedStateName)
    {
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
        _nodeAutomationId = nodeAutomationId;
        _toggleAutomationId = toggleAutomationId;
        _expandedStateName = expandedStateName;
        _collapsedStateName = collapsedStateName;
    }

    /// <summary>The node's text.</summary>
    public Label<TTree> Text => new(_tree, _nodeAutomationId);

    /// <summary>The node's expand/collapse toggle.</summary>
    public Button<TTree> Toggle => new(_tree, _toggleAutomationId);

    /// <summary>Whether the node is in the tree at all, which means every ancestor is expanded.</summary>
    public bool Exists() => Text.Exists();

    /// <summary>Waits until the node is (or is no longer) in the tree.</summary>
    public bool WaitExists(bool? expected = true, int? timeoutMs = null) => Text.WaitExists(expected, timeoutMs);

    /// <summary>Asserts the node is (or is no longer) in the tree.</summary>
    public TTree AssertExists(bool? expected = true, string? message = null, int? timeoutMs = null)
        => Text.AssertExists(expected, message, timeoutMs);

    /// <summary>The node's text, or null when the platform does not publish it.</summary>
    public string? GetText(int? timeoutMs = null) => Text.GetText(timeoutMs);

    /// <summary>
    /// Whether the node is expanded, or null when the app publishes no expansion state - which is
    /// also what a leaf answers.
    /// </summary>
    public bool? IsExpanded(int? timeoutMs = null) => ReadState(Toggle.GetAttribute("name", timeoutMs));

    /// <summary>Waits until the node is (or is no longer) expanded.</summary>
    public bool WaitExpanded(bool expected = true, int? timeoutMs = null)
        => Toggle.WaitAttribute("name", expected ? _expandedStateName : _collapsedStateName, timeoutMs);

    /// <summary>Asserts the node's expansion.</summary>
    public TTree AssertExpanded(bool expected = true, string? message = null, int? timeoutMs = null)
        => Toggle.AssertAttribute(
            "name",
            expected ? _expandedStateName : _collapsedStateName,
            message ?? $"Expected node '{_nodeAutomationId}' to be {(expected ? "expanded" : "collapsed")}. Toggle: {_toggleAutomationId}",
            timeoutMs);

    /// <summary>Expands the node, doing nothing when it is already expanded.</summary>
    public TTree Expand(int? timeoutMs = null) => SetExpanded(true, timeoutMs);

    /// <summary>Collapses the node, doing nothing when it is already collapsed.</summary>
    public TTree Collapse(int? timeoutMs = null) => SetExpanded(false, timeoutMs);

    /// <summary>
    /// Brings the node to <paramref name="expanded"/>; null does nothing. Clicks the toggle once
    /// and waits for the state to flip, and throws naming both parts when it does not.
    /// </summary>
    public TTree SetExpanded(bool? expanded, int? timeoutMs = null)
    {
        if (expanded is not { } target)
        {
            return _tree.Self;
        }

        var toggle = Toggle;
        var before = ReadState(toggle.GetAttribute("name", timeoutMs));

        if (before == target)
        {
            return _tree.Self;
        }

        if (before is null)
        {
            throw new NotSupportedException(
                $"Node '{_nodeAutomationId}' publishes no expansion state, so it cannot be "
                + $"{(target ? "expanded" : "collapsed")}. The app must set the toggle's accessible name to "
                + $"'{_expandedStateName}' or '{_collapsedStateName}'. Toggle: {_toggleAutomationId}");
        }

        toggle.Click(timeoutMs);

        var wanted = target ? _expandedStateName : _collapsedStateName;
        if (!toggle.WaitAttribute("name", wanted, timeoutMs))
        {
            throw new InvalidOperationException(
                $"Node '{_nodeAutomationId}' did not {(target ? "expand" : "collapse")}: its toggle still reports "
                + $"'{toggle.GetAttribute("name")}' after the click, not '{wanted}'. Toggle: {_toggleAutomationId}");
        }

        return _tree.Self;
    }

    private bool? ReadState(string? name) =>
        name == _expandedStateName ? true
        : name == _collapsedStateName ? false
        : null;
}
