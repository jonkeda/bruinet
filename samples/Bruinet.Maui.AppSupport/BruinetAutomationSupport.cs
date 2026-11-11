#if WINDOWS
using Bruinet.Maui.AppSupport.Handlers;
#endif
#if ANDROID
using Bruinet.Maui.AppSupport.Accessibility;
#endif
using Bruinet.Maui.AppSupport.Uia;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace Bruinet.Maui.AppSupport;

/// <summary>
/// The two lines an app under test adds: automation handlers that make MAUI containers
/// addressable, and the switch that turns the gesture bridge on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is needed.</b> Stock MAUI layouts (<c>Grid</c>, the stack layouts,
/// <c>FlexLayout</c>, <c>AbsoluteLayout</c>) and content containers
/// (<c>ContentView</c>, <c>Border</c>) map to WinUI panels that have no
/// AutomationPeer. Their <c>AutomationId</c> is therefore invisible to UI Automation
/// — FlaUI, WinAppDriver, and Appium cannot see them at all. Any Bruinet container
/// object targeting such a layout will fail to resolve, and the only symptom is an
/// <c>ElementNotFoundException</c> that looks exactly like a mistyped AutomationId.
/// </para>
/// <para>
/// <b>Two supported ways to use this.</b> Reference this project and call
/// <see cref="AddBruinetAutomationHandlers(Microsoft.Maui.Hosting.IMauiHandlersCollection)"/>, or copy the <c>Handlers</c> folder and
/// this file directly into the app under test. Both are expected: the app under test
/// is not always one you can add a project reference to.
/// </para>
/// <example>
/// <code>
/// builder.ConfigureMauiHandlers(handlers =&gt; handlers.AddBruinetAutomationHandlers());
/// </code>
/// </example>
/// </remarks>
public static class BruinetAutomationSupport
{
    /// <summary>
    /// Registers automation handlers for <c>Layout</c>, <c>ContentView</c>, <c>Border</c> and
    /// <c>ContentPage</c> on Windows, and the range accessibility mappings for <c>Slider</c> and
    /// <c>Stepper</c> on Android.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>Layout</c> registration is against the base type, so it covers every
    /// layout subclass at once.
    /// </para>
    /// <para>
    /// <b>SwipeView and RefreshView are deliberately absent.</b> They map to the WinUI
    /// <c>SwipeControl</c> and <c>RefreshContainer</c>, which already supply their own
    /// AutomationPeers. Overriding those peers does not just fail to help — it
    /// collapses the entire UIA tree, making every element in the app unaddressable
    /// while the app continues to render normally. This was measured, not assumed. To
    /// scope inside a SwipeView or RefreshView, wrap its <i>content</i> in a container
    /// that is addressable.
    /// </para>
    /// <para>
    /// <c>ContentPage</c> is included: it maps to the same peerless <c>ContentPanel</c>, so
    /// without a handler a page cannot be found by its own <c>AutomationId</c> and a page object
    /// must identify itself by a child control instead. Verified not to collapse the tree.
    /// </para>
    /// <para>
    /// <c>Frame</c> is also absent: it is deprecated in MAUI and has no handler to
    /// hook. Use <c>Border</c>, which is supported here.
    /// </para>
    /// <para>
    /// <b>On Android</b> nothing is registered as a handler. The <c>Slider</c> and <c>Stepper</c>
    /// mappers get an accessibility delegate that publishes their range in the app's units, which
    /// is what TalkBack announces and what a UI test reads (see <c>RangeAccessibility</c>). Call
    /// this on every head, not only inside <c>#if WINDOWS</c>.
    /// </para>
    /// </remarks>
    /// <param name="handlers">The handler collection from <c>ConfigureMauiHandlers</c>.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IMauiHandlersCollection AddBruinetAutomationHandlers(
        this IMauiHandlersCollection handlers)
        => handlers.AddBruinetAutomationHandlers(_ => { });

    /// <summary>
    /// Registers the automation handlers as <see cref="AddBruinetAutomationHandlers(IMauiHandlersCollection)"/>
    /// does, with choices for the Windows container peers.
    /// </summary>
    /// <example>
    /// <code>
    /// handlers.AddBruinetAutomationHandlers(options =&gt;
    /// {
    ///     options.PeersRequireAutomationId = true;
    ///     options.InvokeCommand = view =&gt; view is MyButton b &amp;&amp; b.Command is { } c
    ///         ? new AutomationCommand(c, b.CommandParameter) : null;
    /// });
    /// </code>
    /// </example>
    /// <param name="handlers">The handler collection from <c>ConfigureMauiHandlers</c>.</param>
    /// <param name="configure">Sets the peer options.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IMauiHandlersCollection AddBruinetAutomationHandlers(
        this IMauiHandlersCollection handlers,
        Action<BruinetAutomationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BruinetAutomationOptions();
        configure(options);
        BruinetAutomationOptions.Current = options;

