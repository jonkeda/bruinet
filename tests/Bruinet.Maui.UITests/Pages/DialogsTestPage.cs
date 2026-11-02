using Bruinet.Maui.Controls.Dialogs;

namespace Bruinet.Maui.UITests.Pages;

/// <summary>
/// Page object for the native dialog sample page.
/// </summary>
public class DialogsTestPage : PageObjectBase<DialogsTestPage>
{
    public DialogsTestPage(IMauiTestContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    public override string Name => "DialogsPage";

    public Button<DialogsTestPage> ShowAlertButton => new(this, "ShowAlertButton");

    public Button<DialogsTestPage> ShowConfirmButton => new(this, "ShowConfirmButton");

    public Button<DialogsTestPage> ShowPromptButton => new(this, "ShowPromptButton");

    public Button<DialogsTestPage> ShowConfirmThenErrorButton => new(this, "ShowConfirmThenErrorButton");

    public Button<DialogsTestPage> ResetButton => new(this, "DialogsResetButton");

    public Label<DialogsTestPage> Result => new(this, "DialogResultLabel");

    public ContentDialog<DialogsTestPage> Dialog => new(this);
}
