using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Toggle;

/// <summary>
/// A check box, checked and unchecked through its Toggle pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class CheckBox<TScope> : ToggleControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public CheckBox(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public CheckBox(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
