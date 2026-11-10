using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.IntegrationTests.Service.Steps;

/// <summary>
/// The app's process, below the UI: a restart throws the services away and builds them again on the
/// same database file (<see cref="TodoHost.Restart"/>). The same words as each UI head's
/// <c>AppLifecyclePhrases</c>.
/// </summary>
[BddStepDefinitions(Namespace = "AppLifecycle")]
public sealed class AppLifecycleSteps(TodoServiceFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>Restarts the app on the same database and backend.</summary>
    [BddWhen("I restart the app")]
    public void Restart() => fixture.Host.Restart();
}
