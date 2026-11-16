using AVFoundation;

namespace Bruinet.Samples.NativeIos.App.Screens;

/// <summary>An alert, an alert with a text field, an action sheet, the camera permission alert, and a long piece of work.</summary>
public sealed class DialogsScreen() : Screen("Dialogs")
{
    private UILabel _status = null!;

    /// <inheritdoc />
    protected override void Build()
    {
        base.Build();
        _status = Label("status", "Nothing asked");
        Stack.AddArrangedSubview(Button("show_alert", "Delete everything", ShowAlert));
        Stack.AddArrangedSubview(Button("rename", "Rename", ShowRename));
        Stack.AddArrangedSubview(Button("show_sheet", "Share", ShowSheet));
        Stack.AddArrangedSubview(Button("ask_camera", "Use the camera", AskCamera));
        Stack.AddArrangedSubview(Button("work", "Work for a while", () => Busy(2000, () => _status.Text = "Work done")));
        Stack.AddArrangedSubview(_status);
    }

    private void ShowAlert()
    {
        var alert = UIAlertController.Create("Delete everything?", "This cannot be undone.", UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => _status.Text = "Kept"));
        alert.AddAction(UIAlertAction.Create("Delete", UIAlertActionStyle.Destructive, _ => _status.Text = "Deleted"));
        PresentViewController(alert, true, null);
    }

    private void ShowRename()
    {
        var alert = UIAlertController.Create("Rename", "Give it a new name.", UIAlertControllerStyle.Alert);
        alert.AddTextField(field => field.Placeholder = "Name");
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => _status.Text = "Not renamed"));
        alert.AddAction(UIAlertAction.Create("Rename", UIAlertActionStyle.Default, _ => _status.Text = $"Renamed to {alert.TextFields![0].Text}"));
        PresentViewController(alert, true, null);
    }

    private void ShowSheet()
    {
        var sheet = UIAlertController.Create("Share", "Where to?", UIAlertControllerStyle.ActionSheet);
        foreach (var place in new[] { "Copy", "Mail" })
        {
            sheet.AddAction(UIAlertAction.Create(place, UIAlertActionStyle.Default, _ => _status.Text = $"{place} chosen"));
        }

        sheet.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, _ => _status.Text = "Not shared"));
        PresentViewController(sheet, true, null);
    }

    private void AskCamera()
    {
        switch (AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video))
        {
            case AVAuthorizationStatus.Authorized:
                _status.Text = "Camera allowed";
                return;
            case AVAuthorizationStatus.Denied or AVAuthorizationStatus.Restricted:
                _status.Text = "Camera denied";
                return;
            default:
                AVCaptureDevice.RequestAccessForMediaType(
                    AVAuthorizationMediaType.Video,
                    granted => InvokeOnMainThread(() => _status.Text = granted ? "Camera allowed" : "Camera denied"));
                return;
        }
    }
}
