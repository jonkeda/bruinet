using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Buttons;

/// <summary>
/// A button (<c>System.Windows.Controls.Button</c>), clicked through its Invoke pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Button<TScope> : ClickableControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public Button(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Button(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
