using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Selection;

/// <summary>
/// One wheel of a picker (<c>UIPickerView</c>, a wheels-style <c>UIDatePicker</c>): XCUITest type
/// <c>PickerWheel</c>. A picker with several wheels is one <see cref="PickerWheel{TScope}"/> per wheel.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class PickerWheel<TScope> : SelectorControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public PickerWheel(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public PickerWheel(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
