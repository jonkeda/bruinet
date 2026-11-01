namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the RangeTestView. Exposes all range controls and their interactions.
/// Demonstrates the page object pattern with slider and stepper locators and action methods.
/// </summary>
public class RangeTestPage : PageObjectBase<RangeTestPage>
{
    public RangeTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "RangeTestPage";


    #region Slider

    /// <summary>
    /// The Slider test control for testing value range adjustments.
    /// </summary>
    public Slider<RangeTestPage> TestSlider => new(this, "TestSlider");

    /// <summary>
    /// The Slider value display label.
    /// </summary>
    public Label<RangeTestPage> SliderValueLabel => new(this, "SliderValueLabel");

    #endregion

    #region Stepper

    /// <summary>
    /// The Stepper test control for testing increment/decrement operations.
    /// </summary>
    public Stepper<RangeTestPage> TestStepper => new(this, "TestStepper");

    /// <summary>
    /// The Stepper value display label.
    /// </summary>
    public Label<RangeTestPage> StepperValueLabel => new(this, "StepperValueLabel");

    #endregion

    #region Labels

    /// <summary>
    /// The status message label showing test results and current state.
    /// </summary>
    public Label<RangeTestPage> StatusLabel => new(this, "StatusLabel");

    #endregion

    #region Buttons

    /// <summary>
    /// The Reset button to restore controls to initial state.
    /// </summary>
    public Button<RangeTestPage> ResetButton => new(this, "ResetButton");

    #endregion
}
