using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Text;

/// <summary>
/// The search field of a search bar (<c>UISearchBar</c>, XCUITest type <c>SearchField</c>). Submit
/// presses the keyboard's Search key.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class SearchField<TScope> : TextField<TScope>
    where TScope : INativeIosScope<TScope>
{
    public SearchField(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public SearchField(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
