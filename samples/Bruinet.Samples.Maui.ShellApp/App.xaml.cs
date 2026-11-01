namespace Bruinet.Samples.Maui.ShellApp;

public partial class App
{
    /// <summary>
    /// The native window title. Automation attaches to the app by this string, so it must
    /// differ from the other sample app's title or a driver could attach to the wrong window.
    /// </summary>
    public const string WindowTitle = "Bruinet Shell Sample";

    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell()) { Title = WindowTitle };
    }
}
