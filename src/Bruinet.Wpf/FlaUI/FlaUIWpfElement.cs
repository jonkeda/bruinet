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

namespace Bruinet.Wpf.FlaUI;

/// <summary>
/// FlaUI-based implementation of <see cref="IWpfElement"/> for WPF desktop apps.
/// Also implements pattern-based interfaces for enhanced Windows Automation support.
/// </summary>
public sealed class FlaUIWpfElement : IWpfElement, IRangePatternElement, IExpandCollapsePatternElement
{
    private readonly AutomationElement _element;
    private readonly FlaUIWpfDriver _driver;

    /// <summary>
    /// Creates a new FlaUIWpfElement wrapper.
    /// </summary>
    public FlaUIWpfElement(AutomationElement element, FlaUIWpfDriver driver)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    #region State Properties (IElement<IWpfElement>)

    /// <inheritdoc />
    public bool Visible
    {
        get
        {
            try
            {
                if (!_element.IsOffscreen)
                    return true;

                // Fallback: check if element has a valid bounding rectangle
                var bounds = _element.BoundingRectangle;
                if (bounds.Width > 0 && bounds.Height > 0)
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public bool Enabled => _element.IsEnabled;

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            catch
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public string? TagName => _element.ControlType.ToString();

    /// <inheritdoc />
    public Point Location => new(_element.BoundingRectangle.X, _element.BoundingRectangle.Y);

    /// <inheritdoc />
    public Size Size => new(_element.BoundingRectangle.Width, _element.BoundingRectangle.Height);

    /// <inheritdoc />
    public Rectangle Rect => new(Location, Size);

    #endregion

    #region Actions (IElement<IWpfElement>)

    /// <inheritdoc />
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
            PhysicalInput.Used("FlaUIWpfElement.Click(pointer fallback)", "an activation pattern");
            Mouse.Position = center;
            Mouse.Click(MouseButton.Left);
            return;
        }

        throw new InvalidOperationException(
            $"Element is not clickable: Invoke pattern not supported and bounds are empty ({rect}).");
    }

    /// <inheritdoc />
    public void SendKeys(string text, TextInputMethod method = TextInputMethod.Keys)
    {
        switch (method)
        {
            case TextInputMethod.Keys:
                PhysicalInput.Used("FlaUIWpfElement.SendKeys(Keys)", "a SetText verb");
                _element.Focus();
                Keyboard.Type(text);
                break;
            case TextInputMethod.Paste:
                PhysicalInput.Used("FlaUIWpfElement.SendKeys(Paste)", "a SetText verb - also destroys the user's clipboard");
                _element.Focus();
                System.Windows.Forms.Clipboard.SetText(text);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                break;
            case TextInputMethod.SetValue:
                PhysicalInput.Used("FlaUIWpfElement.SendKeys(SetValue)", "a SetText verb");
                if (_element.Patterns.Value.IsSupported)
                {
                    _element.Patterns.Value.Pattern.SetValue(text);
                }
                else
                {
                    _element.Focus();
                    Keyboard.Type(text);
                }
                break;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        PhysicalInput.Used("FlaUIWpfElement.Clear", "a ValuePattern write");
        if (_element.Patterns.Value.IsSupported)
        {
            _element.Patterns.Value.Pattern.SetValue(string.Empty);
        }
        else
        {
            _element.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
        }
    }

    /// <inheritdoc />
    public void DoubleClick()
    {
        PhysicalInput.Used("FlaUIWpfElement.DoubleClick", "a DoubleTap verb");
        _element.DoubleClick();
    }

    /// <inheritdoc />
    public void RightClick()
    {
        PhysicalInput.Used("FlaUIWpfElement.RightClick", "an InvokeMenuItem verb");
        _element.RightClick();
    }

    /// <inheritdoc />
    public void Hover()
    {
        PhysicalInput.Used("FlaUIWpfElement.Hover", "a pointer-enter verb");
        var rect = _element.BoundingRectangle;
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Mouse.MoveTo(center);
    }

    /// <inheritdoc />
    public void LongPress(int durationMs = 1000)
    {
        PhysicalInput.Used("FlaUIWpfElement.LongPress", "a LongPress verb");
        var rect = _element.BoundingRectangle;
        var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Mouse.Position = center;
        Mouse.Down(MouseButton.Left);
        WaitHelper.Pause(durationMs);
        Mouse.Up(MouseButton.Left);
    }

    /// <inheritdoc />
    public void ScrollIntoView(int timeoutMs = 5000)
    {
        if (_element.Patterns.ScrollItem.IsSupported)
        {
            _element.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }
        else
        {
            var parent = _element.Parent;
            while (parent != null)
            {
                if (parent.Patterns.Scroll.IsSupported)
                {
                    var parentRect = parent.BoundingRectangle;
                    var elementRect = _element.BoundingRectangle;

                    if (elementRect.Bottom > parentRect.Bottom)
                    {
                        parent.Patterns.Scroll.Pattern.Scroll(
                            global::FlaUI.Core.Definitions.ScrollAmount.NoAmount,
                            global::FlaUI.Core.Definitions.ScrollAmount.LargeIncrement);
                    }
                    else if (elementRect.Top < parentRect.Top)
                    {
                        parent.Patterns.Scroll.Pattern.Scroll(
                            global::FlaUI.Core.Definitions.ScrollAmount.NoAmount,
                            global::FlaUI.Core.Definitions.ScrollAmount.LargeDecrement);
                    }
                    break;
                }
                parent = parent.Parent;
            }
        }
    }

    /// <inheritdoc />
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
            var x = (int)(startX + dx * i);
            var y = (int)(startY + dy * i);
            Mouse.MoveTo(new Point(x, y));
            WaitHelper.Pause(stepDelay);
        }

        Mouse.Up(MouseButton.Left);
    }

