using Bruinet.Core.Diagnostics;
using System.Drawing;
using Bruinet.Core;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Utilities;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Globalization;

namespace Bruinet.WinForms.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IWinFormsElement"/> for WinForms desktop apps.
/// </summary>
public sealed class FlaUIWinFormsElement : IWinFormsElement, IRangePatternElement, Interfaces.IExpandCollapsePatternElement
{
    private readonly AutomationElement _element;
    private readonly FlaUIWinFormsDriver _driver;

    public FlaUIWinFormsElement(AutomationElement element, FlaUIWinFormsDriver driver)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    #region State Properties

    public bool Visible
    {
        get
        {
            try
            {
                if (!_element.IsOffscreen) return true;
                var bounds = _element.BoundingRectangle;
                return bounds.Width > 0 && bounds.Height > 0;
            }
            catch { return false; }
        }
    }

    public bool Enabled => _element.IsEnabled;

    public bool Selected
    {
        get
        {
            if (_element.Patterns.SelectionItem.IsSupported)
                return _element.Patterns.SelectionItem.Pattern.IsSelected.Value;
            if (_element.Patterns.Toggle.IsSupported)
                return _element.Patterns.Toggle.Pattern.ToggleState.Value ==
                       global::FlaUI.Core.Definitions.ToggleState.On;
            return false;
        }
    }

    public string? Text
    {
        get
        {
            try
            {
                if (_element.Patterns.Value.IsSupported)
                    return _element.Patterns.Value.Pattern.Value.Value;
                if (_element.Patterns.RangeValue.IsSupported)
                    return _element.Patterns.RangeValue.Pattern.Value.Value.ToString(CultureInfo.InvariantCulture);
                return _element.Properties.Name.ValueOrDefault;
            }
            catch { return null; }
        }
    }

    public string? TagName => _element.ControlType.ToString();
    public Point Location => new(_element.BoundingRectangle.X, _element.BoundingRectangle.Y);
    public Size Size => new(_element.BoundingRectangle.Width, _element.BoundingRectangle.Height);
    public Rectangle Rect => new(Location, Size);

    #endregion

    #region Actions

    public void Click()
    {
        _driver.EnsureRootWindowFocused();

        if (_element.Patterns.Invoke.IsSupported)
        {
            _element.Patterns.Invoke.Pattern.Invoke();
            return;
        }

        var rect = _element.BoundingRectangle;
        if (rect.Width > 0 && rect.Height > 0)
        {
            var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            PhysicalInput.Used("FlaUIWinFormsElement.Click(pointer fallback)", "an activation pattern");
            Mouse.Position = center;
            Mouse.Click(MouseButton.Left);
            return;
        }

        throw new InvalidOperationException($"Element is not clickable: no Invoke pattern and empty bounds ({rect}).");
    }

    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
    {
        switch (method)
        {
            case TextInputMethod.Keys:
                PhysicalInput.Used("FlaUIWinFormsElement.SendKeys(Keys)", "a SetText verb");
                _element.Focus();
                Keyboard.Type(text);
                break;
            case TextInputMethod.Paste:
                PhysicalInput.Used("FlaUIWinFormsElement.SendKeys(Paste)", "a SetText verb - also destroys the user's clipboard");
                _element.Focus();
                System.Windows.Forms.Clipboard.SetText(text);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                break;
            case TextInputMethod.SetValue:
                PhysicalInput.Used("FlaUIWinFormsElement.SendKeys(SetValue)", "a SetText verb");
                if (_element.Patterns.Value.IsSupported)
                    _element.Patterns.Value.Pattern.SetValue(text);
                else
                {
                    _element.Focus();
                    Keyboard.Type(text);
                }
                break;
        }
    }

    public void Clear()
    {
        PhysicalInput.Used("FlaUIWinFormsElement.Clear", "a ValuePattern write");
        if (_element.Patterns.Value.IsSupported)
            _element.Patterns.Value.Pattern.SetValue(string.Empty);
        else
        {
            _element.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
        }
    }

    public void DoubleClick() => _element.DoubleClick();
    public void RightClick() => _element.RightClick();

    public void Hover()
    {
        PhysicalInput.Used("FlaUIWinFormsElement.Hover", "a pointer-enter verb");
        var rect = _element.BoundingRectangle;
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Mouse.MoveTo(center);
    }

    public void LongPress(int durationMs = 1000)
    {
        var rect = _element.BoundingRectangle;
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Mouse.Position = center;
        Mouse.Down(MouseButton.Left);
        WaitHelper.Pause(durationMs);
        Mouse.Up(MouseButton.Left);
    }

