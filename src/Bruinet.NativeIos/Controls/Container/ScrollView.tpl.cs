using Bruinet.Core.Locators;
using Bruinet.NativeIos.Interfaces;

namespace Bruinet.NativeIos.Controls.Container;

/// <summary>
/// A scroll view (<c>UIScrollView</c>).
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ScrollView<TScope> : ScrollableControlBase<TScope>
    where TScope : INativeIosScope<TScope>
{
    public ScrollView(INativeIosScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ScrollView(INativeIosScope<TScope> scope, string identifierOrXPath)
        : base(scope, identifierOrXPath)
    {
    }
}
