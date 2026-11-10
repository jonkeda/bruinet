using Bruinet.Core.Logging;

namespace Bruinet.Maui.Tests.Semantic;

/// <summary>
/// <c>WaitAttribute(name, until)</c>: a predicate polled within the call's budget, the last value
/// in the log when it never held. As Html's and native Android's.
/// </summary>
public class PredicateWaitTests : SemanticControlTestsBase
{
    private readonly Mock<ITestLogger> _logger = new();

    public PredicateWaitTests()
    {
        Context.Setup(c => c.Logger).Returns(_logger.Object);
    }

    private Mock<IMauiElement> GivenNotes()
    {
        var element = CreateElement("Notes", 0, 0, 200, 40);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Notes"))).Returns(element.Object);
        Context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "Notes"))).Returns(element.Object);
        return element;
    }

    [Fact]
    public void WaitAttribute_Passes_WhenTheValueChangesWithinTheBudget()
    {
        var reads = 0;
        GivenNotes().Setup(e => e.GetAttribute("Value")).Returns(() => ++reads < 4 ? "" : "typed");

        Assert.True(Page.Notes.WaitAttribute("Value", value => !string.IsNullOrEmpty(value), timeoutMs: 1000));
        Assert.True(reads >= 4);
    }

    [Fact]
    public void WaitAttribute_AnswersFalse_WithTheLastValueInTheLog_WhenThePredicateNeverHolds()
    {
        GivenNotes().Setup(e => e.GetAttribute("Value")).Returns("draft");

        Assert.False(Page.Notes.WaitAttribute("Value", value => value == "final", timeoutMs: 200));

        _logger.Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Action == "WaitAttribute" && e.Result == LogResult.Error && (e.Message != null && e.Message.Contains("value 'draft'")))),
            Times.Once);
    }
}
