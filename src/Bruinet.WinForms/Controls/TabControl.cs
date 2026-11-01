using Bruinet.WinForms.FlaUI;
using FlaUI.Core.AutomationElements;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// WinForms TabControl with tab selection and navigation.
/// </summary>
public sealed class TabControl<TScope> : ControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public TabControl(IWinFormsScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public TabControl(IWinFormsScope<TScope> scope, string locatorValue) : base(scope, locatorValue) { }

    /// <summary>Gets the currently selected tab's text.</summary>
    public string? GetSelectedTabText(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var tab = flaui.Element.AsTab();
                return tab.SelectedTabItem?.Name;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Gets the currently selected tab's index.</summary>
    public int? GetSelectedTabIndex(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var tab = flaui.Element.AsTab();
                return tab.SelectedTabItemIndex;
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Selects a tab by index.</summary>
    public TScope SelectTabByIndex(int index, int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui)
            {
                var tab = flaui.Element.AsTab();
                tab.SelectTabItem(index);
            }
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Selects a tab by text.</summary>
    public TScope SelectTabByText(string text, int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui)
            {
                var tab = flaui.Element.AsTab();
                var items = tab.TabItems;
                var target = items.FirstOrDefault(p => p.Name == text);
                if (target != null)
                {
                    target.Click();
                }
            }
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Gets the text of all tabs.</summary>
    public IReadOnlyList<string>? GetTabTexts(int? timeoutMs = null)
    {
        try
        {
            var element = FindElement(timeoutMs);
            if (element is FlaUIWinFormsElement flaui)
            {
                var tab = flaui.Element.AsTab();
                return tab.TabItems.Select(p => p.Name ?? "").ToList();
            }
            return null;
        }
        catch { return null; }
    }

    /// <summary>Gets the number of tabs.</summary>
    public int? GetTabCount(int? timeoutMs = null)
    {
        return GetTabTexts(timeoutMs)?.Count;
    }

    /// <summary>Waits for the selected tab to match the expected text.</summary>
    public bool WaitSelectedTab(string? expected, int? timeoutMs = null)
    {
        if (expected == null)
            return true;
        var timeout = timeoutMs ?? DefaultTimeoutMs;
        return Poll(() => GetSelectedTabText() == expected, timeout);
    }

    /// <summary>Asserts the selected tab matches the expected text.</summary>
    public TScope AssertSelectedTab(string? expected, string? message = null, int? timeoutMs = null)
    {
        if (expected == null)
            return ContainingScope;
        if (!WaitSelectedTab(expected, timeoutMs))
        {
            var actual = GetSelectedTabText();
            throw new AssertionException(
                message ?? $"Expected selected tab '{expected}' for '{AutomationId}' but got '{actual}'",
                expected, actual, AutomationId);
        }
        return ContainingScope;
    }
}
