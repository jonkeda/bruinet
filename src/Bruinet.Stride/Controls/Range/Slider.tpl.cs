using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Range;

/// <summary>
/// A slider: its value set in the game, clamped to its range, and confirmed.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Slider<TScope> : RangeControlBase<TScope>
    where TScope : IStrideScope<TScope>
{
    public Slider(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Slider(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
