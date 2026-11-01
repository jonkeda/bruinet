using Bruinet.Core.Exceptions;
using Xunit;

namespace Bruinet.Uat.Tests;

// A failed step's kind in its report (AI2): from the exception and the ones inside it.
public sealed class UatStepFailureTests
{
    public static TheoryData<Exception, string> Failures => new()
    {
        { new AssertionException("Expected Text to be 'Done'.", "Done", "Overdue", "AutomationId:StatusText"), "assertion" },
        { Thrown(() => Assert.Fail("The runner counted 1 times, not 5.")), "assertion" },
        { new ElementNotFoundException("Not found: #save."), "locator-not-found" },
        { new WaitTimeoutException("Waited for Save.", 5000, "Save shown"), "timeout" },
        { new TimeoutException("Too slow."), "timeout" },
        { new InvalidOperationException("Failed to run 'Click' on control 'Save'.", new AssertionException("Expected it enabled.")), "assertion" },
        { Thrown(() => throw new FormatException("Not a date.")), "app-exception" },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void AFailure_IsKnownByItsExceptions(Exception exception, string kind)
        => Assert.Equal(kind, UatStepFailure.Kind([.. UatStepFailure.Chain(exception)], bound: true));

    [Fact]
    public void TheFrames_AreTheSuitesAndTheAppsCode_NotTheFrameworks()
    {
        Assert.Contains(UatStepFailure.Frames([Thrown(() => throw new FormatException("Not a date."))]), frame => frame.StartsWith("UatStepFailureTests.", StringComparison.Ordinal));
        Assert.Contains(UatStepFailure.Frames([Thrown(() => Assert.Fail("No."))]), frame => frame.StartsWith("UatStepFailureTests.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLocator_IsTheExceptionsOwn_ElseTheOneItsMessageNames()
    {
        Assert.Equal("AutomationId:StatusText", (string?)UatStepFailure.Locate([new AssertionException("Expected Text to be 'Done'.", "Done", "Overdue", "AutomationId:StatusText")]));
        Assert.Equal("AutomationId:StatusText", (string?)UatStepFailure.Locate([new InvalidOperationException("Failed to run 'AssertRowStatus' on control 'Todos': Expected Text to be 'Done'. Locator: AutomationId:StatusText\r\nActual: 'Overdue'.")]));
        Assert.Null(UatStepFailure.Locate([new FormatException("Not a date.")]));
    }

    [Fact]
    public void AStepWithNoDefinition_IsABindFailure()
        => Assert.Equal("bind", UatStepFailure.Kind([], bound: false));

    // Thrown here, in this test's own code: a frame that is not Bruinet's or the runtime's.
    private static Exception Thrown(Action act)
    {
        try
        {
            act();
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Nothing was thrown.");
    }
}
