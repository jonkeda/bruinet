using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.Contracts;
using Bruinet.Samples.Todo.Web.Uat.Runtime;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Web.Uat.Phrases;

/// <summary>
/// Steps about what the fake server received from each user's app. Only the web head runs several
/// users, so only it has these; the other backend steps are the shared <c>BackendPhrases</c>.
/// </summary>
[BddStepDefinitions(Namespace = "Backend")]
public sealed class UserBackendPhrases(TodoWebUatFixture fixture) : BddStepDefinitionsBase
{
    // The waits a scenario step gets: the MAUI suite's TestConstants.PageTimeoutMs.
    private const int PageTimeoutMs = 10_000;

    /// <summary>
    /// The fake server received the todo from that user's app: a PUT of it that carries the app's
    /// <c>X-Todo-Client</c>. A wait on the request, not a count (<c>todo-blazor.md</c> 4.1).
    /// </summary>
    [BddThen("the server should have received the todo {title} from {user}")]
    public void ServerReceivedFrom(string title, string user)
    {
        TestSupport.Uat.BackendPhrases.NeedsTheStub(fixture, "the server should have received the todo from a user");
        var clientId = fixture.ClientIdOfUser(user);
        fixture.Backend.WaitForRequest(
            "PUT",
            TodoApi.TodosPath + "/*",
            request => request.BodyAs<TodoDto>(TodoApi.JsonOptions)?.Title == title
                && request.Headers.TryGetValue(TodoApi.ClientHeader, out var sender)
                && sender == clientId,
            TimeSpan.FromMilliseconds(PageTimeoutMs));
    }
}