    #endregion

    #region Element Finding (IElement<IWpfElement>)

    /// <inheritdoc />
    public IWpfElement FindElement(Locator locator, int timeoutMs = 5000)
    {
        var condition = locator.ToCondition(_driver.ConditionFactory);

        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);

        do
        {
            var found = _element.FindFirstDescendant(condition);
            if (found != null)
                return new FlaUIWpfElement(found, _driver);

            if (timeoutMs <= 0) break;
            WaitHelper.Pause(100);
        }
        while (DateTime.UtcNow - startTime < timeout);

        throw new ElementNotFoundException(locator);
    }

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement> FindElements(Locator locator, int timeoutMs = 0)
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
                    return found.Select(e => new FlaUIWpfElement(e, _driver)).ToList();
                WaitHelper.Pause(100);
            }
        }

        var elements = _element.FindAllDescendants(condition);
        return elements.Select(e => new FlaUIWpfElement(e, _driver)).ToList();
    }

    /// <inheritdoc />
    public bool TryFindElement(Locator locator, out IWpfElement? element, int timeoutMs = 0)
    {
        try
        {
            element = FindElement(locator, timeoutMs);
            return true;
        }
        catch (ElementNotFoundException)
        {
            element = null;
            return false;
        }
    }

    #endregion

    #region Attribute Access

    /// <inheritdoc />
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
                "scroll.verticalscrollpercent" => GetScrollPatternValue(p => p.VerticalScrollPercent.Value),
                "scroll.horizontalscrollpercent" => GetScrollPatternValue(p => p.HorizontalScrollPercent.Value),
                "scroll.verticallyscrollable" => GetScrollPatternBool(p => p.VerticallyScrollable.Value),
                "scroll.horizontallyscrollable" => GetScrollPatternBool(p => p.HorizontallyScrollable.Value),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private string? GetScrollPatternValue(Func<global::FlaUI.Core.Patterns.IScrollPattern, double> accessor)
    {
        if (!_element.Patterns.Scroll.IsSupported)
            return null;
        var value = accessor(_element.Patterns.Scroll.Pattern);
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private string? GetScrollPatternBool(Func<global::FlaUI.Core.Patterns.IScrollPattern, bool> accessor)
    {
        if (!_element.Patterns.Scroll.IsSupported)
            return null;
        return accessor(_element.Patterns.Scroll.Pattern).ToString();
    }

    /// <inheritdoc />
    public string? GetDomAttribute(string attributeName) => null;

    /// <inheritdoc />
    public string? GetDomProperty(string propertyName) => null;

    /// <inheritdoc />
    public string? GetCssValue(string propertyName) => null;

    #endregion

    #region Internal

    /// <summary>
    /// Gets the underlying FlaUI AutomationElement for internal use.
    /// </summary>
    internal AutomationElement Element => _element;

    #endregion

    #region IRangePatternElement Implementation

    /// <inheritdoc />
    public bool SupportsRangeValue
    {
        get
        {
            try { return _element.Patterns.RangeValue.IsSupported; }
            catch { return false; }
        }
    }

    /// <inheritdoc />
    public bool SetRangeValue(double value)
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported)
                return false;

            var pattern = _element.Patterns.RangeValue.Pattern;
            var min = pattern.Minimum.Value;
            var max = pattern.Maximum.Value;
            var clampedValue = Math.Max(min, Math.Min(max, value));

            pattern.SetValue(clampedValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public double? GetRangeValue()
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported)
                return null;
            return _element.Patterns.RangeValue.Pattern.Value.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public double? GetRangeMinimum()
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported)
                return null;
            return _element.Patterns.RangeValue.Pattern.Minimum.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public double? GetRangeMaximum()
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported)
                return null;
            return _element.Patterns.RangeValue.Pattern.Maximum.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public double? GetRangeSmallChange()
    {
        try
        {
            if (!_element.Patterns.RangeValue.IsSupported)
                return null;
            return _element.Patterns.RangeValue.Pattern.SmallChange.Value;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region IExpandCollapsePatternElement Implementation

    /// <inheritdoc />
    public bool SupportsExpandCollapse => _element.Patterns.ExpandCollapse.IsSupported;

    /// <inheritdoc />
    public bool IsExpanded
    {
        get
        {
            if (!_element.Patterns.ExpandCollapse.IsSupported)
                return false;
            return _element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value ==
                   global::FlaUI.Core.Definitions.ExpandCollapseState.Expanded;
        }
    }

    /// <inheritdoc />
    public bool Expand()
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported)
            return false;

        _element.Patterns.ExpandCollapse.Pattern.Expand();

        LegacyWaitHelper.WaitFor(
            () => _element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value ==
                  global::FlaUI.Core.Definitions.ExpandCollapseState.Expanded,
            timeoutMs: 2000,
            pollingIntervalMs: 50);

        return IsExpanded;
    }

    /// <inheritdoc />
    public bool Collapse()
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported)
            return false;

        _element.Patterns.ExpandCollapse.Pattern.Collapse();
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<IWpfElement>? GetExpandedItems()
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported)
            return null;

        var wasExpanded = IsExpanded;

        if (!wasExpanded)
        {
            if (!Expand())
                return null;
        }

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

            return items.Select(e => new FlaUIWpfElement(e, _driver) as IWpfElement).ToList();
        }
        finally
        {
            if (!wasExpanded)
                Collapse();
        }
    }

    /// <inheritdoc />
    public bool SelectItemByText(string text)
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported)
            return false;

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
        if (target == null)
        {
            Collapse();
            return false;
        }

        if (target.Patterns.SelectionItem.IsSupported)
            target.Patterns.SelectionItem.Pattern.Select();
        else
            target.Click();

        LegacyWaitHelper.WaitFor(() => !IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);

        if (IsExpanded)
            Collapse();

        return true;
    }

    /// <inheritdoc />
    public bool SelectItemByIndex(int index)
    {
        if (!_element.Patterns.ExpandCollapse.IsSupported)
            return false;

        _element.Patterns.ExpandCollapse.Pattern.Expand();
        LegacyWaitHelper.WaitFor(() => IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);

        AutomationElement[] items = [];
        LegacyWaitHelper.WaitFor(() =>
        {
            items = _element.FindAllDescendants(cf =>
                cf.ByControlType(global::FlaUI.Core.Definitions.ControlType.ListItem));
            return items.Length > 0;
        }, timeoutMs: 2000, pollingIntervalMs: 50);

        if (index >= items.Length)
        {
            Collapse();
            return false;
        }

        var item = items[index];

        if (item.Patterns.SelectionItem.IsSupported)
            item.Patterns.SelectionItem.Pattern.Select();
        else
            item.Click();

        LegacyWaitHelper.WaitFor(() => !IsExpanded, timeoutMs: 2000, pollingIntervalMs: 50);

        if (IsExpanded)
            Collapse();

        return true;
    }

    /// <inheritdoc />
    public string? GetSelectedItemText()
    {
        if (!_element.Patterns.Selection.IsSupported)
            return null;

        var selection = _element.Patterns.Selection.Pattern.Selection.Value;
        if (selection == null || selection.Length == 0)
            return null;

        return selection[0].Name;
    }

    #endregion
}
