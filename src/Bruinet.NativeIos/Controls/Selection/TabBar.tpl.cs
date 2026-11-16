using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Selection;

/// <summary>
/// A tab bar (<c>UITabBar</c>): each tab is a button labelled with its title, and the selected one
/// reports itself selected.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TabBar<TScope> : ChoiceGroupBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public TabBar(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TabBar(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }

    /// <inheritdoc />
    protected override Locator ChoicesLocator => Locator.ByClassName("XCUIElementTypeButton");

    /// <inheritdoc />
    protected override bool IsChosen(INativeIosElement choice) => choice.Selected;
}
