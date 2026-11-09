using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Display;

/// <summary>
/// Text the app shows: a <c>TextBlock</c> or a <c>Label</c>. Its text is its UI Automation name.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Label<TScope> : ViewBase<TScope>, ITextControlObject<TScope>
    where TScope : IWpfScope<TScope>
{
    public Label(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Label(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