#if WINDOWS
        // One registration against the base Layout type covers Grid, the stack
        // layouts, FlexLayout, and AbsoluteLayout.
        handlers.AddHandler<Layout, AutomationLayoutHandler>();
        handlers.AddHandler<ContentView, AutomationContentViewHandler>();
        handlers.AddHandler<Border, AutomationBorderHandler>();
        handlers.AddHandler<ContentPage, AutomationPageHandler>();
#endif

#if ANDROID
        // Slider and Stepper publish their range in the app's own units, on the node TalkBack
        // reads. Without it a Stepper publishes no value at all and a Slider only a raw fraction.
        RangeAccessibility.Register();
#endif

        return handlers;
    }

    /// <summary>
    /// Turns on the UI Automation gesture bridge, if this build has one and the harness asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bridge is a remotely invocable command channel into application logic</b>, and it
    /// is off unless two separate things say otherwise. UI Automation has no per-caller
    /// authentication: any process at the same or higher integrity level on the desktop can
    /// enumerate the tree, find the fragment root and call the pattern. There is nothing to
    /// authenticate and nobody to ask, so the only control that holds is that a shipping build
    /// has nothing to find.
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Compile time.</b> The provider and everything serving it are in the build only when
    /// <c>BRUINET_UIA_BRIDGE</c> is defined - Debug, or an explicit
    /// <c>-p:BruinetUiaBridge=true</c>. A Release build does not contain disabled bridge code;
    /// it contains no bridge code.
    /// </item>
    /// <item>
    /// <b>Run time.</b> This call is a no-op unless the environment variable
    /// <c>BRUINET_UIA_BRIDGE</c> is <c>1</c>, which the test harness sets on the app it
    /// launches. One build can then serve development and testing without the decision to
    /// instrument leaking into an ordinary run of the app.
    /// </item>
    /// </list>
    /// <para>
    /// <b>Safe to call unconditionally</b>, and meant to be: an app writes this line once and
    /// the two gates decide, rather than the app growing its own <c>#if</c> around it. In an
    /// uninstrumented build the method is still here and still does nothing, so the line
    /// compiles and reads the same in both.
    /// </para>
    /// <para>
    /// <b>Call it before the first page loads.</b> Elements publish themselves on
    /// <c>Loaded</c>, and one that loads before this runs finds the bridge off and stays
    /// unpublished. <c>CreateMauiApp</c> is early enough; a page constructor is not.
    /// </para>
    /// <example>
    /// <code>
    /// builder.UseBruinetGestureBridge();
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="builder">The app builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MauiAppBuilder UseBruinetGestureBridge(this MauiAppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The decision, its reasons and the variable's name go to the bridge log rather than
        // to a return value nobody would read. An app whose tests all fail with "nothing
        // answers" is the case this line exists for, and BRUINET_UIA_LOG is where that is read.
        BruinetBridgeHost.Enable();

        return builder;
    }
}
