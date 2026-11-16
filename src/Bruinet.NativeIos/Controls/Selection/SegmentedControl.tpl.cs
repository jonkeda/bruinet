using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Selection;

/// <summary>
/// A segmented control (<c>UISegmentedControl</c>): each segment is a button labelled with its
/// title, and the selected one reports itself selected.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class SegmentedControl<TScope> : ChoiceGroupBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public SegmentedControl(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public SegmentedControl(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    /// <inheritdoc />
    protected override Locator ChoicesLocator => Locator.ByClassName("XCUIElementTypeButton");

    /// <inheritdoc />
    protected override bool IsChosen(INativeIosElement choice) => choice.Selected;
}
