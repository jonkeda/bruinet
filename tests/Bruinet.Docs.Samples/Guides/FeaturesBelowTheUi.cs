// docs/guides/features-below-the-ui.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Bdd;

namespace Bruinet.Docs.Samples.Guides;

#region docs:steps
[BddStepDefinitions(Namespace = "Sync")]
public sealed class SyncSteps(TodoServiceFixture fixture) : BddStepDefinitionsBase
{
    [BddWhen("I sync")]
    public Task SyncAsync() => fixture.Host.Sync.SyncAsync();
}
#endregion

/// <summary>The Todo sample's service fixture: a headless host of the app's services per scenario.</summary>
public sealed class TodoServiceFixture
{
    public TodoHost Host { get; } = new();
}

public sealed class TodoHost
{
    public SyncService Sync { get; } = new();
}

public sealed class SyncService
{
    public Task SyncAsync() => Task.CompletedTask;
}
