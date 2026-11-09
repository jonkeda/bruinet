using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Range;

/// <summary>
/// A slider, set through its RangeValue pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Slider<TScope> : RangeControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public Slider(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Slider(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
