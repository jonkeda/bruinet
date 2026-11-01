using Bruinet.Maui.AppSupport.Uia;

namespace Bruinet.Samples.Maui.App.Views2.TestViews;

/// <summary>
/// Code-behind for the dialogs demo.
/// </summary>
/// <remarks>
/// The dialog calls live here rather than in the view model because DisplayAlert and
/// DisplayPromptAsync are Page methods - a view model has no Page to raise them on. The
/// result is pushed back into the view model so tests observe it the same way as every
/// other module.
///
/// They go through BruinetAlerts rather than calling the page directly, which is the one line
/// an app adds so that a test can assert what it asked rather than only that something opened.
/// Nothing about the dialogs changes: BruinetAlerts raises the same page methods with the same
/// arguments, and records them for the length of the await.
/// </remarks>
public partial class DialogsView : ContentView
{
    public DialogsView()
    {
        InitializeComponent();
    }

    private ViewModels.DialogsViewModel? ViewModel => BindingContext as ViewModels.DialogsViewModel;

    private async void OnShowAlert(object? sender, EventArgs e)
    {
        var page = GetPage();
        if (page == null)
            return;

        await BruinetAlerts.DisplayAlertAsync(page, "Alert", "This is an alert.", "OK");
        ViewModel?.Record("alert dismissed");
    }

    private async void OnShowConfirm(object? sender, EventArgs e)
    {
        var page = GetPage();
        if (page == null)
            return;

        var accepted = await BruinetAlerts.DisplayAlertAsync(
            page, "Confirm", "Proceed?", "Yes", "No");
        ViewModel?.Record(accepted ? "confirmed" : "declined");
    }

    private async void OnShowPrompt(object? sender, EventArgs e)
    {
        var page = GetPage();
        if (page == null)
            return;

        var answer = await BruinetAlerts.DisplayPromptAsync(
            page, "Prompt", "Enter a value", "OK", "Cancel");
        ViewModel?.Record(answer == null ? "prompt cancelled" : $"prompt: {answer}");
    }

    /// <summary>
    /// Finds the hosting page. Returns null rather than throwing when the view is not yet
    /// attached, which happens during teardown.
    /// </summary>
    private Page? GetPage()
    {
        Element? element = this;
        while (element != null && element is not Page)
        {
            element = element.Parent;
        }

        return element as Page;
    }
}
