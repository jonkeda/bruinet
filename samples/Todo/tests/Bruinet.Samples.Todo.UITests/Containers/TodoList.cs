using Bruinet.Bdd;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Scopes;
using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Samples.Todo.UITests.Pages;

namespace Bruinet.Samples.Todo.UITests.Containers;

/// <summary>
/// The list page's rows (<c>TodoList_Items</c>, a CollectionView).
/// </summary>
/// <remarks>
/// <para>
/// Rows are found by the repeating <c>TodoRow</c> id, and opened by selecting their containing
/// list item, which is what tapping a row does (probed 2026-09-18: <c>SelectionItemPattern.Select</c>
/// on the row's <c>ListItem</c> opened the detail page).
/// </para>
/// <para>
/// Counts here are realized rows. The scenarios have a handful of todos, so every row is realized;
/// a long list would need a count the app shows.
/// </para>
/// </remarks>
public sealed class TodoList : CollectionObjectBase<TodoListPage, TodoList, TodoRow>
{
    /// <summary>Creates the collection within the list page.</summary>
    /// <param name="parentScope">The list page.</param>
    /// <param name="automationId">The CollectionView's AutomationId.</param>
    public TodoList(IMauiScope<TodoListPage> parentScope, string automationId)
        : base(parentScope,
               automationId,
               ItemStrategy.ByAutomationId("TodoRow"),
               (collection, itemRoot, index) => new TodoRow(collection, itemRoot, index))
    {
    }

    // A row gone mid-read costs this, not a full default wait.
    private const int VanishedRowReadMs = 250;

    /// <summary>The row with this title. Throws when there is none.</summary>
    public TodoRow Row(string title) => ItemWhere(row => TryReadTitle(row) == title);

    /// <summary>The titles of the rows, in the order shown.</summary>
    public IReadOnlyList<string> Titles() => [.. Items.Select(row => TryReadTitle(row) ?? string.Empty)];

    /// <summary>Waits until a row with this title is shown (or, with <c>false</c>, is gone).</summary>
    public bool WaitRow(string title, bool present = true, int? timeoutMs = null)
        => RunWait(() => (FindItem(row => TryReadTitle(row) == title) is not null) == present, timeoutMs);

    /// <summary>Asserts a row with this title is shown (or, with <c>false</c>, is not).</summary>
    [BddThen("{control} should have todo {value}")]
    public TodoList AssertRow(string title, bool present = true, int? timeoutMs = null)
    {
        if (!WaitRow(title, present, timeoutMs))
        {
            throw new AssertionException(
                $"Expected {(present ? "a" : "no")} row titled '{title}' in '{Locator.Value}'. "
                + $"Rows shown: {string.Join(", ", Titles().Select(shown => $"'{shown}'"))}.");
        }

        return Self;
    }

    /// <summary>Asserts no row with this title is shown.</summary>
    [BddThen("{control} should not have todo {value}")]
    public TodoList AssertNoRow(string title, int? timeoutMs = null) => AssertRow(title, present: false, timeoutMs);

    /// <summary>Asserts a row is shown for each title, waiting for them.</summary>
    [BddThen("the list should show {value}")]
    public TodoList AssertRows(string[] titles, int? timeoutMs = null)
    {
        foreach (var title in titles)
        {
            AssertRow(title, timeoutMs: timeoutMs ?? TestConstants.PageTimeoutMs);
        }

        return Self;
    }

    /// <summary>Asserts the row shows this status name: Open, In progress, Done or Overdue.</summary>
    [BddThen("the todo {item} should show status {value}")]
    public TodoList AssertRowStatus(string title, string status)
    {
        ShownRow(title).Status.AssertStatus(status);
        return Self;
    }

    /// <summary>Asserts the row's status cannot be changed there: it has no Next button.</summary>
    [BddThen("the todo {item} should have a read-only status")]
    public TodoList AssertRowStatusReadOnly(string title)
    {
        ShownRow(title).Status.AssertChangeable(false);
        return Self;
    }

