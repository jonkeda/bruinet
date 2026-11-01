namespace Bruinet.Wpf.Controls;

/// <summary>
/// Base class for selector WPF controls (ComboBox, ListBox).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class SelectorControlBase<TScope> : ClickableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    /// <summary>
    /// Creates a new selector control with the specified scope and locator.
    /// </summary>
    protected SelectorControlBase(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    /// <summary>
    /// Creates a new selector control using the scope's default locator strategy.
    /// </summary>
    protected SelectorControlBase(IWpfScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Select Actions

    /// <inheritdoc />
    public virtual TScope SelectByText(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        RunWithElement(e => SelectByTextCore(e, text), timeoutMs);
        return ContainingScope;
    }

    /// <summary>
    /// Core select by text implementation.
    /// </summary>
    protected virtual void SelectByTextCore(IWpfElement element, string text)
    {
        if (element is IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            expander.SelectItemByText(text);
        }
    }

    /// <inheritdoc />
    public virtual TScope SelectByIndex(int? index, int? timeoutMs = null)
    {
        if (index == null)
            return ContainingScope;
        RunWithElement(e => SelectByIndexCore(e, index.Value), timeoutMs);
        return ContainingScope;
    }

    /// <summary>
    /// Core select by index implementation.
    /// </summary>
    protected virtual void SelectByIndexCore(IWpfElement element, int index)
    {
        if (element is IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            expander.SelectItemByIndex(index);
        }
    }

    /// <inheritdoc />
    public virtual TScope SelectByValue(string? value, int? timeoutMs = null)
    {
        // Value-based selection maps to text-based for WPF
        return SelectByText(value, timeoutMs);
    }

    #endregion

    #region Selected State

    /// <inheritdoc />
    public virtual string? GetSelectedText(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            return GetSelectedTextCore(element);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Core selected text implementation.
    /// </summary>
    protected virtual string? GetSelectedTextCore(IWpfElement element)
    {
        if (element is IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            return expander.GetSelectedItemText();
        }
        return element.Text;
    }

    /// <inheritdoc />
    public bool WaitSelectedText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetSelectedText() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertSelectedText(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (WaitSelectedText(expected, timeoutMs) != true)
        {
            var actual = GetSelectedText();
            throw new AssertionException(
                message ?? $"Expected selected text '{expected}' for '{AutomationId}' but got '{actual}'",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    /// <inheritdoc />
    public virtual int? GetSelectedIndex(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            var items = GetItemTextsCore(element);
            var selected = GetSelectedTextCore(element);
            if (items != null && selected != null)
            {
                var idx = items.ToList().IndexOf(selected);
                return idx >= 0 ? idx : null;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool WaitSelectedIndex(int? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetSelectedIndex() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertSelectedIndex(int? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (WaitSelectedIndex(expected, timeoutMs) != true)
        {
            var actual = GetSelectedIndex();
            throw new AssertionException(
                message ?? $"Expected selected index {expected} for '{AutomationId}' but got {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion

    #region Item Access

    /// <inheritdoc />
    public virtual IReadOnlyList<string>? GetItemTexts(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            return GetItemTextsCore(element);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Core item texts implementation.
    /// </summary>
    protected virtual IReadOnlyList<string>? GetItemTextsCore(IWpfElement element)
    {
        if (element is IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            var items = expander.GetExpandedItems();
            return items?.Select(i => i.Text ?? "").ToList();
        }
        return null;
    }

    /// <inheritdoc />
    public virtual int? GetItemCount(int? timeoutMs = null)
    {
        return GetItemTexts(timeoutMs)?.Count;
    }

    /// <inheritdoc />
    public bool WaitItemCount(int? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetItemCount() == expected, timeout);
    }

    /// <inheritdoc />
    public TScope AssertItemCount(int? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (WaitItemCount(expected, timeoutMs) != true)
        {
            var actual = GetItemCount();
            throw new AssertionException(
                message ?? $"Expected item count {expected} for '{AutomationId}' but got {actual}",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }

    #endregion
}