    public void ScrollIntoView(int timeoutMs = 5000)
    {
        if (_element.Patterns.ScrollItem.IsSupported)
        {
            _element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            return;
        }

        var parent = _element.Parent;
        while (parent != null)
        {
            if (parent.Patterns.Scroll.IsSupported)
            {
                var parentRect = parent.BoundingRectangle;
                var elementRect = _element.BoundingRectangle;

                if (elementRect.Bottom > parentRect.Bottom)
                    parent.Patterns.Scroll.Pattern.Scroll(
                        global::FlaUI.Core.Definitions.ScrollAmount.NoAmount,
                        global::FlaUI.Core.Definitions.ScrollAmount.LargeIncrement);
                else if (elementRect.Top < parentRect.Top)
                    parent.Patterns.Scroll.Pattern.Scroll(
                        global::FlaUI.Core.Definitions.ScrollAmount.NoAmount,
                        global::FlaUI.Core.Definitions.ScrollAmount.LargeDecrement);
                break;
            }
            parent = parent.Parent;
        }
    }

    public void Swipe(int startX, int startY, int endX, int endY, int durationMs = 500)
    {
        Mouse.MoveTo(new Point(startX, startY));
        Mouse.Down(MouseButton.Left);
        var steps = Math.Max(10, durationMs / 50);
        var dx = (endX - startX) / (double)steps;
        var dy = (endY - startY) / (double)steps;
        var stepDelay = durationMs / steps;
        for (int i = 1; i <= steps; i++)
        {
            Mouse.MoveTo(new Point((int)(startX + dx * i), (int)(startY + dy * i)));
            WaitHelper.Pause(stepDelay);
        }
        Mouse.Up(MouseButton.Left);
    }

    #endregion

    #region Element Finding

    public IWinFormsElement FindElement(Locator locator, int timeoutMs = 5000)
    {
        var condition = locator.ToCondition(_driver.ConditionFactory);
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        do
        {
            var found = _element.FindFirstDescendant(condition);
            if (found != null)
                return new FlaUIWinFormsElement(found, _driver);
            if (timeoutMs <= 0) break;
            WaitHelper.Pause(100);
        } while (DateTime.UtcNow - startTime < timeout);

        throw new ElementNotFoundException(locator);
    }

