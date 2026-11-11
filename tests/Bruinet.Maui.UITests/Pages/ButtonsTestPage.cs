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

    /// <summary>The control contracts' button; <see cref="ContractAction"/> says what happened to it.</summary>
    public Button<ButtonsTestPage> ContractButton => new(this, "ContractButton");

    /// <summary>On the page, not visible.</summary>
    public Button<ButtonsTestPage> HiddenButton => new(this, "HiddenButton");

    /// <summary>Visible, not enabled.</summary>
    public Button<ButtonsTestPage> DisabledButton => new(this, "DisabledButton");

    /// <summary>Not on the page.</summary>
    public Button<ButtonsTestPage> MissingButton => new(this, "NoSuchButton");

    #endregion

    #region Labels

    /// <summary>
    /// The status message label showing test results.
    /// </summary>
    public Label<ButtonsTestPage> StatusLabel => new(this, "StatusLabel");

    /// <summary>What happened to <see cref="ContractButton"/> last.</summary>
    public Label<ButtonsTestPage> ContractAction => new(this, "ContractAction");

    /// <summary>Shows "Contract label".</summary>
    public Label<ButtonsTestPage> ContractLabel => new(this, "ContractLabel");

    /// <summary>A label with no text.</summary>
    public Label<ButtonsTestPage> EmptyLabel => new(this, "EmptyLabel");

    #endregion
}
