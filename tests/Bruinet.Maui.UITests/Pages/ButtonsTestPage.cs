namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the ButtonsTestView. Exposes all button controls and their interactions.
/// Demonstrates the page object pattern with control locators and action methods.
/// </summary>
public class ButtonsTestPage : PageObjectBase<ButtonsTestPage>
{
    public ButtonsTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "ButtonsTestPage";


    #region Buttons

    /// <summary>
    /// The basic Button test control.
    /// </summary>
    public Button<ButtonsTestPage> TestButton => new(this, "TestButton");

    /// <summary>
    /// The ImageButton test control.
    /// </summary>
    public Button<ButtonsTestPage> TestImageButton => new(this, "TestImageButton");

    /// <summary>
    /// The Reset button.
    /// </summary>
    public Button<ButtonsTestPage> ResetButton => new(this, "ResetButton");

    #endregion

    #region Labels

    /// <summary>
    /// The status message label showing test results.
    /// </summary>
    public Label<ButtonsTestPage> StatusLabel => new(this, "StatusLabel");

    #endregion
}
