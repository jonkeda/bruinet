using Bruinet.Maui.Containers;

namespace Bruinet.Maui.UITests.Containers;

/// <summary>One row of the paged list.</summary>
public class PagedRow : ItemObjectBase<PagedRows, PagedRow>
{
    public PagedRow(PagedRows collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The row's text, "Item n".</summary>
    public Label<PagedRow> Text => new(this, "PagedRowText");
}
