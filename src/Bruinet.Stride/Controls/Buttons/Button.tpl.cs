using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Buttons;

/// <summary>
/// A button: a click raises its Click event in the game.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Button<TScope> : ClickableControlBase<TScope>
    where TScope : IStrideScope<TScope>
{
    public Button(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Button(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
