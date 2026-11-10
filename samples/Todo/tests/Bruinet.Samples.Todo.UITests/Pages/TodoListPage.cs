using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.UITests.Pages;

/// <summary>
/// Page object for the Todo list, the Shell root.
/// </summary>
/// <remarks>
/// Sync and Add are toolbar items: they belong to the window's chrome, so they are declared on
/// <see cref="AppRoot"/> and clicked through the page's <c>InvokeToolbarItem</c> verb.
/// </remarks>
public class TodoListPage(IMauiTestContext context) : PageObjectBase<TodoListPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "TodoListPage";

    /// <summary>Syncs with the server.</summary>
    public ToolbarButton<AppRoot> SyncButton => new(_appRoot, Locator.ByAccessibilityId("TodoList_Sync"));

    /// <summary>Opens a new, empty edit page.</summary>
    [UatNavigatesTo(typeof(TodoEditPage))]
    public ToolbarButton<AppRoot> AddButton => new(_appRoot, Locator.ByAccessibilityId("TodoList_Add"));

    /// <summary>Opens the users (TOD.11).</summary>
    [UatNavigatesTo(typeof(UserListPage))]
    public ToolbarButton<AppRoot> UsersButton => new(_appRoot, Locator.ByAccessibilityId("TodoList_Users"));

    /// <summary>The status filter: All, Open, In progress, Done.</summary>
    public Picker<TodoListPage> Filter => new(this, "TodoList_Filter");

    /// <summary>"Synced 09:00", "Offline", "Sync failed", ...</summary>
    public Label<TodoListPage> LastSync => new(this, "TodoList_LastSync");

    /// <summary>Loading, Empty, Error, or the rows.</summary>
    [UatName("List")]
    public TodoListState State => new(this, "TodoList_State");

    /// <summary>The rows.</summary>
    public TodoList Todos => new(this, "TodoList_Items");

    /// <summary>"Nothing to do", in the Empty state.</summary>
    [UatName("Empty Message")]
    public Label<TodoListState> EmptyMessage => State.EmptyLabel;

    /// <summary>What went wrong, in the Error state.</summary>
    [UatName("Error Message")]
    public Label<TodoListState> ErrorMessage => State.ErrorLabel;

    /// <summary>Retries the sync, in the Error state.</summary>
    public Button<TodoListState> RetryButton => State.Retry;

    /// <summary>Opens a new, empty edit page.</summary>
    public TodoEditPage Add()
    {
        AddButton.Click();
        return TodoEditPage.Arrived(Context);
    }

    /// <summary>Creates a todo with this title through the edit page, and returns to the list.</summary>
    [BddWhen("I create a todo {title}")]
    public TodoListPage CreateTodo(string title) => Add().EnterTitle(title).SaveNew();

    /// <summary>Starts a sync.</summary>
    /// <remarks>
    /// Does not wait for it to finish: the sync line cannot tell two syncs in the same minute apart
    /// ("Synced 09:00" both times). The caller asserts the outcome it expects instead - a pending
    /// marker gone, a row arrived, the Error state - and that assert waits.
    /// </remarks>
    public TodoListPage Sync()
    {
        SyncButton.Click();
        return this;
    }

    /// <summary>Opens the users.</summary>
    public UserListPage OpenUsers()
    {
        UsersButton.Click();
        return UserListPage.Arrived(Context);
    }

    /// <summary>Opens the detail page of the row with this title.</summary>
    public TodoDetailPage Open(string title) => Todos.Open(title, TestConstants.PageTimeoutMs);

    /// <summary>The list page, once it is on screen.</summary>
    public static TodoListPage Arrived(IMauiTestContext context)
    {
        var page = new TodoListPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
