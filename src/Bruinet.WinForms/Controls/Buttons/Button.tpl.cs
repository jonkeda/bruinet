using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Buttons;

/// <summary>
/// A button (<c>System.Windows.Controls.Button</c>), clicked through its Invoke pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Button<TScope> : ClickableControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public Button(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Button(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
