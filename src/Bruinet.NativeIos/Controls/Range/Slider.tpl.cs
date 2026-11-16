using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Range;

/// <summary>
/// A slider (<c>UISlider</c>). Give its bounds; XCUITest reports only the position, as a percentage.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Slider<TScope> : RangeControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public Slider(INativeIosScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, locator, minimum, maximum, step)
    {
    }

    public Slider(INativeIosScope<TScope> scope, string identifierOrXPath, double minimum = 0, double? maximum = null, double step = 1)
        : base(scope, identifierOrXPath, minimum, maximum, step)
    {
    }
}
