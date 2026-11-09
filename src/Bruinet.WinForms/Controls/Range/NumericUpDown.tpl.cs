using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Range;

/// <summary>
/// A numeric up-down: UI Automation shows it as a spinner with no pattern of its own, around an
/// edit that holds the number and its Up and Down buttons. The number is written to the edit and
/// committed with the buttons, then confirmed. The page object gives its bounds and step.
/// </summary>
/// <remarks>
/// WinForms takes an edit's text into the control's <c>Value</c> only when the focus leaves it. A
/// write through UI Automation moves no focus, so until it is committed the app reads the old
/// value, and the app setting that same old value does not refresh the text (the sample's Clear
/// left 9000 shown over 8080). Each spinner button first takes the text, so Up then Down (Down then
/// Up at the maximum) leaves the written number taken, with no focus or key.
/// </remarks>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class NumericUpDown<TScope> : ValueRangeControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    private static readonly Locator Edit = Locator.ByControlType("Edit");

    private static readonly Locator Buttons = Locator.ByControlType("Button");

    public NumericUpDown(IWinFormsScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = 100, double step = 1)
        : base(scope, locator, minimum, maximum, step)
    {
    }

    public NumericUpDown(IWinFormsScope<TScope> scope, string automationId, double minimum = 0, double? maximum = 100, double step = 1)
        : base(scope, automationId, minimum, maximum, step)
    {
    }

    /// <summary>The edit inside the spinner; the spinner itself when it has none.</summary>
    /// <param name="element">The spinner.</param>
    protected override IWinFormsElement ValueElement(IWinFormsElement element)
        => element.TryFindElement(Edit) ?? element;

    /// <summary>Commits the written number with the spinner's Up and Down buttons (in that order: the first is Up).</summary>
    /// <param name="element">The spinner.</param>
    /// <param name="target">The value written.</param>
    protected override void Commit(IWinFormsElement element, double target)
    {
        var buttons = element.FindElements(Buttons);
        if (buttons.Count < 2)
            return;

        var (up, down) = (buttons[0], buttons[1]);
        if (GetMaximumCore(element) is { } max && target >= max)
        {
            down.Click();
            up.Click();
        }
        else
        {
            up.Click();
            down.Click();
        }
    }
}
