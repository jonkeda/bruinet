// docs/getting-started/quick-start-maui.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
// A MAUI app's own global usings.
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

#region docs:app
using Bruinet.Maui.AppSupport;

namespace MyMauiApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // Layouts and content views show their AutomationId to UI Automation on Windows.
            .ConfigureMauiHandlers(handlers => handlers.AddBruinetAutomationHandlers());

        // Before the first page loads; it does nothing unless a Debug build is started by a test.
        builder.UseBruinetGestureBridge();

        return builder.Build();
    }
}
#endregion

public sealed class App : Application;
