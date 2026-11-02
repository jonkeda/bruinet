using Bruinet.Maui.UITests.Containers;

namespace Bruinet.Maui.UITests.Pages;

/// <summary>Page object for the paged list sample page.</summary>
public class PagedListTestPage : PageObjectBase<PagedListTestPage>
{
    public PagedListTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "PagedListPage";

    /// <summary>"n of 60": how many rows the app has loaded.</summary>
    public Label<PagedListTestPage> Status => new(this, "PagedListStatus");

    public Entry<PagedListTestPage> Search => new(this, "PagedListSearch");

    public Button<PagedListTestPage> ClearSearch => new(this, "PagedListClearSearch");

    public PagedRows Rows => new(this);
}
