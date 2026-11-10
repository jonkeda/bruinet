namespace Bruinet.NativeAndroid.Tests;

/// <summary>
/// <c>WaitText(until)</c> and <c>WaitAttribute(name, until)</c>: a predicate polled within the
/// call's budget, the last value in the log when it never held. As Html's.
/// </summary>
public class PredicateWaitTests
{
    private readonly FakeApp _app = new();

    [Fact]
    public void WaitText_Passes_WhenThePredicateHoldsAtOnce()
    {
        _app.Given("Target", () => FakeApp.View(text: "3 targets").Object);

        Assert.True(_app.Page().Status.WaitText(text => text?.StartsWith('3') == true, timeoutMs: 500));
    }

    [Fact]
    public void WaitAttribute_Passes_WhenTheValueChangesWithinTheBudget()
    {
        var reads = 0;
        var view = FakeApp.View();
        view.Setup(e => e.GetAttribute("checked")).Returns(() => ++reads < 4 ? "false" : "true");
        _app.Given("Target", () => view.Object);

        Assert.True(_app.Page().Status.WaitAttribute("checked", value => value == "true", timeoutMs: 1000));
        Assert.True(reads >= 4);
    }

    [Fact]
    public void WaitAttribute_AnswersFalse_WithTheLastValueInTheLog_WhenThePredicateNeverHolds()
    {
        var view = FakeApp.View();
        view.Setup(e => e.GetAttribute("checked")).Returns("false");
        _app.Given("Target", () => view.Object);

        Assert.False(_app.Page().Status.WaitAttribute("checked", value => value == "true", timeoutMs: 200));

        _app.Logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Action == "WaitAttribute" && e.Result == LogResult.Error && (e.Message != null && e.Message.Contains("value 'false'")))),
            Times.Once);
    }
}
