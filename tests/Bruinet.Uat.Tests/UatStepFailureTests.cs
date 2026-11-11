using System.Globalization;
using Bruinet.Core.Exceptions;
using Xunit;

namespace Bruinet.Uat.Tests;

// A failed step's kind in its report (AI2): from the exception and the ones inside it.
public sealed class UatStepFailureTests
{
    public static TheoryData<Exception, string> Failures => new()
    {
        { new ControlAssertionException("Expected Text to be 'Done'.", "Done", "Overdue", Bruinet.Core.Locators.Locator.ByAutomationId("StatusText")), "assertion" },
        { Thrown(() => Assert.Fail("The runner counted 1 times, not 5.")), "assertion" },
        { new ElementNotFoundException("Not found: #save."), "locator-not-found" },
        { new WaitTimeoutException("Waited for Save.", 5000, "Save shown"), "timeout" },
        { new TimeoutException("Too slow."), "timeout" },
        { new ScopeNotReadyException(new Bruinet.Core.Scopes.ScopeReadiness("TodoPage", Bruinet.Core.Scopes.ScopeReadinessState.NotLoaded), "Not ready."), "timeout" },
        { new StaleElementException(detail: "Gone."), "locator-not-found" },
        { new AppUnavailableException("The app is not running."), "app-unavailable" },
        { Thrown(() => _ = int.Parse("seven", CultureInfo.InvariantCulture)), "app-exception" },
        { new InvalidOperationException("Something inside."), "error" },
        { new InvalidOperationException("Failed to run 'Click' on control 'Save'.", new ControlAssertionException("Expected it enabled.")), "assertion" },
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
    public void TheFrames_LeaveOutBruinetsOwnUatEngine()
    {
        var frames = UatStepFailure.Frames([Thrown(() => UatScenarioResults.Record(null!, null!, null!, null!, TimeSpan.Zero, null, "x", "test"))]).ToList();

        Assert.DoesNotContain(frames, frame => frame.StartsWith("UatScenarioResults.", StringComparison.Ordinal));
        Assert.Contains(frames, frame => frame.StartsWith("UatStepFailureTests.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLocator_IsTheExceptionsOwn_WholeAndNeverReadFromItsMessage()
    {
        var css = Bruinet.Core.Locators.Locator.ByCss("li[data-row='2'] .status");
        Assert.Equal("AutomationId:StatusText", (string?)UatStepFailure.Locate([new ControlAssertionException("Expected Text to be 'Done'.", "Done", "Overdue", Bruinet.Core.Locators.Locator.ByAutomationId("StatusText"))]));
        Assert.Equal(css.ToString(), (string?)UatStepFailure.Locate([new InvalidOperationException("Failed to run 'AssertRowStatus' on control 'Todos'."), new ControlAssertionException("Expected Text to be 'Done'.", "Done", "Overdue") { Locator = css }]));
        Assert.Null(UatStepFailure.Locate([new InvalidOperationException("Failed: Expected Text to be 'Done'. Locator: AutomationId:StatusText")]));
        Assert.Null(UatStepFailure.Locate([new FormatException("Not a date.")]));
    }

    [Fact]
    public async Task AnAsyncMethodsFrame_IsNamedAsWritten()
    {
        var exception = await Assert.ThrowsAsync<FormatException>(SaveAsync);

        Assert.Contains(UatStepFailure.Frames([exception]), frame => frame.StartsWith("UatStepFailureTests.SaveAsync (", StringComparison.Ordinal));
    }

    [Fact]
    public void ALocatorNotFound_GivesItsLocator()
        => Assert.Equal("Css:#save", UatStepFailure.Locate([new ElementNotFoundException(Bruinet.Core.Locators.Locator.ByCss("#save"))]));

    [Fact]
    public void AStepWithNoDefinition_IsABindFailure()
        => Assert.Equal("bind", UatStepFailure.Kind([], bound: false));

    [Fact]
    public void ASetupFailure_IsASetupFailure_WhateverWentWrongInside()
    {
        var setup = Assert.Throws<UiSetupException>(() => UiSetup.Setup(() => throw new ElementNotFoundException("Not found: #new.")));

        Assert.Equal("setup", UatStepFailure.Kind([.. UatStepFailure.Chain(setup)], bound: true));
        Assert.IsType<ElementNotFoundException>(setup.InnerException);
    }

    [Fact]
    public void AFailedGivenStep_IsASetupFailure()
        => Assert.Equal("setup", UatStepFailure.Kind([new ElementNotFoundException("Not found: #new.")], bound: true, given: true));

    [Fact]
    public async Task SetupAsync_KeepsACancellation_AsItIs()
        => await Assert.ThrowsAsync<OperationCanceledException>(() => UiSetup.SetupAsync(() => throw new OperationCanceledException()));

    private static async Task SaveAsync()
    {
        await Task.Yield();
        throw new FormatException("Not a date.");
    }

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
