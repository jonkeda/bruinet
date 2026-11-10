using Bruinet.Core.Collections;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Testing;
using Bruinet.Html.Collections;
using Bruinet.Html.Controls.Buttons;
using Bruinet.Html.Controls.Display;
using Bruinet.Html.Controls.Selection;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Web.UITests.Pages;

/// <summary>
/// The list page. Test ids as in the MAUI app (TodoListPage.xaml), and member names as on the MAUI
/// page object, so one scenario file reads the same on both heads.
/// </summary>
public sealed class TodoListPage : BlazorPageObjectBase<TodoListPage>
{
    public TodoListPage(IHtmlTestContext context)
        : base(context, "/")
    {
    }

    public override bool IsLoaded() => base.IsLoaded() && AddButton.IsExists();

    protected override bool CompareUrl() => new Uri(Context.CurrentUrl).LocalPath is "/" or "/todos";

    public ButtonControl<TodoListPage> SyncButton => new(this, Locator.ByDataTestId("TodoList_Sync"));

    [UatNavigatesTo(typeof(TodoEditPage))]
    public ButtonControl<TodoListPage> AddButton => new(this, Locator.ByDataTestId("TodoList_Add"));

    public SelectControl<TodoListPage> Filter => new(this, Locator.ByDataTestId("TodoList_Filter"));

    public LabelControl<TodoListPage> LastSync => new(this, Locator.ByDataTestId("TodoList_LastSync"));

    /// <summary>Loading, Empty, Error or the rows: exactly one shows.</summary>
    [UatName("List")]
    public TodoListState State => new(this);

    [UatName("Empty Message")]
    public LabelControl<TodoListPage> EmptyMessage => new(this, Locator.ByDataTestId("TodoList_EmptyLabel"));

    [UatName("Error Message")]
    public LabelControl<TodoListPage> ErrorMessage => new(this, Locator.ByDataTestId("TodoList_ErrorLabel"));

    public ButtonControl<TodoListPage> RetryButton => new(this, Locator.ByDataTestId("TodoList_Retry"));

    [UatName("Todos")]
    public TodoRows Todos => new(this);

    /// <summary>Opens a new, empty edit form.</summary>
    public TodoEditPage Add()
    {
        AddButton.Click();
        return TodoEditPage.Arrived(Context);
    }

    /// <summary>Adds a todo with <paramref name="title"/> and returns to the list.</summary>
    [BddWhen("I create a todo {title}")]
    public TodoListPage CreateTodo(string title) => Add().EnterTitle(title).SaveNew();

    /// <summary>Syncs, and waits until the sync has finished.</summary>
    public TodoListPage Sync()
    {
        SyncButton.Click();
        AssertIdle();
        return this;
    }

    /// <summary>Opens the todo titled <paramref name="title"/>.</summary>
    public TodoDetailPage Open(string title) => Todos.Open(title);

