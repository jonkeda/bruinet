using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Toggle;

/// <summary>
/// A radio button, checked through its SelectionItem pattern. A radio button is unchecked by checking another.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class RadioButton<TScope> : ToggleControlBase<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public RadioButton(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public RadioButton(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
