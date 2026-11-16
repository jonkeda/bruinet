using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Text;

/// <summary>
/// Text shown to the user (<c>UILabel</c>, XCUITest type <c>StaticText</c>): read and asserted, not edited.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Label<TScope> : ViewBase<TScope>, ITextControlObject<TScope>
    where TScope : INativeIosScope<TScope>
{
    public Label(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Label(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
