namespace Bruinet.Api.Mocking.Tests.Api;

/// <summary>
/// The shared server is process-wide state, so these run one at a time.
/// </summary>
[Collection(SharedServerCollection.Name)]
public sealed class SharedMockApiServerTests
{
    [Fact]
    public void AcquireShared_GivesEveryCallerTheSameServer()
    {
        var first = MockApiServer.AcquireShared();
        var second = MockApiServer.AcquireShared();

        try
        {
            Assert.Same(first, second);
        }
        finally
        {
            second.Dispose();
            first.Dispose();
        }
    }

    [Fact]
    public void Dispose_StopsTheSharedServerOnlyWithTheLastLease()
    {
        var first = MockApiServer.AcquireShared();
        var second = MockApiServer.AcquireShared();

        first.Dispose();
        Assert.True(second.IsStarted);

        second.Dispose();
        Assert.False(second.IsStarted);
    }

    [Fact]
    public void AcquireShared_AfterTheLastRelease_StartsAFreshServer()
    {
        var old = MockApiServer.AcquireShared();
        old.Dispose();

        var fresh = MockApiServer.AcquireShared();
        try
        {
            Assert.NotSame(old, fresh);
            Assert.True(fresh.IsStarted);
        }
        finally
        {
            fresh.Dispose();
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedServerCollection
{
    public const string Name = "Shared mock API server";
}
