using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Microsoft.Maui.Controls.Hosting;

#if MAUI_DEVFLOW
using Microsoft.Maui.DevFlow.Agent;
#endif

// Not inside the #if WINDOWS below: UseBruinetGestureBridge is multi-targeted and the call is
// unconditional, so that turning the bridge on reads the same on every head. Off Windows it
// reports that there is no bridge and returns, which is the truth there - Android and iOS
// drive gestures with real touch input.
using Bruinet.Maui.AppSupport;
#if WINDOWS
using Bruinet.Samples.Maui.App.Platforms.Windows.Handlers;
#endif

namespace Bruinet.Samples.Maui.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            // MediaElement is its own package and its own registration. No Android foreground
            // service: the sample plays a local clip only while its page is open.
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false, static _ => { })
            .ConfigureMauiHandlers(handlers =>
            {
                // On Windows, makes Grid, the stack layouts, FlexLayout, AbsoluteLayout,
                // ContentView, and Border expose their AutomationId to UI Automation.
                // Without this, Bruinet container objects cannot resolve on Windows.
                // On Android, makes Slider and Stepper publish their range in app units.
                //
                // Referenced here as a project; copying AppSupport's sources into the
                // app is the equally supported alternative.
                //
                // A container with a one-tap recognizer is a button to UI Automation: Invoke runs its command,
                // as a tap would (the Shortcut rows, R2-T111).
                handlers.AddBruinetAutomationHandlers(options => options.InvokeCommand = view =>
                    view is View tappable
                    && tappable.GestureRecognizers.OfType<TapGestureRecognizer>()
                        .FirstOrDefault(tap => tap.NumberOfTapsRequired == 1 && tap.Command is not null) is { } tap
                        ? new AutomationCommand(tap.Command!, tap.CommandParameter)
                        : null);
            });

        // The gesture bridge, off unless this build was compiled with BRUINET_UIA_BRIDGE and
        // the launcher sets BRUINET_UIA_BRIDGE=1. Unconditional on purpose: the gates decide,
        // not the call site, so this line reads the same in a shipping build - where it does
        // nothing - as it does under test.
        //
        // Here rather than beside AddBruinetAutomationHandlers because it has to run before
        // the first page loads: elements publish themselves on Loaded.
        builder.UseBruinetGestureBridge();

#if MAUI_DEVFLOW
        // The port the DevFlow runner gave this launch, so two apps of one run do not share 9223.
        builder.AddMauiDevFlowAgent(options =>
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("BRUINET_DEVFLOW_PORT"), out var port))
                options.Port = port;
        });
#endif

#if WINDOWS
        // Configure TabbedPage to properly map AutomationId to tab elements
        // This fixes dotnet/maui#3996 where tabs don't expose AutomationId
        TabbedPageAutomationMapper.Configure();

#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
