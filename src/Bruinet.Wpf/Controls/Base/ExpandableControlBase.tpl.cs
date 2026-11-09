using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for WPF controls that expand and collapse (an expander, a tree view item): Expand,
/// Collapse, ToggleExpanded, through the ExpandCollapse pattern; each confirms the state took.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public abstract partial class ExpandableControlBase<TScope> : ClickableControlBase<TScope>, IExpandableControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    protected ExpandableControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    protected ExpandableControlBase(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }

    #region Core Methods

    /// <summary>Whether the control is expanded; null for a leaf or without the pattern.</summary>
    /// <param name="element">The pre-found element.</param>
    protected virtual bool? IsExpandedCore(IWpfElement? element) => element?.IsExpanded;

    /// <summary>Expands the control, and confirms it is expanded. Does nothing when it is.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ExpandCore(IWpfElement element, int? timeoutMs = null)
    {
        if (IsExpandedCore(element) == true)
            return;

        ActAndConfirm("Expand", element.Expand, () => IsExpandedCore(element), expanded => expanded == true, "expanded", timeoutMs);
    }

    /// <summary>Collapses the control, and confirms it is collapsed. Does nothing when it is.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void CollapseCore(IWpfElement element, int? timeoutMs = null)
    {
        if (IsExpandedCore(element) == false)
            return;

        ActAndConfirm("Collapse", element.Collapse, () => IsExpandedCore(element), expanded => expanded == false, "collapsed", timeoutMs);
    }

    /// <summary>Expands a collapsed control, or collapses an expanded one.</summary>
    /// <param name="element">The pre-found element.</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds.</param>
    protected virtual void ToggleExpandedCore(IWpfElement element, int? timeoutMs = null)
    {
        if (IsExpandedCore(element) == true)
            CollapseCore(element, timeoutMs);
        else
            ExpandCore(element, timeoutMs);
    }

    #endregion
}
