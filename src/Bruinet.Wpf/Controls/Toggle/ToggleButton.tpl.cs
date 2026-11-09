using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Toggle;

/// <summary>
/// A toggle button, flipped through its Toggle pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ToggleButton<TScope> : ToggleControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public ToggleButton(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ToggleButton(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
