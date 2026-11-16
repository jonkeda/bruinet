using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Text;

/// <summary>
/// A secure text field (<c>UITextField</c> with secure entry, XCUITest type <c>SecureTextField</c>):
/// it reads as dots, so its text is confirmed by length.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class SecureTextField<TScope> : TextField<TScope>
    where TScope : INativeIosScope<TScope>
{
    public SecureTextField(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public SecureTextField(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
