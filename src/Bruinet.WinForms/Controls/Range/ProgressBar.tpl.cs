using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Range;

/// <summary>
/// A progress bar: its value, read through the RangeValue pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ProgressBar<TScope> : RangeControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public ProgressBar(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ProgressBar(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
