using Bruinet.Core.Locators;
using Bruinet.Stride.Interfaces;

namespace Bruinet.Stride.Controls.Display;

/// <summary>
/// An image (Stride's <c>ImageElement</c>): shown, enabled, there.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ImageElement<TScope> : ViewBase<TScope>
    where TScope : IStrideScope<TScope>
{
    public ImageElement(IStrideScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ImageElement(IStrideScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
