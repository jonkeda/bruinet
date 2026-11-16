using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Selection;

/// <summary>
/// One wheel of a picker (<c>UIPickerView</c>, a wheels-style <c>UIDatePicker</c>): XCUITest type
/// <c>PickerWheel</c>. The accessibility identifier is on the picker and its wheels have none, so a
/// locator that finds the picker reads the wheel at <c>wheel</c> inside it. A picker with several
/// wheels is one <see cref="PickerWheel{TScope}"/> per wheel.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class PickerWheel<TScope> : SelectorControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    private static readonly Locator Wheels = Locator.ByClassName("XCUIElementTypePickerWheel");

    private readonly int _wheel;

    public PickerWheel(INativeIosScope<TScope> scope, Locator locator, int wheel = 0)
        : base(scope, locator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wheel);
        _wheel = wheel;
    }

    public PickerWheel(INativeIosScope<TScope> scope, string identifierOrXPath, int wheel = 0)
        : base(scope, identifierOrXPath)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(wheel);
        _wheel = wheel;
    }

    /// <inheritdoc />
    protected override INativeIosElement? TryFindElement()
    {
        var found = base.TryFindElement();
        if (found is null || found.ElementType != "XCUIElementTypePicker")
        {
            return found;
        }

        var wheels = found.FindElements(Wheels);
        return _wheel < wheels.Count ? wheels[_wheel] : null;
    }
}
