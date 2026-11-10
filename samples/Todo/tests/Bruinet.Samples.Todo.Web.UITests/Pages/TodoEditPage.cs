using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.DateTime;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Text;
using Bruinet.Html.Controls.Toggle;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Web.UITests.Pages;

/// <summary>The edit page, for a new todo or an existing one. Test ids as in TodoEditPage.xaml.</summary>
public sealed class TodoEditPage : BlazorPageObjectBase<TodoEditPage>
{
    public TodoEditPage(IHtmlTestContext context)
        : base(context, "/edit")
    {
    }

    /// <summary>The edit page, once it is open and interactive.</summary>
    public static TodoEditPage Arrived(IHtmlTestContext context)
    {
        var page = new TodoEditPage(context);
        page.AssertIdle();
        return page;
    }

    public override bool IsLoaded() => base.IsLoaded() && Save.Exists();

    public LabelControl<TodoEditPage> Heading => new(this, Locator.ByCss("h1"));

    public TextInputControl<TodoEditPage> Title => new(this, Locator.ByDataTestId("TodoEdit_Title"));

    public LabelControl<TodoEditPage> TitleError => new(this, Locator.ByDataTestId("TodoEdit_TitleError"));

    public TextAreaControl<TodoEditPage> Notes => new(this, Locator.ByDataTestId("TodoEdit_Notes"));

    public CheckBoxControl<TodoEditPage> HasDueDate => new(this, Locator.ByDataTestId("TodoEdit_HasDue"));

    public DateInputControl<TodoEditPage> DueDate => new(this, Locator.ByDataTestId("TodoEdit_Due"));

    [UatName("Status")]
    public TodoStatus<TodoEditPage> Status => new(this, "TodoEdit_Status");

    public ButtonControl<TodoEditPage> Save => new(this, Locator.ByDataTestId("TodoEdit_Save"));

    public ButtonControl<TodoEditPage> CancelButton => new(this, Locator.ByDataTestId("TodoEdit_Cancel"));

    public TodoDialog<TodoEditPage> Dialog => new(this);

    /// <summary>The dialog's title, found on the page: for "is a dialog open", which the dialog itself cannot answer when it is not.</summary>
    public LabelControl<TodoEditPage> DialogTitle => new(this, Locator.ByDataTestId("Dialog_Title"));

    public TodoEditPage EnterTitle(string title)
    {
        Title.SetText(title);
        return this;
    }

    /// <summary>Saves a new todo and returns to the list.</summary>
    public TodoListPage SaveNew()
    {
        Save.Click();
        var list = new TodoListPage(Context);
        list.AssertIdle();
        return list;
    }

    /// <summary>Saves an edit and returns to the detail page.</summary>
    public TodoDetailPage SaveEdit()
    {
        Save.Click();
        return TodoDetailPage.Arrived(Context);
    }

    /// <summary>Clicks Save and stays, with the title error shown.</summary>
    public TodoEditPage SaveExpectingError()
    {
        Save.Click();
        TitleError.AssertExists(true);
        return this;
    }

    /// <summary>Cancels an unchanged new todo and returns to the list.</summary>
    public TodoListPage CancelNew()
    {
        CancelButton.Click();
        var list = new TodoListPage(Context);
        list.AssertIdle();
        return list;
    }
}
