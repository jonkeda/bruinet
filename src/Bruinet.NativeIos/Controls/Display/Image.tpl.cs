using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Display;

/// <summary>
/// An image (<c>UIImageView</c>); its accessibility label says what it shows.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Image<TScope> : ViewBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public Image(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Image(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
