using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.Contracts;
using Bruinet.Bdd;

namespace Bruinet.Samples.Todo.TestSupport.Uat;

/// <summary>
/// Steps about the fake server: the todos it holds, and the faults and delays it answers with.
/// </summary>
/// <remarks>
/// Tables bind to the real types (<see cref="TodoDto"/>), listing only the columns that matter;
/// relative dates are resolved against the test set's now. A record list parameter
/// (<see cref="ServerAlsoHas"/>) gets its rows from the engine, checked when the step binds. The
/// same steps for every head: each head's fixture is an <see cref="ITodoArrangement"/>.
/// </remarks>
[BddStepDefinitions(Namespace = "Backend")]
public sealed class BackendPhrases(ITodoArrangement fixture) : BddStepDefinitionsBase
{
    /// <summary>Adds the table's todos to the fake server, as another device would.</summary>
    [BddGiven("the server also has todos")]
    public void ServerAlsoHas(IReadOnlyList<ServerTodoRow> todos)
    {
        var data = new TodoData(fixture.TestSet.Now);
        foreach (var row in todos)
        {
            fixture.Server.Store(data.Todo(row.Title, row.Status, row.DueDate, row.Notes));
        }
    }

    /// <summary>Every request for the todo list fails with this status until the next scenario.</summary>
    [BddGiven("the server fails with status {status}")]
    public void ServerFails(int status) => fixture.Backend.Get(TodoApi.TodosPath).AtPriority(1).Fails(status);

    /// <summary>The next request for the todo list fails with this status; later ones succeed.</summary>
    [BddGiven("the server fails once with status {status}")]
    public void ServerFailsOnce(int status)
        => fixture.Backend.Get(TodoApi.TodosPath).AtPriority(1).FailsFirst(1, status).ReturnsJson(fixture.Server.Todos);

    /// <summary>The todo list comes back only after this many seconds.</summary>
    [BddGiven("the server answers after {seconds} seconds")]
    public void ServerIsSlow(int seconds)
        => fixture.Backend.Get(TodoApi.TodosPath).AtPriority(1).WithDelay(TimeSpan.FromSeconds(seconds)).ReturnsJson(fixture.Server.Todos);

    /// <summary>Drops the failures arranged above: the server answers normally again, with the todos it held.</summary>
    [BddWhen("the server recovers")]
    public void ServerRecovers()
    {
        var todos = fixture.Server.Todos.ToList();
        fixture.ResetBackend();
        foreach (var todo in todos)
        {
            fixture.Server.Store(todo);
        }
    }

    /// <summary>The fake server holds a todo like each row.</summary>
    [BddThen("the server should have todos")]
    public void ServerHas(BddTable table)
    {
        NeedsTheStub(fixture, "the server should have todos");
        table.AssertContains(fixture.Server.Todos, fixture.TestSet.Now);
    }

    /// <summary>
    /// The fake server received exactly these requests, in this order, since the scenario began:
    /// a request's resource is the first path segment after <c>/api/</c> (<c>todos</c>, <c>users</c>).
    /// </summary>
    [BddThen("the server should have received, in order")]
    public void ServerReceivedInOrder(IReadOnlyList<RequestRow> requests)
    {
        // Replayed, the calls the app made in this scenario: the stub server received none of them.
        var actual = fixture.ReplayedCalls is { } calls
            ? calls.Select(call => new RequestRow(call.Method, ResourceOf(new Uri(call.Url).AbsolutePath)))
            : fixture.Backend.Requests().Select(request => new RequestRow(request.Method, ResourceOf(request.Path)));
        BddSets.AssertSet(requests.Select(Normal), actual.Select(Normal), ordered: true, what: "requests the server received");

        static RequestRow Normal(RequestRow row) => new(row.Method.ToUpperInvariant(), row.Resource.ToLowerInvariant());
    }

    /// <summary>
    /// Stops a step that reads the stub server's state back while the scenario is replayed: a replay sends it nothing, so
    /// its state is the scenario's start, not what the app did.
    /// </summary>
    internal static void NeedsTheStub(ITodoArrangement fixture, string step)
    {
        if (fixture.ReplayedCalls is not null)
        {
            throw new InvalidOperationException(
                $"'{step}' reads the stub server back, which a replayed scenario never changes: run it with the backend as a stub (BRUINET_HTTP_REPLAY unset).");
        }
    }

    private static string ResourceOf(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries) is [_, var resource, ..] ? resource : path;

    /// <summary>A request as a step table writes it.</summary>
    /// <param name="Method">The HTTP method, <c>GET</c>.</param>
    /// <param name="Resource">The resource, <c>todos</c>.</param>
    public sealed record RequestRow(string Method, string Resource);

    /// <summary>A server todo as a step table writes it.</summary>
    /// <param name="Title">The title.</param>
    /// <param name="Status">The status; Open when left out.</param>
    /// <param name="DueDate">The due date, e.g. <c>+2d</c>; none when left out.</param>
    /// <param name="Notes">Notes; none when left out.</param>
    public sealed record ServerTodoRow(string Title, TodoStatusDto Status = TodoStatusDto.Open, DateOnly? DueDate = null, string? Notes = null);
}
