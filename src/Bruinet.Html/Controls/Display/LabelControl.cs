using Bruinet.Core.Interfaces;
using Bruinet.Core.Locators;
using Bruinet.Html.Interfaces;

namespace Bruinet.Html.Controls.Display;

/// <summary>
/// HTML label/text display control. Wraps any element used for text display
/// (&lt;label&gt;, &lt;span&gt;, &lt;p&gt;, &lt;h1-h6&gt;, etc.). Its text members come from
/// <see cref="ViewBase{TScope}"/>.
/// </summary>
public class LabelControl<TScope> : ViewBase<TScope>, ITextControlObject<TScope>
    where TScope : IHtmlScope<TScope>
{
    public LabelControl(IHtmlScope<TScope> scope, Locator locator)
        : base(scope, locator)
    {
    }

    public LabelControl(IHtmlScope<TScope> scope, string selectorOrId)
        : base(scope, selectorOrId)
    {
    }
}
