#if WINDOWS || MACCATALYST
using CommunityToolkit.Maui;
#endif

namespace Bruinet.Recorder;

/// <summary>Builds the Recorder's app.</summary>
public static class MauiProgram
{
    /// <summary>What the app was started with; read before the first webview exists, which takes its profile from it.</summary>
    public static StartOptions Options { get; } = StartOptions.Parse([.. Environment.GetCommandLineArgs().Skip(1)], Environment.CurrentDirectory);

    /// <summary>The app.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<RecorderApp>();
#if WINDOWS || MACCATALYST
        builder.UseMauiCommunityToolkit(); // the folder picker
#endif
        return builder.Build();
    }
}

/// <summary>The Recorder: one window.</summary>
public sealed class RecorderApp : Application
{
    /// <inheritdoc/>
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new MainPage(MauiProgram.Options)) { Title = "Bruinet Recorder" };
}
