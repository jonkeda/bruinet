using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.IntegrationTests.Service.Steps;

/// <summary>
/// The todo steps the UI heads say with their screen (a page phrase, a list row), said to the
/// app's <see cref="TodoService"/> and repository instead. Same words, so a scenario tagged for a UI
/// head and <c>@service</c> reads the same on both.
/// </summary>
[BddStepDefinitions(Namespace = "Todos")]
public sealed class TodoSteps(TodoServiceFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>Creates an open todo with no due date, as the Add form does with only a title.</summary>
    [BddWhen("I create a todo {title}")]
    public Task CreateAsync(string title) => fixture.Host.Todos.CreateAsync(title, notes: null, dueDate: null, TodoStatus.Open);

    /// <summary>The todo has changes the server has not seen yet.</summary>
    [BddThen("the todo {title} should be waiting to sync")]
    public async Task WaitingToSyncAsync(string title)
    {
        var todo = await FindAsync(title);
        Assert.True(todo.NeedsPush, $"'{title}' is {todo.SyncState}, not waiting to sync.");
    }

    /// <summary>The server has seen every change to the todo.</summary>
    [BddThen("the todo {title} should be synced")]
    public async Task SyncedAsync(string title)
    {
        var todo = await FindAsync(title);
        Assert.Equal(SyncState.Synced, todo.SyncState);
    }

    private async Task<TodoItem> FindAsync(string title)
    {
        var todos = await fixture.Host.Todos.GetVisibleAsync();
        return todos.SingleOrDefault(todo => todo.Title == title)
            ?? throw new InvalidOperationException($"No todo '{title}'. The todos are: {string.Join(", ", todos.Select(todo => $"'{todo.Title}'"))}.");
    }
}
