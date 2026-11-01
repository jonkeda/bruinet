namespace Bruinet.WinForms.Controls;

/// <summary>
/// Base class for selector WinForms controls (ComboBox, ListBox).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent method chaining.</typeparam>
public abstract class SelectorControlBase<TScope> : ClickableControlBase<TScope>, ISelectorControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    protected SelectorControlBase(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator) { }

    protected SelectorControlBase(IWinFormsScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue) { }

    #region Select Actions

    public virtual TScope SelectByText(string? text, int? timeoutMs = null)
    {
        if (text == null)
            return ContainingScope;
        RunWithElement(e => SelectByTextCore(e, text), timeoutMs);
        return ContainingScope;
    }

    protected virtual void SelectByTextCore(IWinFormsElement element, string text)
    {
        if (element is Interfaces.IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            expander.SelectItemByText(text);
        }
    }

    public virtual TScope SelectByIndex(int? index, int? timeoutMs = null)
    {
        if (index == null)
            return ContainingScope;
        RunWithElement(e => SelectByIndexCore(e, index.Value), timeoutMs);
        return ContainingScope;
    }

    protected virtual void SelectByIndexCore(IWinFormsElement element, int index)
    {
        if (element is Interfaces.IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            expander.SelectItemByIndex(index);
        }
    }

    public virtual TScope SelectByValue(string? value, int? timeoutMs = null)
    {
        return SelectByText(value, timeoutMs);
    }

    #endregion

    #region Selected State

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

    protected virtual string? GetSelectedTextCore(IWinFormsElement element)
    {
        if (element is Interfaces.IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            return expander.GetSelectedItemText();
        }
        return element.Text;
    }

    public bool WaitSelectedText(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetSelectedText() == expected, timeout);
    }

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

    public bool WaitSelectedIndex(int? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetSelectedIndex() == expected, timeout);
    }

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

    protected virtual IReadOnlyList<string>? GetItemTextsCore(IWinFormsElement element)
    {
        if (element is Interfaces.IExpandCollapsePatternElement expander && expander.SupportsExpandCollapse)
        {
            var items = expander.GetExpandedItems();
            return items?.Select(i => i.Text ?? "").ToList();
        }
        return null;
    }

    public virtual int? GetItemCount(int? timeoutMs = null)
    {
        return GetItemTexts(timeoutMs)?.Count;
    }

    public bool WaitItemCount(int? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetItemCount() == expected, timeout);
    }

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
