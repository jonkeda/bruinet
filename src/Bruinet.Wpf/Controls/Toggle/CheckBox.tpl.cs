using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Toggle;

/// <summary>
/// A check box, checked and unchecked through its Toggle pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class CheckBox<TScope> : ToggleControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public CheckBox(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public CheckBox(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
