using Bruinet.Core.Scopes;
using Bruinet.Html.Scopes;

namespace Bruinet.Html.UITests.PageObjects;

/// <summary>
/// Any page of the Blazor sample, as far as "is the app interactive" goes: the sample's layout
/// sets <c>data-busy</c> on its first interactive render, and this page requires it.
/// </summary>
public sealed class SampleShell : PageObjectBase<SampleShell>
{
    public SampleShell(IHtmlTestContext context)
        : base(context)
    {
    }

    protected override BusySignal BusySignal => BusySignal.Required;
}
