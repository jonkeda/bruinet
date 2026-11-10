using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Web.UITests.Pages;

/// <summary>One todo. Test ids as in TodoDetailPage.xaml, member names as on the MAUI page object.</summary>
public sealed class TodoDetailPage : BlazorPageObjectBase<TodoDetailPage>
{
    public TodoDetailPage(IHtmlTestContext context)
        : base(context, "/detail")
    {
    }

    /// <summary>The detail page, once it is open, interactive and showing its todo.</summary>
    public static TodoDetailPage Arrived(IHtmlTestContext context)
    {
        var page = new TodoDetailPage(context);
        page.AssertIdle();
        page.Title.AssertTextEmpty(false);
        return page;
    }

    public override bool IsLoaded() => base.IsLoaded() && EditButton.Exists();

    public LabelControl<TodoDetailPage> Title => new(this, Locator.ByDataTestId("TodoDetail_Title"));

    [UatName("Status")]
    public TodoStatus<TodoDetailPage> Status => new(this, "TodoDetail_Status");

    public SectionCard<TodoDetailPage> InfoCard => new(this, "TodoDetail_Info");

    public SectionCard<TodoDetailPage> NotesCard => new(this, "TodoDetail_Notes");

    public LabelControl<SectionCard<TodoDetailPage>> Due => InfoCard.Value("DueValue");

    public LabelControl<SectionCard<TodoDetailPage>> Created => InfoCard.Value("CreatedValue");

    public LabelControl<SectionCard<TodoDetailPage>> Updated => InfoCard.Value("UpdatedValue");

    public LabelControl<SectionCard<TodoDetailPage>> Notes => NotesCard.Value("NotesValue");

    /// <summary>Back to the list: the MAUI navigation bar's back button.</summary>
    [UatNavigatesTo(typeof(TodoListPage))]
    [UatName("Back")]
    public ButtonControl<TodoDetailPage> BackButton => new(this, Locator.ByDataTestId("TodoDetail_Back"));

    public SectionCard<TodoDetailPage> SyncCard => new(this, "TodoDetail_Sync");

    public LabelControl<SectionCard<TodoDetailPage>> SyncState => SyncCard.Value("SyncValue");

    [UatNavigatesTo(typeof(TodoEditPage))]
    public ButtonControl<TodoDetailPage> EditButton => new(this, Locator.ByDataTestId("TodoDetail_Edit"));

    public ButtonControl<TodoDetailPage> DeleteButton => new(this, Locator.ByDataTestId("TodoDetail_Delete"));

    public TodoDialog<TodoDetailPage> Dialog => new(this);

    /// <summary>The dialog's title, found on the page: for "is a dialog open".</summary>
    public LabelControl<TodoDetailPage> DialogTitle => new(this, Locator.ByDataTestId("Dialog_Title"));

    public TodoEditPage Edit()
    {
        EditButton.Click();
        return TodoEditPage.Arrived(Context);
    }

    /// <summary>Deletes, answering the confirmation, and returns to the list.</summary>
    public TodoListPage Delete()
    {
        DeleteButton.Click();
        Dialog.Accept.Click();
        var list = new TodoListPage(Context);
        list.AssertIdle();
        return list;
    }

    /// <summary>The browser's back button, to the list.</summary>
    public TodoListPage Back()
    {
        BackButton.Click();
        var list = new TodoListPage(Context);
        list.AssertIdle();
        return list;
    }
}
