using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Mocking;
using Bruinet.Samples.Todo.Mixed.Uat.Runtime;
using Bruinet.Samples.Todo.UITests;
using Bruinet.Samples.Todo.Web.UITests;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Mixed.Uat.Phrases;

/// <summary>
/// Steps about a user's app rather than its screen, the same for every head: its network, and
/// what the backend received from it.
/// </summary>
[BddStepDefinitions]
public sealed class TodoMixedPhrases(TodoMixedUatFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>The backend received the todo from the user's app: a PUT that carries its <c>X-Todo-Client</c>.</summary>
    [BddThen("the server should have received the todo {title} from {user}")]
    public void ServerReceivedFrom(string title, BddUserRole user) => fixture.WaitForTodoFrom(fixture.Apps.Get(user.Name), title);

    /// <summary>The user's app loses its network.</summary>
    [BddGiven("{user} is offline")]
    public void Offline(BddUserRole user) => NetworkOf(user).SetOffline();

    /// <summary>The user's app loses its network.</summary>
    [BddWhen("{user} goes offline")]
    public void GoesOffline(BddUserRole user) => NetworkOf(user).SetOffline();

    /// <summary>The user's app has its network back.</summary>
    [BddWhen("{user} is back online")]
    public void BackOnline(BddUserRole user) => NetworkOf(user).SetOnline();

    private NetworkStateFile NetworkOf(BddUserRole user) => fixture.Apps.Get(user.Name) switch
    {
        TodoMauiDevice maui => maui.Network,
        TodoWebDevice web => web.Network,
        ITestApp app => throw new NotSupportedException($"{app.Definition.Name} has no network switch."),
    };
}
