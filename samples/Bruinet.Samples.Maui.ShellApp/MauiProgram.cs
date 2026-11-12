using Microsoft.Extensions.Logging;

using Bruinet.Maui.AppSupport;

#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace Bruinet.Samples.Maui.ShellApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers =>
            {
#if WINDOWS
                // Layouts expose their AutomationId to UI Automation. Every page below marks
                // itself with a layout carrying an id, so this is what makes the pages
                // addressable on Windows at all.
                handlers.AddBruinetAutomationHandlers();
#endif
            });

        // The gesture bridge's run-time switch (step 27). Without this line the bridge compiles in
        // and publishes nothing - which is how this app's flyout verbs went silent: the gate was
        // added to the hub app and not here, and nothing noticed until this app was rebuilt.
        builder.UseBruinetGestureBridge();

        // The DevFlow runner's agent, only in a build with -p:BruinetDevFlow=true (Bruinet.Maui.DevFlow).
#if MAUI_DEVFLOW
        // The port the DevFlow runner gave this launch, so two apps of one run do not share 9223.
        builder.AddMauiDevFlowAgent(options =>
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("BRUINET_DEVFLOW_PORT"), out var port))
                options.Port = port;

            // Bruinet's verbs over the agent: what the agent has no route for (Bruinet.Maui.AppSupport).
            options.AddBruinetBridge();
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
