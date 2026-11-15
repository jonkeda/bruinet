using Bruinet.Bdd;
using Bruinet.Samples.Todo.UITests.Containers;
using Bruinet.Samples.Todo.UITests.Controls;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.UITests.Pages;

/// <summary>
/// Page object for one todo's detail page.
/// </summary>
/// <remarks>
/// The three cards each scope their own labels; the labels are named here, on the page, so a test
/// reads <c>Due</c> rather than knowing which card holds it.
/// </remarks>
public class TodoDetailPage(IMauiTestContext context) : PageObjectBase<TodoDetailPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    /// <inheritdoc />
    public override string Name => "TodoDetailPage";

    /// <summary>Opens the edit page for this todo.</summary>
    [BddNavigatesTo(typeof(TodoEditPage))]
    public ToolbarButton<AppRoot> EditButton => new(_appRoot, Locator.ByAccessibilityId("TodoDetail_Edit"));

    /// <summary>Asks, then deletes this todo.</summary>
    public ToolbarButton<AppRoot> DeleteButton => new(_appRoot, Locator.ByAccessibilityId("TodoDetail_Delete"));

    /// <summary>
    /// Shell's back arrow.
    /// </summary>
    /// <remarks>
    /// Windows: WinUI's <c>NavigationViewBackButton</c>, which answers Invoke (probed 2026-09-18).
    /// Android: the toolbar's "up" button, whose content description is "Navigate up".
    /// </remarks>
    [BddNavigatesTo(typeof(TodoListPage))]
    public Button<AppRoot> BackButton => Context.Platform == Bruinet.Maui.Enums.MauiPlatform.Android
        ? new(_appRoot, Locator.ByAccessibilityId("Navigate up"))
        : new(_appRoot, "NavigationViewBackButton");

    /// <summary>The native delete confirmation.</summary>
    /// <remarks>
    /// On <see cref="AppRoot"/>, not on the page: the dialog is window chrome, and on Android the page
    /// is not in the automation tree while an alert covers it, so a page-scoped dialog failed the
    /// page's readiness gate (MissingRoot) and was never found.
    /// </remarks>
    public ContentDialog<AppRoot> Dialog => new(_appRoot);

    /// <summary>The title.</summary>
    public Label<TodoDetailPage> Title => new(this, "TodoDetail_Title");

    /// <summary>The status, changeable here with Next.</summary>
    [BddControl("Status")]
    public TodoStatus<TodoDetailPage> Status => new(this, "TodoDetail_Status");

    /// <summary>The Details card: due, created, updated.</summary>
    public SectionCard<TodoDetailPage> InfoCard => new(this, "TodoDetail_Info");

    /// <summary>The Notes card.</summary>
    public SectionCard<TodoDetailPage> NotesCard => new(this, "TodoDetail_Notes");

    /// <summary>The Sync card.</summary>
    public SectionCard<TodoDetailPage> SyncCard => new(this, "TodoDetail_Sync");

    /// <summary>"Due …", or "No due date".</summary>
    public Label<SectionCard<TodoDetailPage>> Due => InfoCard.Label("DueValue");

    /// <summary>When the todo was created.</summary>
    public Label<SectionCard<TodoDetailPage>> Created => InfoCard.Label("CreatedValue");

    /// <summary>When the todo last changed.</summary>
    public Label<SectionCard<TodoDetailPage>> Updated => InfoCard.Label("UpdatedValue");

    /// <summary>The notes.</summary>
    public Label<SectionCard<TodoDetailPage>> Notes => NotesCard.Label("NotesValue");

    /// <summary>"Synced", "Waiting to sync" or "Local only".</summary>
    public Label<SectionCard<TodoDetailPage>> SyncState => SyncCard.Label("SyncValue");

    /// <summary>Opens the edit page for this todo.</summary>
    public TodoEditPage Edit()
    {
        EditButton.Click();
        return TodoEditPage.Arrived(Context);
    }

    /// <summary>The detail page, once it is on screen.</summary>
    public static TodoDetailPage Arrived(IMauiTestContext context)
    {
        var page = new TodoDetailPage(context);
        page.AssertLoaded(true, timeoutMs: TestConstants.PageTimeoutMs);
        return page;
    }

    /// <summary>Returns to the list.</summary>
    public TodoListPage Back()
    {
        BackButton.Click();
        return TodoListPage.Arrived(Context);
    }
}
