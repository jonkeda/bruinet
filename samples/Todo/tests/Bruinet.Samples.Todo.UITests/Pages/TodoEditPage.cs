using Bruinet.Bdd;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.UITests.Pages;

/// <summary>
/// Page object for the edit page, for a new todo or an existing one.
/// </summary>
/// <remarks>
/// Save and Cancel are toolbar items. Leaving by Shell's back arrow runs Cancel too, so it asks about
/// unsaved changes the same way.
/// </remarks>
public class TodoEditPage(IMauiTestContext context) : PageObjectBase<TodoEditPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "TodoEditPage";

    /// <summary>Validates, saves and returns.</summary>
    public ToolbarButton<AppRoot> SaveButton => new(_appRoot, Locator.ByAccessibilityId("TodoEdit_Save"));

    /// <summary>Returns, asking first when there are unsaved changes.</summary>
    public ToolbarButton<AppRoot> CancelButton => new(_appRoot, Locator.ByAccessibilityId("TodoEdit_Cancel"));

    /// <summary>
    /// The page title the user reads: "New todo" or "Edit todo".
    /// </summary>
    /// <remarks>
    /// Shell draws it into the window's chrome, so it is on <see cref="AppRoot"/>. Windows: the
    /// navigation header's text, AutomationId <c>title</c> (probed 2026-09-18). The page root's own
    /// name is its AutomationId, which is why the base class's <c>AssertTitle</c> does not read this.
    /// </remarks>
    public Label<AppRoot> Heading => new(_appRoot, "title");

    /// <summary>The native "discard changes?" confirmation.</summary>
    /// <remarks>
    /// On <see cref="AppRoot"/>, not on the page: the dialog is window chrome, and on Android the page
    /// is not in the automation tree while an alert covers it, so a page-scoped dialog failed the
    /// page's readiness gate (MissingRoot) and was never found.
    /// </remarks>
    public ContentDialog<AppRoot> Dialog => new(_appRoot);

    /// <summary>The Basics card: title and notes.</summary>
    public SectionCard<TodoEditPage> BasicsCard => new(this, "TodoEdit_Basics");

    /// <summary>The Schedule card: the due date.</summary>
    public SectionCard<TodoEditPage> ScheduleCard => new(this, "TodoEdit_Schedule");

    /// <summary>The title.</summary>
    public Entry<SectionCard<TodoEditPage>> Title => BasicsCard.Entry("TodoEdit_Title");

    /// <summary>"Title is required", shown after a Save attempt with no title.</summary>
    public Label<SectionCard<TodoEditPage>> TitleError => BasicsCard.Label("TodoEdit_TitleError");

    /// <summary>The notes.</summary>
    [BddControl("Notes")]
    public Editor<SectionCard<TodoEditPage>> Notes => new(BasicsCard, "TodoEdit_Notes");

    /// <summary>Whether the todo has a due date.</summary>
    public Switch<SectionCard<TodoEditPage>> HasDueDate => new(ScheduleCard, "TodoEdit_HasDue");

    /// <summary>The due date, enabled while <see cref="HasDueDate"/> is on.</summary>
    public DatePicker<SectionCard<TodoEditPage>> DueDate => new(ScheduleCard, "TodoEdit_Due");

    /// <summary>The status, set with Next.</summary>
    [BddControl("Status")]
    public TodoStatus<TodoEditPage> Status => new(this, "TodoEdit_Status");

    /// <summary>Replaces the title.</summary>
    public TodoEditPage EnterTitle(string title)
    {
        Title.SetText(title);
        return this;
    }

    /// <summary>Saves a new todo and returns to the list.</summary>
    public TodoListPage SaveNew()
    {
        SaveButton.Click();
        return TodoListPage.Arrived(Context);
    }

    /// <summary>Saves an edited todo and returns to its detail page.</summary>
    public TodoDetailPage SaveEdit()
    {
        SaveButton.Click();
        return TodoDetailPage.Arrived(Context);
    }

    /// <summary>Presses Save on a form that should be refused, and stays.</summary>
    public TodoEditPage SaveExpectingError()
    {
        SaveButton.Click();
        TitleError.AssertVisible(true, timeoutMs: TestConstants.PageTimeoutMs);
        return this;
    }

    /// <summary>Cancels an unchanged new todo and returns to the list.</summary>
    public TodoListPage CancelNew()
    {
        CancelButton.Click();
        return TodoListPage.Arrived(Context);
    }

    /// <summary>Cancels an unchanged edit and returns to the detail page.</summary>
    public TodoDetailPage CancelEdit()
    {
        CancelButton.Click();
        return TodoDetailPage.Arrived(Context);
    }

    /// <summary>The edit page, once it is on screen.</summary>
    public static TodoEditPage Arrived(IMauiTestContext context)
    {
        var page = new TodoEditPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }

}
