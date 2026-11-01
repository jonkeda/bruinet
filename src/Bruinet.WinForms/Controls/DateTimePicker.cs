using Bruinet.Core.Diagnostics;
using Bruinet.Core.Utilities;
using Bruinet.WinForms.FlaUI;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Globalization;

namespace Bruinet.WinForms.Controls;

/// <summary>
/// WinForms DateTimePicker control with segment-based date navigation.
/// </summary>
public sealed class DateTimePicker<TScope> : ControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public DateTimePicker(IWinFormsScope<TScope> scope, Locator locator) : base(scope, locator) { }
    public DateTimePicker(IWinFormsScope<TScope> scope, string locatorValue) : base(scope, locatorValue) { }

    /// <summary>Sets the date value using the Value pattern if supported, else keyboard fallback.</summary>
    public TScope SetValue(string? value, int? timeoutMs = null)
    {
        if (value == null) return ContainingScope;
        return Run("SetValue", e =>
        {
            if (e is FlaUIWinFormsElement flaui &&
                flaui.Element.Patterns.Value.IsSupported)
            {
                flaui.Element.Patterns.Value.Pattern.SetValue(value);
                return;
            }

            // Keyboard fallback: select all, type, enter
            e.Click();
            PhysicalInput.Used("WinForms.DateTimePicker.SetText", "a SetDate verb");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(value);
            Keyboard.Type(VirtualKeyShort.ENTER);
        }, value: value, timeoutMs: timeoutMs);
    }

    /// <summary>
    /// Sets the date by adjusting individual segments (Year, Month, Day) using arrow keys.
    /// Navigates segments to avoid day clamping issues (sets year first, then month, then day).
    /// </summary>
    public TScope SetDate(DateTime date, int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            e.Click();

            // Navigate to the start (Home) and use arrow keys for segments.
            // Typical WinForms DTP format: MM/dd/yyyy
            // Home → Month segment, Right → Day, Right → Year
            PhysicalInput.Used("WinForms.DateTimePicker.SetDate(keys)", "a SetDate verb");
            Keyboard.Type(VirtualKeyShort.HOME);

            // Set in Year→Month→Day order to avoid clamping:
            // First navigate to year (Right, Right from Home for MM/dd/yyyy)
            Keyboard.Type(VirtualKeyShort.RIGHT);
            Keyboard.Type(VirtualKeyShort.RIGHT);

            // Type year digits
            Keyboard.Type(date.Year.ToString(CultureInfo.InvariantCulture));

            // Navigate to month (Home)
            Keyboard.Type(VirtualKeyShort.HOME);
            Keyboard.Type(date.Month.ToString(CultureInfo.InvariantCulture));

            // Navigate to day (Right from month)
            Keyboard.Type(VirtualKeyShort.RIGHT);
            Keyboard.Type(date.Day.ToString(CultureInfo.InvariantCulture));

            Keyboard.Type(VirtualKeyShort.ENTER);
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Opens the calendar dropdown if supported.</summary>
    public TScope OpenCalendar(int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui &&
                flaui.Element.Patterns.ExpandCollapse.IsSupported)
            {
                flaui.Element.Patterns.ExpandCollapse.Pattern.Expand();
            }
        }, timeoutMs);
        return ContainingScope;
    }

    /// <summary>Closes the calendar dropdown if supported.</summary>
    public TScope CloseCalendar(int? timeoutMs = null)
    {
        RunWithElement(e =>
        {
            if (e is FlaUIWinFormsElement flaui &&
                flaui.Element.Patterns.ExpandCollapse.IsSupported)
            {
                flaui.Element.Patterns.ExpandCollapse.Pattern.Collapse();
            }
        }, timeoutMs);
        return ContainingScope;
    }
}
