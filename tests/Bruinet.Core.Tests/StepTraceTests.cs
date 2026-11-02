using Bruinet.Core.Logging;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

// The control calls of a step (AI8): the ones made under its scope, and no other step's.
public sealed class StepTraceTests
{
    [Fact]
    public void AStep_GetsTheCallsItMade_AndNoOtherSteps()
    {
        TestScope first;
        TestScope second;
        using (TestScope.EnterScenario("Delete"))
        {
            using (TestScope.EnterStep("27: When I tap Delete"))
            {
                first = TestScope.Current;
                Timing.Call("Todo Detail", "Delete", "Tap", Timing.NowMs, 95, 1, "success");
            }

            using (TestScope.EnterStep("28: Then the dialog should ask"))
            {
                second = TestScope.Current;
                Timing.Call("Todo Edit", "Dialog", "Exists", Timing.NowMs, 5004, 24, "error");
            }
        }

        Assert.Equal(("Todo Detail", "Delete", "Tap", 95, 1, "success"), Summary(Assert.Single(StepTrace.Of(first))));
        Assert.Equal(("Todo Edit", "Dialog", "Exists", 5004, 24, "error"), Summary(Assert.Single(StepTrace.Of(second))));
    }

    [Fact]
    public void TheSameStepRunAgain_GetsOnlyItsOwnRunsCalls()
    {
        using (TestScope.EnterStep("27: When I tap Delete"))
        {
            Timing.Call("Todo Detail", "Delete", "Tap", Timing.NowMs, 95, 1, "success");
        }

        using (TestScope.EnterStep("27: When I tap Delete"))
        {
            Assert.Empty(StepTrace.Of(TestScope.Current));
        }
    }

    private static (string, string, string, int, int, string) Summary(StepTraceCall call)
        => (call.Page, call.Control, call.Action, call.DurationMs, call.Attempts, call.Result);
}
