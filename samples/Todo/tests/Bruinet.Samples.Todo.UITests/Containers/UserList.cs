using Bruinet.Bdd;
using Bruinet.Core.Scopes;
using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Samples.Todo.UITests.Pages;

namespace Bruinet.Samples.Todo.UITests.Containers;

/// <summary>
/// The users page's rows (<c>UserList_Users</c>, a CollectionView), found by the repeating
/// <c>UserRow</c> id and opened by selecting their list item, as the todo list's are.
/// </summary>
public sealed class UserList : CollectionObjectBase<UserListPage, UserList, UserRow>
{
    // A row gone mid-read costs this, not a full default wait.
    private const int VanishedRowReadMs = 250;

    /// <summary>Creates the collection within the users page.</summary>
    /// <param name="parentScope">The users page.</param>
    /// <param name="automationId">The CollectionView's AutomationId.</param>
    public UserList(IMauiScope<UserListPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("UserRow"),
               (collection, itemRoot, index) => new UserRow(collection, itemRoot, index))
    {
    }

    /// <summary>The row with this name. Throws when there is none.</summary>
    public UserRow Row(string name) => ItemWhere(row => TryReadName(row) == name);

    /// <summary>The names of the rows, in the order shown.</summary>
    public IReadOnlyList<string> Names() => [.. Items.Select(row => TryReadName(row) ?? string.Empty)];

    /// <summary>Waits until a row with this name is shown (or, with <c>false</c>, is gone).</summary>
    public bool WaitRow(string name, bool present = true, int? timeoutMs = null)
        => RunWait(() => (FindItem(row => TryReadName(row) == name) is not null) == present, timeoutMs);

    /// <summary>Asserts a row with this name is shown (or, with <c>false</c>, is not).</summary>
    [BddThen("{control} should have user {value}")]
    public UserList AssertRow(string name, bool present = true, int? timeoutMs = null)
    {
        if (!WaitRow(name, present, timeoutMs ?? TestConstants.PageTimeoutMs))
        {
            throw new AssertionException(
                $"Expected {(present ? "a" : "no")} user '{name}' in '{Locator.Value}'. "
                + $"Users shown: {string.Join(", ", Names().Select(shown => $"'{shown}'"))}.");
        }

        return Self;
    }

    /// <summary>
    /// Asserts these users are shown in this order. Other users may be shown between them: tests
    /// add users of their own.
    /// </summary>
    [BddThen("the users should be listed as {value}")]
    public UserList AssertOrder(string[] names)
    {
        AssertRow(names[^1]);
        var shown = Names();
        var positions = names.Select(name => shown.ToList().IndexOf(name)).ToList();
        if (positions.Any(position => position < 0) || positions.Zip(positions.Skip(1)).Any(pair => pair.First > pair.Second))
        {
            throw new AssertionException(
                $"Expected the users in the order {string.Join(", ", names)}; shown: {string.Join(", ", shown)}.");
        }

        return Self;
    }

    /// <summary>Asserts the row shows this role: Member, Lead or Admin.</summary>
    [BddThen("the user {item} should show role {value}")]
    public UserList AssertRole(string name, string role)
    {
        ShownRow(name).Role.AssertText(role);
        return Self;
    }

    /// <summary>Asserts the row shows this state: Expired, On leave or Inactive.</summary>
    [BddThen("the user {item} should show state {value}")]
    public UserList AssertState(string name, string state)
    {
        ShownRow(name).State.AssertText(state);
        return Self;
    }

    /// <summary>Asserts the row shows no state: the user is available.</summary>
    [BddThen("the user {item} should show no state")]
    public UserList AssertNoState(string name)
    {
        ShownRow(name).State.AssertShown(false);
        return Self;
    }

    /// <summary>Opens a row's user, as tapping it does.</summary>
    [BddWhen("I open the user {item}")]
    public UserEditPage Open(string name, int? timeoutMs = null)
    {
        AssertRow(name, timeoutMs: timeoutMs);
        SelectItem(Row(name).Index, timeoutMs);
        return UserEditPage.Arrived(Context);
    }

    // The row with this name, once it is shown.
    private UserRow ShownRow(string name) => AssertRow(name).Row(name);

    // The list reloads when the page appears, so a row listed a moment ago can be gone before its
    // name is read. Such a row is not the one asked for: null, and the caller's wait looks again.
    private static string? TryReadName(UserRow row)
    {
        try
        {
            return row.UserName.GetText(VanishedRowReadMs);
        }
        catch (ScopeNotReadyException vanished) when (vanished.Readiness.State == ScopeReadinessState.MissingRoot)
        {
            return null;
        }
    }
}

/// <summary>One user row, scoped to its own item root.</summary>
public sealed class UserRow : ItemObjectBase<UserList, UserRow>
{
    /// <summary>Creates a row from the root the collection found.</summary>
    public UserRow(UserList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The name.</summary>
    public Label<UserRow> UserName => new(this, "UserRow_Name");

    /// <summary>"Member", "Lead" or "Admin".</summary>
    public Label<UserRow> Role => new(this, "UserRow_Role");

    /// <summary>"Expired", "On leave" or "Inactive"; not shown for an available user.</summary>
    public ShownLabel<UserRow> State => new(this, "UserRow_State");
}
