using Bruinet.Core.Locators;
using Bruinet.Wpf.Interfaces;

namespace Bruinet.Wpf.Controls.Container;

/// <summary>
/// An expander, expanded and collapsed through its ExpandCollapse pattern.
/// </summary>
/// <typeparam name="TScope">The containing scope type for fluent chaining.</typeparam>
public partial class Expander<TScope> : ExpandableControlBase<TScope>
    where TScope : IWpfScope<TScope>
{
    public Expander(IWpfScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public Expander(IWpfScope<TScope> scope, string automationId)
        : base(scope, automationId)
    {
    }
}
