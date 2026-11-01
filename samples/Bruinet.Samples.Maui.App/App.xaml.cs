
using Microsoft.Maui;

namespace Bruinet.Samples.Maui.App;

public partial class App
{
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates the app window, titled so automation can find it.
    /// </summary>
    /// <remarks>
    /// The title is set explicitly because <see cref="NavigationPage"/> does not surface its
    /// page's <c>Title</c> as the native window title the way <c>Shell</c> did. Without this
    /// the Windows window has an <em>empty</em> title, and a driver that attaches by window
    /// title simply never finds the app — which presents as a hang, not as an error, since
    /// there is nothing to report except a wait that does not end.
    /// </remarks>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // A flat hub, not Shell. Shell's tab bar did not survive going cross-platform:
        // Android's BottomNavigationView hides tabs past the fifth behind an overflow menu,
        // and Windows only ever exposed nine, which is why four pages had to become routes.
        // The hub reaches every page with one identical click everywhere, and its flat stack
        // leaves no pushed route to leak between tests.
        return new Window(new NavigationPage(new HubPage())) { Title = WindowTitle };
    }

    /// <summary>
    /// The native window title. Automation attaches to the app by this string.
    /// </summary>
    public const string WindowTitle = "Bruinet MAUI Sample";
}
