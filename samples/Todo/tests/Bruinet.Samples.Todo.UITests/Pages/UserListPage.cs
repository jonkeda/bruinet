using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.UITests.Pages;

/// <summary>
/// Page object for the users (TOD.11), opened from the list's Users toolbar item.
/// </summary>
/// <remarks>
/// In the <c>Users</c> namespace, as its edit page is: a scenario names these pages after
/// <c>Using | Users</c> (AD-019).
/// </remarks>
[BddNamespace("Users")]
public class UserListPage(IMauiTestContext context) : PageObjectBase<UserListPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "UserListPage";

    /// <summary>Opens a new, empty user. Disabled offline.</summary>
    [UatNavigatesTo(typeof(UserEditPage))]
    public ToolbarButton<AppRoot> AddButton => new(_appRoot, Locator.ByAccessibilityId("UserList_Add"));

    /// <summary>"You are offline. Users can be viewed, but not changed."; not shown online.</summary>
    [UatName("Offline Notice")]
    public ShownLabel<UserListPage> OfflineNotice => new(this, "UserList_Offline");

    /// <summary>The rows: available users first, then the rest, each by name.</summary>
    public UserList Users => new(this, "UserList_Users");

    /// <summary>Shell's back arrow, to the todo list.</summary>
    /// <remarks>As on <see cref="TodoDetailPage.BackButton"/>.</remarks>
    [UatNavigatesTo(typeof(TodoListPage))]
    public Button<AppRoot> BackButton => Context.Platform == Bruinet.Maui.Enums.MauiPlatform.Android
        ? new(_appRoot, Locator.ByAccessibilityId("Navigate up"))
        : new(_appRoot, "NavigationViewBackButton");

    /// <summary>Opens a new, empty user.</summary>
    public UserEditPage Add()
    {
        AddButton.Click();
        return UserEditPage.Arrived(Context);
    }

    /// <summary>Adds a user with this name and every other field as it starts, and returns to the list.</summary>
    [BddWhen("I add a user {name}")]
    public UserListPage AddUser(string name) => Add().EnterName(name).SaveNew();

    /// <summary>Opens the user with this name.</summary>
    public UserEditPage Open(string name) => Users.Open(name, TestConstants.PageTimeoutMs);

    /// <summary>Returns to the todo list.</summary>
    public TodoListPage Back()
    {
        BackButton.Click();
        return TodoListPage.Arrived(Context);
    }

    /// <summary>The users page, once it is on screen.</summary>
    public static UserListPage Arrived(IMauiTestContext context)
    {
        var page = new UserListPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }
}
