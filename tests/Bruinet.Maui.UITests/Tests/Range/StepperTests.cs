using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;

namespace Bruinet.Maui.UITests.Tests.Range;

/// <summary>
/// UI tests for the Stepper control in the RangeTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Stepper")]
public class StepperTests
{
    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StepperTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _fixture.Open(SamplePage.Range);
    }

    private RangeTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    [ProbeFact("reports what invoking a stepper's buttons does", Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Pattern", "Probe")]
    public Task StepperButtons_ReportInvokeBehavior()
    {
        var minus = _fixture.Context.TryFindElement(Locator.ByAutomationId("TestStepperMinus"));
        var plus = _fixture.Context.TryFindElement(Locator.ByAutomationId("TestStepperPlus"));
        var value = _fixture.Context.TryFindElement(Locator.ByAutomationId("StepperValueLabel"));

        _output.WriteLine($"Minus: found={minus is not null}, Enabled={minus?.Enabled}");
        _output.WriteLine($"Plus: found={plus is not null}, Enabled={plus?.Enabled}");

        var before = value?.Text ?? value?.Name;
        string result;

        try
        {
            plus?.Invoke();
            result = "returned";
        }
        catch (Exception ex)
        {
            result = $"threw {ex.GetType().Name}: {ex.Message}";
        }

        var after = value?.Text ?? value?.Name;
        _output.WriteLine($"Plus Invoke {result}; value before='{before}', after='{after}'.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper value updates when set.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetValue")]
    public Task Stepper_SetValue_UpdatesDisplay()
    {
        var page = GetPage();
        // Act
        page.TestStepper.SetValue(8)
            .StepperValueLabel.AssertTextContains("8");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper respects minimum value bounds.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MinValue")]
    public Task Stepper_SetValue_RespectsBounds_Min()
    {
        var page = GetPage();
        // Act & Assert
        page.TestStepper.SetValue(0)
            .StepperValueLabel.AssertTextContains("0");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper respects maximum value bounds.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MaxValue")]
    public Task Stepper_SetValue_RespectsBounds_Max()
    {
        var page = GetPage();
        // Act & Assert
        page.TestStepper.SetValue(10)
            .StepperValueLabel.AssertTextContains("10");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper increments by the correct step size.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Increment")]
    public Task Stepper_Increment_ChangesValueByStepSize()
    {
        var page = GetPage();
        // Arrange
        page.TestStepper.SetValue(5);

        // Act - increment
        page.TestStepper.Increment()
            .StepperValueLabel.AssertTextContains("6");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper decrements by the correct step size.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Decrement")]
    public Task Stepper_Decrement_ChangesValueByStepSize()
    {
        var page = GetPage();
        // Arrange
        page.TestStepper.SetValue(5);

        // Act - decrement
        page.TestStepper.Decrement()
            .StepperValueLabel.AssertTextContains("4");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper status message updates on value change.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "StatusUpdate")]
    public Task Stepper_SetValue_UpdatesStatus()
    {
        var page = GetPage();
        // Act
        page.TestStepper.SetValue(7)
            .StatusLabel.AssertTextContains("Stepper value");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper can be reset to its initial value.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task Stepper_Reset_RestoresInitialValue()
    {
        var page = GetPage();
        // Arrange - change value
        page.TestStepper.SetValue(8)
            .StepperValueLabel.AssertTextContains("8");

        // Act - reset
        page.ResetButton.Click()
            .StepperValueLabel.AssertTextContains("5");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper enforces minimum bounds on decrement.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "BoundsAtMin")]
    public Task Stepper_Decrement_StopsAtMinimum()
    {
        var page = GetPage();
        // Arrange - set to minimum
        page.TestStepper.SetValue(0);

        // Act - try to decrement at minimum
        page.TestStepper.Decrement()
            .StepperValueLabel.AssertTextContains("0");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Stepper enforces maximum bounds on increment.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "BoundsAtMax")]
    public Task Stepper_Increment_StopsAtMaximum()
    {
        var page = GetPage();
        // Arrange - set to maximum
        page.TestStepper.SetValue(10);

        // Act - try to increment at maximum
        page.TestStepper.Increment()
            .StepperValueLabel.AssertTextContains("10");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple stepper value changes are reflected correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleChanges")]
    public Task Stepper_MultipleValueChanges_UpdatesDisplay()
    {
        var page = GetPage();
        // Act & Assert
        page.TestStepper.SetValue(2)
            .StepperValueLabel.AssertTextContains("2")
            .TestStepper.SetValue(8)
            .StepperValueLabel.AssertTextContains("8")
            .TestStepper.SetValue(5)
            .StepperValueLabel.AssertTextContains("5");

        return Task.CompletedTask;
    }
}
