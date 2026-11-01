using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace Bruinet.Samples.Maui.App.Views2.TestViews;

/// <summary>
/// Code-behind for the CommunityToolkit alerts demo.
/// </summary>
/// <remarks>
/// Popup, Snackbar and Toast are raised from a page, not declared in markup, so the calls
/// live here. Each records its outcome in <c>AlertResultLabel</c>. A platform that cannot show
/// one records the reason instead of crashing the app, so a test reads why.
/// </remarks>
public partial class CommunityToolkitAlertsView : ContentView
{
    public CommunityToolkitAlertsView()
    {
        InitializeComponent();
    }

    private async void OnShowPopup(object? sender, EventArgs e)
    {
        var page = GetPage();
        if (page == null)
            return;

        var popup = new Popup
        {
            AutomationId = "TestPopup",
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    new Label { AutomationId = "PopupMessageLabel", Text = "Popup message" },
                    CreatePopupCloseButton(),
                },
            },
        };

        AlertResult.Text = "popup open";
        await page.ShowPopupAsync(popup);
        AlertResult.Text = "popup closed";
    }

    private static Button CreatePopupCloseButton()
    {
        var button = new Button { AutomationId = "PopupCloseButton", Text = "Close" };
        button.Clicked += async (sender, _) =>
        {
            if (FindAncestor<Popup>(sender as Element) is { } popup)
            {
                await popup.CloseAsync();
            }
        };
        return button;
    }

    private async void OnShowSnackbar(object? sender, EventArgs e)
    {
        try
        {
            var snackbar = Snackbar.Make(
                "Snackbar message",
                () => AlertResult.Text = "snackbar action",
                "Undo",
                TimeSpan.FromSeconds(10));
            AlertResult.Text = "snackbar shown";
            await snackbar.Show();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
        {
            AlertResult.Text = $"snackbar unavailable: {ex.GetType().Name}";
        }
    }

    private async void OnShowToast(object? sender, EventArgs e)
    {
        try
        {
            var toast = Toast.Make("Toast message", ToastDuration.Long);
            AlertResult.Text = "toast shown";
            await toast.Show();
        }
        catch (Exception ex)
        {
            // Any failure, not a list: on Windows an unpackaged app's toast fails inside the
            // notification platform, and an async void handler that lets it escape takes the
            // app down with it.
            AlertResult.Text = $"toast unavailable: {ex.GetType().Name}";
        }
    }

    private Page? GetPage() => FindAncestor<Page>(this);

    private static T? FindAncestor<T>(Element? element) where T : Element
    {
        while (element != null && element is not T)
        {
            element = element.Parent;
        }

        return element as T;
    }
}