    /// <summary>Asserts the row shows "Due …", or with <c>false</c> no due date.</summary>
    [BddThen("the todo {item} should show a due date", HasLiteral = true, Literal = true)]
    [BddThen("the todo {item} should show no due date", HasLiteral = true, Literal = false)]
    public TodoList AssertRowDue(string title, bool shown)
    {
        var due = ShownRow(title).Due;
        if (shown)
        {
            due.AssertTextStartsWith("Due ");
        }
        else
        {
            due.AssertShown(false);
        }

        return Self;
    }

    /// <summary>
    /// Waits until the row with this title shows (or, with <c>false</c>, no longer shows) the
    /// "not synced yet" marker.
    /// </summary>
    /// <remarks>
    /// Finds the row by title on every poll rather than holding one: a sync reloads the list, and a
    /// row found before it would be the old one.
    /// </remarks>
    [BddThen("the todo {item} should be waiting to sync", HasLiteral = true, Literal = true)]
    [BddThen("the todo {item} should be synced", HasLiteral = true, Literal = false)]
    public TodoList AssertPending(string title, bool expected = true, int? timeoutMs = null)
    {
        if (!RunWait(() => FindItem(row => TryReadTitle(row) == title)?.Pending.IsShown() == expected, timeoutMs ?? TestConstants.PageTimeoutMs))
        {
            throw new AssertionException(
                $"Expected the row titled '{title}' in '{Locator.Value}' to {(expected ? "show" : "not show")} the pending marker.");
        }

        return Self;
    }

    // The row with this title, once it is shown.
    private TodoRow ShownRow(string title) => AssertRow(title, timeoutMs: TestConstants.PageTimeoutMs).Row(title);

    // A sync reloads the list, so a row listed a moment ago can be gone before its title is read.
    // Such a row is not the one asked for: null, and the caller's wait looks again.
    private static string? TryReadTitle(TodoRow row)
    {
        try
        {
            return row.Title.GetText(VanishedRowReadMs);
        }
        catch (ScopeNotReadyException vanished) when (vanished.Readiness.State == ScopeReadinessState.MissingRoot)
        {
            return null;
        }
    }

    /// <summary>Opens a row's detail page, as tapping it does.</summary>
    [BddWhen("I open the todo {item}")]
    public TodoDetailPage Open(string title, int? timeoutMs = null)
    {
        WaitRow(title, present: true, timeoutMs);
        SelectItem(Row(title).Index, timeoutMs);

        var detail = new TodoDetailPage(Context);
        detail.AssertLoaded(true, timeoutMs: timeoutMs ?? TestConstants.PageTimeoutMs);
        return detail;
    }
}

/// <summary>
/// One todo row, scoped to its own item root.
/// </summary>
/// <remarks>
/// Every id below repeats unchanged on every row. The status is the read-only form of the
/// <see cref="TodoStatus{TScope}"/> component: it has no Next button.
/// </remarks>
public sealed class TodoRow : ItemObjectBase<TodoList, TodoRow>
{
    /// <summary>Creates a row from the root the collection found.</summary>
    public TodoRow(TodoList collection, IMauiElement itemRoot, int index)
        : base(collection, itemRoot, index)
    {
    }

    /// <summary>The title.</summary>
    public Label<TodoRow> Title => new(this, "TodoRow_Title");

    /// <summary>"Due …"; not shown when the todo has no due date.</summary>
    public ShownLabel<TodoRow> Due => new(this, "TodoRow_Due");

    /// <summary>The read-only status.</summary>
    public TodoStatus<TodoRow> Status => new(this, "TodoRow_Status");

    /// <summary>The "not synced yet" marker, shown only while the row has unsynced changes.</summary>
    public TodoPendingMark<TodoRow> Pending => new(this, "TodoRow_Pending");
}
