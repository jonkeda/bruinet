using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.WinForms.Interfaces;

namespace Bruinet.WinForms.Controls.Display;

/// <summary>
/// Text the app shows: a <c>Label</c>. Its text is its UI Automation name.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Label<TScope> : ViewBase<TScope>, ITextControlObject<TScope>
    where TScope : IWinFormsScope<TScope>
{
    public Label(IWinFormsScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Label(IWinFormsScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
