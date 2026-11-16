namespace Bruinet.Samples.NativeIos.App;

/// <summary>The app's entry point.</summary>
public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

/// <summary>The application delegate; the scene delegate owns the window.</summary>
[Register("AppDelegate")]
public sealed class AppDelegate : UIApplicationDelegate
{
    /// <inheritdoc />
    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions) => true;

    /// <inheritdoc />
    public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        => new("Default Configuration", connectingSceneSession.Role);
}

/// <summary>
/// The window: a navigation stack with the gallery at its root. A deep link
/// (<c>bruinet-native://login?stuck=1</c>) returns to the gallery and opens that screen on top of it,
/// so every screen has a back button.
/// </summary>
[Register("SceneDelegate")]
public sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    private UINavigationController? _navigation;

    /// <summary>The scene's window.</summary>
    [Export("window")]
    public UIWindow? Window { get; set; }

    /// <summary>Builds the window, and opens the screen of the link that launched the app.</summary>
    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is not UIWindowScene windowScene)
        {
            return;
        }

        _navigation = new UINavigationController(new GalleryScreen());
        Window = new UIWindow(windowScene) { RootViewController = _navigation };
        Window.MakeKeyAndVisible();
        if (connectionOptions.UrlContexts?.AnyObject is UIOpenUrlContext launch)
        {
            Open(launch.Url);
        }
    }

    /// <summary>Opens the screen of a link that arrives while the app runs.</summary>
    [Export("scene:openURLContexts:")]
    public void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        if (urlContexts.AnyObject is { } context)
        {
            Open(context.Url);
        }
    }

    private void Open(NSUrl url)
    {
        if (_navigation is null || GalleryScreen.ForLink(url) is not { } screen)
        {
            return;
        }

        _navigation.DismissViewController(false, null);
        _navigation.PopToRootViewController(false);
        _navigation.PushViewController(screen, false);
    }
}
