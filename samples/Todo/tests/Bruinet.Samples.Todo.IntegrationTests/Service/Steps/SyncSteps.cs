using Bruinet.Bdd;
using Bruinet.Core.Testing;
using Bruinet.Uat;

namespace Bruinet.Samples.Todo.IntegrationTests.Service.Steps;

/// <summary>
/// Syncing, and what the last sync did: the app's <see cref="ISyncService"/>, which a UI head reaches
/// through the Sync button and the Last Sync label.
/// </summary>
[BddStepDefinitions(Namespace = "Sync")]
public sealed class SyncSteps(TodoServiceFixture fixture) : BddStepDefinitionsBase
{
    /// <summary>Runs one sync to its end.</summary>
    [BddWhen("I sync")]
    public Task SyncAsync() => fixture.Host.Sync.SyncAsync();

    /// <summary>The last sync completed and sent this many todos.</summary>
    [BddThen("the sync should succeed with {count} pushed")]
    public void Succeeded(int count)
    {
        var result = Last();
        Assert.True(result.Succeeded, $"The sync failed: {result.Error}.");
        Assert.Equal(count, result.Pushed);
    }

    /// <summary>The last sync stopped, for this reason.</summary>
    [BddThen("the sync should fail with {error}")]
    public void Failed(SyncError error) => Assert.Equal(error, Last().Error);

    private SyncResult Last() => fixture.Host.Sync.LastResult
        ?? throw new InvalidOperationException("No sync has run. Say 'When I sync' first.");
}
