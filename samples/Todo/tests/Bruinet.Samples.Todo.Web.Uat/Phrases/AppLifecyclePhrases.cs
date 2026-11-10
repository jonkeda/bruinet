using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Samples.Todo.Web.Uat.Runtime;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.Web.Uat.Phrases;

/// <summary>
/// Steps about the app's process: restarting it. The web head's version of the MAUI suite's class;
/// a phrase that changes here changes there too, since the scenario files are shared.
/// </summary>
[BddStepDefinitions(Namespace = "AppLifecycle")]
public sealed class AppLifecyclePhrases(TodoWebUatFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>Stops the app and starts it again on the same database and backend.</summary>
    [BddWhen("I restart the app")]
    public void RestartApp() => fixture.Restart();

    /// <summary>Restarts the app pointed at an address nothing listens on; the next scenario restores it.</summary>
    [BddWhen("I restart the app with the server unreachable")]
    public void RestartUnreachable() => fixture.RestartWith(settings => settings with { ApiBaseUrl = "http://127.0.0.1:1" });
}