    /// <summary>The rows, one per todo shown. Each row follows its todo by key.</summary>
    public sealed class TodoRows(IHtmlScope<TodoListPage> page)
        : CollectionObjectBase<TodoListPage, TodoRows, TodoRow>(
            page, Locator.ByDataTestId("TodoList_Items"), Locator.ByDataTestId("TodoRow"))
    {
        public override TodoRows Self => this;

        protected override TodoRow CreateItem(ItemKey key, int position) => new(this, key, position);

        /// <summary>The row titled <paramref name="title"/>, waiting for it.</summary>
        public TodoRow Row(string title, int? timeoutMs = null)
            => ItemWhere(row => row.Title.TryGetText() == title, timeoutMs);

        /// <summary>Waits until a row titled <paramref name="title"/> is shown, or is not.</summary>
        [BddThen("{control} should have todo {value}")]
        public TodoRows AssertRow(string title, bool present = true, int? timeoutMs = null)
        {
            if (present)
            {
                Row(title, timeoutMs);
            }
            else if (!WaitNoItemWhere(row => row.Title.TryGetText() == title, timeoutMs))
            {
                throw new AssertionException($"Expected no row titled '{title}' in the list.");
            }

            return this;
        }

        /// <summary>Waits until no row titled <paramref name="title"/> is shown.</summary>
        [BddThen("{control} should not have todo {value}")]
        public TodoRows AssertNoRow(string title, int? timeoutMs = null) => AssertRow(title, present: false, timeoutMs);

        /// <summary>Waits until the row with <paramref name="title"/> shows the pending marker, or does not.</summary>
        [BddThen("the todo {item} should be waiting to sync", HasLiteral = true, Literal = true)]
        [BddThen("the todo {item} should be synced", HasLiteral = true, Literal = false)]
        public TodoRows AssertPending(string title, bool expected = true, int? timeoutMs = null)
        {
            timeoutMs ??= StepTimeoutMs;
            Row(title, timeoutMs).Pending.AssertExists(expected, timeoutMs: timeoutMs);
            return this;
        }

        /// <summary>Opens the todo titled <paramref name="title"/>: the MAUI step.</summary>
        [BddWhen("I open the todo {item}")]
        public TodoDetailPage Open(string title)
        {
            Row(title, StepTimeoutMs).Open.Click();
            return TodoDetailPage.Arrived(Context);
        }

        /// <summary>Asserts a row is shown for each title, waiting for them: the MAUI step.</summary>
        [BddThen("the list should show {value}")]
        public TodoRows AssertRows(string[] titles)
        {
            foreach (var title in titles)
            {
                Row(title, StepTimeoutMs);
            }

            return this;
        }

        /// <summary>Asserts the row shows this status name: the MAUI step.</summary>
        [BddThen("the todo {item} should show status {value}")]
        public TodoRows AssertRowStatus(string title, string status)
        {
            Row(title, StepTimeoutMs).Status.AssertStatus(status);
            return this;
        }

        /// <summary>Asserts the row's status has no Next button: the MAUI step.</summary>
        [BddThen("the todo {item} should have a read-only status")]
        public TodoRows AssertRowStatusReadOnly(string title)
        {
            Row(title, StepTimeoutMs).Status.AssertChangeable(false);
            return this;
        }

        /// <summary>Asserts the row shows "Due …", or with <c>false</c> no due date: the MAUI step.</summary>
        [BddThen("the todo {item} should show a due date", HasLiteral = true, Literal = true)]
        [BddThen("the todo {item} should show no due date", HasLiteral = true, Literal = false)]
        public TodoRows AssertRowDue(string title, bool shown)
        {
            var due = Row(title, StepTimeoutMs).Due;
            if (shown)
            {
                due.AssertTextStartsWith("Due ");
            }
            else
            {
                due.AssertExists(false);
            }

            return this;
        }

        // The waits a scenario step gets: the MAUI suite's TestConstants.PageTimeoutMs.
        private const int StepTimeoutMs = 10_000;
    }

    /// <summary>One row: title, due date, read-only status, pending marker.</summary>
    public sealed class TodoRow(IHtmlCollection rows, ItemKey key, int position) : ItemObjectBase<TodoRow>(rows, key, position)
    {
        public override TodoRow Self => this;

        public LabelControl<TodoRow> Title => new(this, Locator.ByDataTestId("TodoRow_Title"));

        public LabelControl<TodoRow> Due => new(this, Locator.ByDataTestId("TodoRow_Due"));

        public TodoStatus<TodoRow> Status => new(this, "TodoRow_Status");

        public LabelControl<TodoRow> Pending => new(this, Locator.ByDataTestId("TodoRow_Pending"));

        /// <summary>The row's title, which opens the todo when clicked.</summary>
        public ButtonControl<TodoRow> Open => new(this, Locator.ByDataTestId("TodoRow_Title"));
    }
}