    public IReadOnlyList<IWinFormsElement> FindElements(Locator locator, int timeoutMs = 0)
    {
        var condition = locator.ToCondition(_driver.ConditionFactory);
        if (timeoutMs > 0)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            while (DateTime.UtcNow - startTime < timeout)
            {
                var found = _element.FindAllDescendants(condition);
                if (found.Length > 0)
                    return found.Select(e => new FlaUIWinFormsElement(e, _driver)).ToList();
                WaitHelper.Pause(100);
            }
        }
        var elements = _element.FindAllDescendants(condition);
        return elements.Select(e => new FlaUIWinFormsElement(e, _driver)).ToList();
    }

    public bool TryFindElement(Locator locator, out IWinFormsElement? element, int timeoutMs = 0)
    {
        try { element = FindElement(locator, timeoutMs); return true; }
        catch (ElementNotFoundException) { element = null; return false; }
    }

    #endregion

    #region Attribute Access

    public string? GetAttribute(string attributeName)
    {
        try
        {
            return attributeName.ToLowerInvariant() switch
            {
                "name" => _element.Properties.Name.ValueOrDefault,
                "automationid" => _element.Properties.AutomationId.ValueOrDefault,
                "classname" or "class" => _element.Properties.ClassName.ValueOrDefault,
                "controltype" => _element.ControlType.ToString(),
                "enabled" => _element.IsEnabled.ToString(),
                "visible" => (!_element.IsOffscreen).ToString(),
                "helptext" => _element.Properties.HelpText.ValueOrDefault,
                _ => null
            };
        }
        catch { return null; }
    }

    public string? GetDomAttribute(string attributeName) => null;
    public string? GetDomProperty(string propertyName) => null;
    public string? GetCssValue(string propertyName) => null;

    #endregion

    internal AutomationElement Element => _element;

    #region IRangePatternElement

    public bool SupportsRangeValue
    {
        get
        {
            try { return _element.Patterns.RangeValue.IsSupported; }
            catch { return false; }
        }
    }

    public bool SetRangeValue(double value)
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported) return false;
            var pattern = _element.Patterns.RangeValue.Pattern;
            var clamped = Math.Max(pattern.Minimum.Value, Math.Min(pattern.Maximum.Value, value));
            pattern.SetValue(clamped);
            return true;
        }
        catch { return false; }
    }

    public double? GetRangeValue()
    {
        try { return _element.Patterns.RangeValue.IsSupported ? _element.Patterns.RangeValue.Pattern.Value.Value : null; }
        catch { return null; }
    }

    public double? GetRangeMinimum()
    {
        try { return _element.Patterns.RangeValue.IsSupported ? _element.Patterns.RangeValue.Pattern.Minimum.Value : null; }
        catch { return null; }
    }

    public double? GetRangeMaximum()
    {
        try { return _element.Patterns.RangeValue.IsSupported ? _element.Patterns.RangeValue.Pattern.Maximum.Value : null; }
        catch { return null; }
    }

    public double? GetRangeSmallChange()
    {
        try { return _element.Patterns.RangeValue.IsSupported ? _element.Patterns.RangeValue.Pattern.SmallChange.Value : null; }
        catch { return null; }
    }

    #endregion

    #region IExpandCollapsePatternElement

    public bool SupportsExpandCollapse => _element.Patterns.ExpandCollapse.IsSupported;

    public bool IsExpanded
    {
        get
        {
            if (!_element.Patterns.ExpandCollapse.IsSupported) return false;
            return _element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value ==
                   global::FlaUI.Core.Definitions.ExpandCollapseState.Expanded;
        }
    }

    public void Expand()
    {
        if (_element.Patterns.ExpandCollapse.IsSupported)
            _element.Patterns.ExpandCollapse.Pattern.Expand();
    }

    public void Collapse()
    {
        if (_element.Patterns.ExpandCollapse.IsSupported)
            _element.Patterns.ExpandCollapse.Pattern.Collapse();
    }

    public IReadOnlyList<IWinFormsElement>? GetExpandedItems()
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported) return null;

        var wasExpanded = IsExpanded;
        if (!wasExpanded) Expand();

        try
        {
            AutomationElement[] items = [];
            LegacyWaitHelper.WaitFor(() =>
            {
                items = _element.FindAllDescendants(cf =>
                    cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.ListItem));
                if (items.Length > 0) return true;
                items = _element.FindAllChildren(cf =>
                    cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.ListItem));
                return items.Length > 0;
            }, timeoutMs: 2000, pollingIntervalMs: 50);

            return items.Select(e => new FlaUIWinFormsElement(e, _driver) as IWinFormsElement).ToList();
        }
        finally
        {
            if (!wasExpanded) Collapse();
        }
    }

    public void SelectItemByText(string text)
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported) return;

        _element.Patterns.ExpandCollapse.Pattern.Expand();
        LegacyWaitHelper.WaitFor(() => IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);

        AutomationElement[] items = [];
        LegacyWaitHelper.WaitFor(() =>
        {
            items = _element.FindAllDescendants(cf =>
                cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.ListItem));
            return items.Length > 0;
        }, timeoutMs: 2000, pollingIntervalMs: 50);

        var target = items.FirstOrDefault(i => i.Name == text);
        if (target == null) { Collapse(); return; }

        if (target.Patterns.SelectionItem.IsSupported)
            target.Patterns.SelectionItem.Pattern.Select();
        else
            target.Click();

        LegacyWaitHelper.WaitFor(() => !IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);
        if (IsExpanded) Collapse();
    }

    public void SelectItemByIndex(int index)
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported) return;

        _element.Patterns.ExpandCollapse.Pattern.Expand();
        LegacyWaitHelper.WaitFor(() => IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);

        AutomationElement[] items = [];
        LegacyWaitHelper.WaitFor(() =>
        {
            items = _element.FindAllDescendants(cf =>
                cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.ListItem));
            return items.Length > 0;
        }, timeoutMs: 2000, pollingIntervalMs: 50);

        if (index >= items.Length) { Collapse(); return; }

        var item = items[index];
        if (item.Patterns.SelectionItem.IsSupported)
            item.Patterns.SelectionItem.Pattern.Select();
        else
            item.Click();

        LegacyWaitHelper.WaitFor(() => !IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);
        if (IsExpanded) Collapse();
    }

    public string? GetSelectedItemText()
    {
        if (!_element.Patterns.Selection.IsSupported) return null;
        var selection = _element.Patterns.Selection.Pattern.Selection.Value;
        if (selection == null || selection.Length == 0) return null;
        return selection[0].Name;
    }

    #endregion
}
