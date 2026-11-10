using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Toggle;

/// <summary>
/// A toggle button: checked and unchecked by clicking it once, and confirmed.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ToggleButton<TScope> : ToggleControlBase<TScope>
    where TScope : IStrideScope<TScope>
{
    public ToggleButton(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ToggleButton(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
