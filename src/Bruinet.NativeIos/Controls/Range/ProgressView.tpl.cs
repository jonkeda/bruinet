using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Range;

/// <summary>
/// A progress view (<c>UIProgressView</c>): read and asserted; the app sets it. XCUITest reports it as a percentage.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ProgressView<TScope> : RangeControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public ProgressView(INativeIosScope<TScope> scope, Locator locator, double minimum = 0, double? maximum = 100, double step = 1)
        : base(scope, locator, minimum, maximum, step)
    {
    }

    public ProgressView(INativeIosScope<TScope> scope, string identifierOrXPath, double minimum = 0, double? maximum = 100, double step = 1)
        : base(scope, identifierOrXPath, minimum, maximum, step)
    {
    }
}
