using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Text;

/// <summary>
/// A multi-line text input (<c>UITextView</c>, XCUITest type <c>TextView</c>). Unlike Android's
/// <c>TextView</c> it is edited; read-only text is a <see cref="Label{TScope}"/>. Submit types a new line.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class TextView<TScope> : TextField<TScope>
    where TScope : INativeIosScope<TScope>
{
    public TextView(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public TextView(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
