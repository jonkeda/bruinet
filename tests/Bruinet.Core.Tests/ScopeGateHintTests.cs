using Bruinet.Core.Calls;
using Bruinet.Core.Scopes;

namespace Bruinet.Core.Tests;

// A scope that never became ready ends with what to look at next (review 01, proposal 6; R2-T34).
public sealed class ScopeGateHintTests
{
    [Fact]
    public void A_PageThatNeverLoaded_AsksWhetherTheAppIsOnIt()
    {
        var error = ScopeGate.NotReady(new ScopeReadiness("CounterPage", ScopeReadinessState.NotLoaded, "'#counter' is not shown"), "Click", "control 'Save'", 2000);

        Assert.EndsWith("Is the app on this page? Its ready element did not show: the step before may have gone elsewhere.", error.Message, StringComparison.Ordinal);
        Assert.Contains("('#counter' is not shown).", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_BusyPage_PointsAtTheTrace()
    {
        var error = ScopeGate.NotReady(new ScopeReadiness("ListPage", ScopeReadinessState.Busy, null), "Read", "control 'Rows'", 2000);

        Assert.Contains("shows in the step's trace", error.Message, StringComparison.Ordinal);
    }
}
