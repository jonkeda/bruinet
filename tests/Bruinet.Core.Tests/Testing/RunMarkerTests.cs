using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests.Testing;

/// <summary>A run's marker, and undoing what a test made in reverse, whatever the test did.</summary>
public sealed class RunMarkerTests
{
    [Fact]
    public void A_marker_is_BRN_and_the_runs_start()
    {
        Assert.Equal("BRN-1115-0041", RunMarker.For(new DateTime(2026, 11, 15, 0, 41, 30)));
        Assert.True(RunMarker.IsMarked("BRN-1115-0041 Arend"));
        Assert.True(RunMarker.IsMarked(RunMarker.Current));
        Assert.False(RunMarker.IsMarked("Arend BRN-1115-0041"));
        Assert.False(RunMarker.IsMarked("BRN-the-house"));
    }

    [Fact]
    public void Undos_run_last_first_and_a_failing_one_does_not_stop_the_others()
    {
        var done = new List<string>();
        var registry = new CleanUpRegistry();
        registry.Add("first", () => done.Add("first"));
        registry.Add("second", () => throw new InvalidOperationException("gone already"));
        registry.Add("third", () => done.Add("third"));

        registry.Dispose();

        Assert.Equal(["third", "first"], done);
        Assert.Equal(["second: gone already"], registry.Failures);
    }
}
