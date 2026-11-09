using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Range;

/// <summary>
/// A track bar: UI Automation shows it as a slider whose position is the text of its Value pattern,
/// with no RangeValue, so it is set by writing that text and confirmed. The page object gives its
/// bounds and step.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TrackBar<TScope> : ValueRangeControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public TrackBar(IWinFormsScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = 10, double step = 1)
        : base(scope, locator, minimum, maximum, step)
    {
    }

    public TrackBar(IWinFormsScope<TScope> scope, string automationId, double minimum = 0, double? maximum = 10, double step = 1)
        : base(scope, automationId, minimum, maximum, step)
    {
    }
}
