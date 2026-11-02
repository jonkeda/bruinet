using Bruinet.Maui.Containers;
using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Containers;

/// <summary>
/// The paged list's rows: its next page comes when the list nears its end, so a search for a row
/// on a later page scrolls until the app has loaded it.
/// </summary>
public class PagedRows : CollectionObjectBase<PagedListTestPage, PagedRows, PagedRow>
{
    public PagedRows(IMauiScope<PagedListTestPage> parentScope)
        : base(parentScope,
               "PagedListView",
               ItemStrategy.ByAutomationId("PagedRow"),
               (collection, itemRoot, index) => new PagedRow(collection, itemRoot, index))
    {
    }

    /// <summary>The row showing <paramref name="text"/>, loading pages until it is there.</summary>
    public PagedRow Row(string text, int? timeoutMs = null) => ItemWhere(row => row.Text.GetText() == text, timeoutMs);
}
