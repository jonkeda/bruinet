using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Buttons;

/// <summary>
/// A button (<c>UIButton</c>, a bar button item, a cell accessory): XCUITest type <c>Button</c>.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Button<TScope> : ClickableControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public Button(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Button(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
