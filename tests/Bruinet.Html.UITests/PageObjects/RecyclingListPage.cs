using Bruinet.Core.Collections;
using Bruinet.Html.Collections;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;

namespace Bruinet.Html.UITests.PageObjects;

/// <summary>The sample's /recycling-list page: a virtualized list whose row nodes are reused for other items.</summary>
public sealed class RecyclingListPage : HtmlPageObjectBase<RecyclingListPage>
{
    public RecyclingListPage(IHtmlTestContext context)
        : base(context)
    {
    }

    public LabelControl<RecyclingListPage> Status => new(this, "[data-testid='recycling-status']");

    public ButtonControl<RecyclingListPage> Recycle => new(this, "[data-testid='recycle-btn']");

    public TodoRows Rows => new(this);

    /// <summary>The rows, identified by their <c>data-item-key</c>.</summary>
    public sealed class TodoRows(IHtmlScope<RecyclingListPage> page)
        : CollectionObjectBase<RecyclingListPage, TodoRows, TodoRow>(
            page, Locator.ByCss("[data-testid='recycling-list']"), Locator.ByCss("[data-testid='todo-row']"))
    {
        public override TodoRows Self => this;

        protected override TodoRow CreateItem(ItemKey key, int position) => new(this, key, position);
    }

    /// <summary>One row: its title and its Delete button.</summary>
    public sealed class TodoRow(IHtmlCollection rows, ItemKey key, int position) : ItemObjectBase<TodoRow>(rows, key, position)
    {
        public override TodoRow Self => this;

        public LabelControl<TodoRow> Title => new(this, "[data-testid='title']");

        public ButtonControl<TodoRow> Delete => new(this, "[data-testid='delete-btn']");
    }
}
