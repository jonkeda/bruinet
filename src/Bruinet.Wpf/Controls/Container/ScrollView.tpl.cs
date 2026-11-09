using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Container;

/// <summary>
/// A scroll viewer (<c>ScrollViewer</c>), scrolled through its Scroll pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class ScrollView<TScope> : ScrollableControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public ScrollView(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public ScrollView(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
