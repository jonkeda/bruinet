using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Toggle;

/// <summary>
/// A radio button, checked through its SelectionItem pattern. A radio button is unchecked by checking another.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class RadioButton<TScope> : ToggleControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public RadioButton(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public RadioButton(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
