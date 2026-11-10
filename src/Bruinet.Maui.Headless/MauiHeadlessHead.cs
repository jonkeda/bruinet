using Bruinet.Core.Configuration;
using Bruinet.Maui.Interfaces;
using MauiHeadless.Rendering;
using BruinetMauiTestContext = Bruinet.Maui.Context.MauiTestContext;
using BruinetMauiTestContextOptions = Bruinet.Maui.Context.MauiTestContextOptions;
using MauiElement = Microsoft.Maui.Controls.Element;

namespace Bruinet.Maui.Headless;

/// <summary>The MauiHeadless head: a Bruinet test context over a page MauiHeadless rendered.</summary>
public static class MauiHeadlessHead
{
    /// <summary>
    /// Bruinet's timeouts for the head: the device fixture's budgets, because a headless app still
    /// waits on what is real (a backend answering after two seconds, a database), with no settling
    /// time for animations, which finish at once, and a shorter poll. A wait met at once costs nothing.
    /// </summary>
    public static TimeoutSettings Timeouts => new()
    {
        DefaultWait = TimeSpan.FromMilliseconds(5000),
        PageLoad = TimeSpan.FromMilliseconds(10000),
        ElementFind = TimeSpan.FromMilliseconds(3000),
        ElementState = TimeSpan.FromMilliseconds(3000),
        Animation = TimeSpan.FromMilliseconds(0),
        PollingInterval = TimeSpan.FromMilliseconds(25),
    };

    /// <summary>
    /// The options for a Bruinet context on an app made headless: a new MauiHeadless context, the
    /// <paramref name="root"/> page rendered into it, and the driver over that. For
    /// a fixture's <c>CreateHeadlessOptions</c>, which runs a device suite on the MauiHeadless head (<c>BRUINET_HEAD=headless</c>).
    /// </summary>
    /// <param name="root">The app's first page, as its <c>App</c> would show it.</param>
    /// <param name="configure">The MauiHeadless context's options: the app's <c>Application</c>, adapters.</param>
    /// <param name="arrange">
    /// The app's services, registered on the new context before anything renders: what its
    /// <c>MauiProgram</c> registers, with the context's dispatcher where the app's needs a main thread.
    /// </param>
    /// <param name="timeouts">Bruinet's timeouts; <see cref="Timeouts"/> by default.</param>
    public static BruinetMauiTestContextOptions Options(
        Func<IServiceProvider, Microsoft.Maui.Controls.Page> root,
        Action<global::MauiHeadless.MauiTestContextOptions>? configure = null,
        Action<global::MauiHeadless.MauiTestContext>? arrange = null,
        TimeoutSettings? timeouts = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        // The head's tests wait on what the app shows, as on a device, where timers run by themselves.
        var options = new global::MauiHeadless.MauiTestContextOptions { TimeFollowsWallClock = true };
        configure?.Invoke(options);
        var context = new global::MauiHeadless.MauiTestContext(options);
        arrange?.Invoke(context);

        // The app's log, kept by the step that made it, for the step's result (StepLog): after the app's registrations,
        // so one that clears the logging providers does not take it away. BRUINET_STEP_LOG=off leaves it out.
        Bruinet.Logging.StepLogServices.AddBruinetStepLog(context.Services);
        var rendered = context.Render(root);
        return new BruinetMauiTestContextOptions
        {
            Driver = new MauiHeadlessDriver(rendered, ownsContext: true),
            OwnsDriver = true,
            Timeouts = timeouts ?? Timeouts,
            Name = "MauiHeadless",
        };
    }

    /// <summary>
    /// A Bruinet <see cref="IMauiTestContext"/> on <paramref name="rendered"/>, for the same page objects
    /// the device heads use: <c>new ButtonsTestPage(rendered.AsBruinetContext())</c>.
    /// </summary>
    public static IMauiTestContext AsBruinetContext(this IRenderedElement<MauiElement> rendered, TimeoutSettings? timeouts = null)
        => new BruinetMauiTestContext(new BruinetMauiTestContextOptions
        {
            Driver = new MauiHeadlessDriver(rendered),
            Timeouts = timeouts ?? Timeouts,
            Name = "MauiHeadless",
        });
}
