namespace Bruinet.Recorder.WinUI;

/// <summary>The Windows app.</summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>Starts the app; the webview's profile is the account's, outside the program's own folder.</summary>
    public App()
    {
        // WebView2 reads where it keeps its sign-in from this variable, when its first webview is made.
        var profile = MauiProgram.Options.ProfileFolder;
        Directory.CreateDirectory(profile);
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", profile);
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
