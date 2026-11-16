using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Toggle;

/// <summary>
/// A switch (<c>UISwitch</c>): XCUITest reports it on as the value <c>1</c>.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Switch<TScope> : ToggleControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public Switch(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Switch(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
