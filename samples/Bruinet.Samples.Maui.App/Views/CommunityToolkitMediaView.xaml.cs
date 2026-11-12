namespace Bruinet.Samples.Maui.App.Views2.TestViews;

/// <summary>
/// Code-behind for the CommunityToolkit MediaElement demo.
/// </summary>
public partial class CommunityToolkitMediaView : ContentView
{
    public CommunityToolkitMediaView()
    {
        InitializeComponent();
#if ANDROID
        TestMedia.HandlerChanged += (_, _) => KeepControlsShown(TestMedia.Handler?.PlatformView as Android.Views.View);
#endif
    }

#if ANDROID
    /// <summary>
    /// Media3's controls hide a few seconds after they show, and a hidden control is not in the accessibility tree:
    /// a test reading the elapsed time would find nothing. Here they stay shown (R2-T121).
    /// </summary>
    private static void KeepControlsShown(Android.Views.View? view)
    {
        switch (view)
        {
            case AndroidX.Media3.UI.PlayerView player:
                player.ControllerShowTimeoutMs = 0;
                player.ControllerHideOnTouch = false;
                player.ShowController();
                break;
            case Android.Views.ViewGroup group:
                for (var i = 0; i < group.ChildCount; i++)
                {
                    KeepControlsShown(group.GetChildAt(i));
                }

                break;
        }
    }
#endif
}
