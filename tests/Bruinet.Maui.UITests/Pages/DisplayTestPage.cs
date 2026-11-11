namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the DisplayTestView. Exposes all display controls and their interactions.
/// Demonstrates the page object pattern with control locators for testing display controls.
/// </summary>
public class DisplayTestPage : PageObjectBase<DisplayTestPage>
{
    public DisplayTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "DisplayTestPage";

    #region Display Controls

    /// <summary>
    /// The Label test control displaying formatted text.
    /// </summary>
    public Label<DisplayTestPage> TestLabel => new(this, "TestLabel");

    /// <summary>
    /// The Image test control displaying an image from resources.
    /// </summary>
    public Image<DisplayTestPage> TestImage => new(this, "TestImage");

    /// <summary>
    /// The ActivityIndicator test control showing animation state.
    /// </summary>
    public ActivityIndicator<DisplayTestPage> TestActivityIndicator => new(this, "TestActivityIndicator");

    /// <summary>
    /// The ProgressBar test control showing progress value.
    /// </summary>
    public ProgressBar<DisplayTestPage> TestProgressBar => new(this, "TestProgressBar");

    /// <summary>A progress bar at 1.</summary>
    public ProgressBar<DisplayTestPage> CompleteProgressBar => new(this, "CompleteProgressBar");

    #endregion

    #region Buttons

    /// <summary>
    /// Button to toggle the ActivityIndicator running state.
    /// </summary>
    public Button<DisplayTestPage> ToggleActivityButton => new(this, "ToggleActivityButton");

    /// <summary>
    /// Button to decrease the progress bar value.
    /// </summary>
    public Button<DisplayTestPage> DecreaseProgressButton => new(this, "DecreaseProgressButton");

    /// <summary>
    /// Button to increase the progress bar value.
    /// </summary>
    public Button<DisplayTestPage> IncreaseProgressButton => new(this, "IncreaseProgressButton");

    /// <summary>
    /// The Reset button to clear all state.
    /// </summary>
    public Button<DisplayTestPage> ResetButton => new(this, "ResetButton");

    #endregion

    #region Labels

    /// <summary>
    /// The status message label showing test results.
    /// </summary>
    public Label<DisplayTestPage> StatusLabel => new(this, "StatusLabel");

    #endregion
}
