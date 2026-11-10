using Bruinet.Html.Controls.Collection;

namespace Bruinet.Html.UITests.PageObjects;

public sealed class DataTablePage : PageObjectBase<DataTablePage>
{
    public DataTablePage(IHtmlTestContext context)
        : base(context)
    {
    }

    public TableControl<DataTablePage> DataGrid => new(this, "[data-automation-id='DataTable']");
}
