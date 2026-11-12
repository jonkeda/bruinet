// What the UAT and test-data guides' samples use from the Todo sample (samples/Todo), reduced to what they call:
// the samples compile against Bruinet's API, not against the sample app.
using System.Text.Json;
using Bruinet.Bdd;
using Bruinet.Core.Locators;
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;
using Bruinet.Maui.Testing;
using Bruinet.Uat;

namespace Bruinet.Docs.Samples.Uat;

public interface ITodoArrangement
{
    string DatabasePath { get; }

    TodoTestSet TestSet { get; }

    void ResetBackend();
}

public sealed class TodoTestSet
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    public DateTimeOffset Now { get; init; }
}

public sealed class TodoTestSetFile
{
    public IReadOnlyList<TodoDto> Server { get; init; } = [];

    public IReadOnlyList<LocalTodoData> Local { get; init; } = [];
}

public sealed record TodoDto(int Id, string Title)
{
    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record LocalTodoData(int Id, string? Title);

public sealed record TodoItem(int Id, string Title);

public sealed record TodoRow(string Title, DateOnly? DueDate);

public sealed record ServerTodoRow(string Title, string Status, DateOnly? DueDate);

public static class TodoDbReader
{
    public static IReadOnlyList<TodoItem> Todos(string databasePath) => [];
}

public static class TodoValidation
{
    public static string? ValidateTitle(string? title) => string.IsNullOrWhiteSpace(title) ? "has no title." : null;
}

public static class TodoApi
{
    public const string TodosPath = "/todos";
}

/// <summary>The Todo app's fixture: the app launched, its test sets loaded.</summary>
public abstract class TodoAppFixture : MauiTestFixtureBase
{
    public static string DefaultTestData => "three-todos";

    public void UseTestSet(string name)
    {
    }

    public void StartTest()
    {
    }

    public void RescopeAfterRestart(BddExecutionContext context)
    {
    }

    protected override string GetDefaultAppPath(MauiPlatform platform) => "Todo.exe";
}

/// <summary>The scope of controls that live in the app's chrome, not on a page.</summary>
public sealed class AppRoot(IMauiTestContext context) : PageObjectBase<AppRoot>(context);

public sealed class TodoEditPage(IMauiTestContext context) : PageObjectBase<TodoEditPage>(context)
{
    public Entry<TodoEditPage> Title => Entry("TodoEdit_Title");

    public Button<TodoEditPage> Save => Button("TodoEdit_Save");

    public TodoEditPage EnterTitle(string title)
    {
        Title.SetText(title);
        return this;
    }

    public TodoListPage SaveNew()
    {
        Save.Click();
        return new TodoListPage(Context);
    }
}

public sealed class TodoDetailPage(IMauiTestContext context) : PageObjectBase<TodoDetailPage>(context);

// docs/guides/uat-step-definitions.md quotes this page's regions: change them here, then run
// tools/Scripts/update-doc-samples.ps1.
public sealed class TodoListPage(IMauiTestContext context) : PageObjectBase<TodoListPage>(context)
{
    private readonly AppRoot _appRoot = new(context);

    #region docs:navigates-to
    [UatNavigatesTo(typeof(TodoEditPage))]
    public ToolbarButton<AppRoot> AddButton => new(_appRoot, Locator.ByAccessibilityId("TodoList_Add"));
    #endregion

    public TodoEditPage Add()
    {
        AddButton.Click();
        return new TodoEditPage(Context);
    }

    #region docs:page-phrase
    [BddWhen("I create a todo {title}")]
    public TodoListPage CreateTodo(string title) => Add().EnterTitle(title).SaveNew();
    #endregion
}

/// <summary>The Todo list's rows, as the sample's TodoList collection.</summary>
public sealed class TodoList;

/// <summary>The status of a todo, as the sample's TodoStatus control.</summary>
public sealed class TodoStatus<TScope>;
