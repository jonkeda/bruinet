using Bruinet.Uia;
#if BRUINET_UIA_BRIDGE
using Bruinet.Uia.Provider;
#endif
using Microsoft.Maui.Controls;

namespace Bruinet.Maui.AppSupport.Uia;

/// <summary>
/// One bridge per top-level window, created on demand and torn down with the window.
/// </summary>
/// <remarks>
/// <para>
/// Keyed by window handle rather than by MAUI's window object, so an app with several windows
/// gets one bridge each, and an element always reaches the bridge belonging to the window it is
/// actually in.
/// </para>
/// <para>
/// <b>Nothing here starts a bridge on its own.</b> The first element that declares a verb
/// creates one; an app with no declarations never registers a pattern, never creates a window,
/// and is indistinguishable from an app without the bridge compiled in.
/// </para>
/// <para>
/// <b>This is the seam the security gate runs through, and the one file carrying an
/// <c>#if</c>.</b> Everything the gate removes is removed from the build by
/// <c>Bruinet.Maui.AppSupport.csproj</c>; this file has to survive it, because
/// <see cref="GestureAutomation"/> calls into it and that has to compile in an app whose XAML
/// still names the attached property. What is left when the bridge is compiled out is these
/// signatures and nothing behind them.
/// </para>
/// <para>
/// <b>Two gates, and both must be open.</b> <c>BRUINET_UIA_BRIDGE</c> defined at compile time
/// puts the code in the build; <see cref="Enable"/> - reached from
/// <c>UseBruinetGestureBridge()</c> - turns it on at run time, and only when the environment
/// variable of the same name is set to <c>1</c>. Neither is a permission check, because UI
/// Automation offers nothing to check against: any process at the same integrity level on the
/// desktop can call a pattern it can find. The control is that there is nothing to find.
/// </para>
/// </remarks>
public static class BruinetBridgeHost
{
    private static bool _enabled;
    private static bool _refusalReported;

#if BRUINET_UIA_BRIDGE
    private static readonly Lock Gate = new();
    private static readonly Dictionary<IntPtr, BruinetUiaBridge> Bridges = [];

    /// <summary>Windows whose teardown is already subscribed, so it happens once.</summary>
    private static readonly HashSet<IntPtr> Watched = [];
#endif

    /// <summary>Whether any bridge exists in this process. Diagnostics and tests.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static bool IsActive
    {
        get
        {
#if BRUINET_UIA_BRIDGE
            lock (Gate)
            {
                return Bridges.Count > 0;
            }
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Turns the bridge on for this process, if this build has one and the harness asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The decision is <see cref="BruinetBridgeGate"/>'s, not this file's.</b> The same lines
    /// decide it in the bridge's own test host, which is what makes a test of the gate a test of
    /// this app's behaviour rather than of a lookalike.
    /// </para>
    /// <para>
    /// <b>Read once, deliberately.</b> The answer is settled at startup and does not change
    /// afterwards, so nothing can half-enable the bridge by setting the variable partway through
    /// a run.
    /// </para>
    /// </remarks>
    /// <returns>Whether the bridge is on, so the caller can say so.</returns>
    internal static bool Enable()
    {
        _enabled = BruinetBridgeGate.IsOpen;
        BridgeDiagnostics.Report(BruinetBridgeGate.Explain());

        return _enabled;
    }

    /// <summary>
    /// Publishes an element to its window's bridge, creating the bridge if it is the first.
    /// </summary>
    /// <param name="element">The element, which must be realised and have an AutomationId.</param>
    /// <param name="verbs">What the element answers.</param>
    /// <param name="sink">An optional sink that gets first refusal.</param>
    internal static void Publish(
        VisualElement element,
        IReadOnlyCollection<BruinetVerb> verbs,
        IBruinetGestureSink? sink)
    {
#if ANDROID
        // No bridge on Android: what the sink can say is published on the element's node instead (R2-T112).
        Bruinet.Maui.AppSupport.Accessibility.StateAccessibility.PublishSink(element, sink);
#endif

        if (!_enabled)
        {
            // Once, not once per element. A page declaring a dozen verbs would otherwise bury
            // the one line that explains the whole run, and the reader of this log is usually
            // someone whose tests all failed the same way.
            if (!_refusalReported)
            {
                _refusalReported = true;
                BridgeDiagnostics.Report(
                    $"'{element.AutomationId}' declares verbs but the bridge is off, so nothing "
                    + "will be published for the life of this process. Call "
                    + $"UseBruinetGestureBridge() during startup and set {BruinetBridgeGate.EnableVariable}=1.");
            }

            return;
        }

        // The platform check the analyzer needs, and the one a reader needs too: everything
        // below this line is UI Automation, which exists only on Windows. Off Windows the
        // declaration is inert and gestures are real touch input.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

#if BRUINET_UIA_BRIDGE
        var hwnd = WindowHandles.RootOf(element);
        if (hwnd == IntPtr.Zero)
        {
            BridgeDiagnostics.Report(
                $"'{element.AutomationId}' declares verbs but its window could not be found, "
                + "so it was not published. The element is not realised on this platform.");
            return;
        }

        try
        {
            // Resolved here, once, rather than per call. Everything it looks at - the element's
            // type, its recognizers, the verbs the sink names - is fixed by the time the element
            // is on screen, so the dispatch path holds a lookup and nothing else.
            var plan = VerbBindings.Resolve(element, verbs, sink);

            foreach (var refusal in plan.Refusals)
            {
                // The whole reason resolution moved here. A verb that binds to nothing used to
                // be discovered by a test, as UIA_E_NOTSUPPORTED - which is also what a typo
                // looks like, and what a verb this build has not implemented looks like. Said
                // once, at startup, in the app author's terms, it is a markup bug with an
                // address.
                BridgeDiagnostics.Report(
                    $"'{element.AutomationId}' [{element.GetType().Name}] declares {refusal.Verb} "
                    + $"but nothing can perform it: {refusal.Reason}. The verb was not published.");
            }

            if (plan.Capabilities.Count == 0)
            {
                BridgeDiagnostics.Report(
                    $"'{element.AutomationId}' declared {verbs.Count} verb(s) and bound none, so "
                    + "it was not published. See the refusals above.");
                return;
            }

            BridgeDiagnostics.Report(
                $"publishing '{element.AutomationId}' ({string.Join(",", plan.Capabilities)}) "
                + $"on window 0x{hwnd:X}");

            var bridge = GetOrCreate(hwnd);
            BridgeDiagnostics.Report("bridge ready");

            // Withdrawn first: an element loaded again must not be published twice. Other
            // elements sharing its AutomationId stay published.
            bridge.Unregister(
                element.AutomationId, target => target is MauiVerbTarget mine && mine.Owns(element));
            bridge.Register(new MauiVerbTarget(element, element.AutomationId, plan));
            BridgeDiagnostics.Report($"registered '{element.AutomationId}'");

            WatchForClose(element, hwnd);
            BridgeDiagnostics.Report("teardown wired");
        }
        catch (Exception ex)
        {
            // Every exception, not only BruinetUiaException. This runs inside the app's Loaded
            // handler, so anything that escapes takes the app under test down with it - and an
            // app that will not start is a far worse outcome than an app without instrumentation.
            // Test-only code must never be able to break the thing it is meant to observe.
            BridgeDiagnostics.Report($"could not publish '{element.AutomationId}': {ex}");
        }
#endif
    }

    /// <summary>Removes an element from its window's bridge.</summary>
    /// <param name="element">The element being unloaded.</param>
    internal static void Withdraw(VisualElement element)
    {
#if BRUINET_UIA_BRIDGE
        var automationId = element.AutomationId;
        if (string.IsNullOrWhiteSpace(automationId))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Deliberately not resolved through the element: by Unloaded its handler may already be
        // gone, so the window handle is no longer reachable from it. Sweeping every bridge is
        // cheap - there is one per window - and correct in the case that matters.
        lock (Gate)
        {
            foreach (var bridge in Bridges.Values)
            {
                // Only if this element is still the one registered. MAUI loads the incoming page
                // before unloading the outgoing one, so by the time this runs the same
                // AutomationId may already belong to the new page's element - and removing it
                // would leave the bridge empty for every visit after the first.
                var removed = bridge.Unregister(
                    automationId, target => target is MauiVerbTarget mine && mine.Owns(element));

                BridgeDiagnostics.Report($"withdraw '{automationId}' -> {removed}");
            }
        }
#endif
    }

    /// <summary>
    /// Disposes every bridge in this process.
    /// </summary>
    /// <remarks>
    /// For an app that wants to shut instrumentation down explicitly, and for tests of the
    /// bridge's own lifetime. Ordinary teardown happens when each window closes.
    /// </remarks>
    public static void Shutdown()
    {
#if BRUINET_UIA_BRIDGE
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (Gate)
        {
            foreach (var bridge in Bridges.Values)
            {
                bridge.Dispose();
            }

            Bridges.Clear();
            Watched.Clear();
        }
#endif
    }

#if BRUINET_UIA_BRIDGE

    /// <summary>
    /// Arranges for the window's bridge to be disposed when the window goes away.
    /// </summary>
    /// <remarks>
    /// Subscribed once per window, tracked by the same handle the bridge is keyed on.
    /// <c>Destroying</c> rather than the platform window's <c>Closed</c>, because by the time
    /// the native window is closing the element that led us here may have no handler left and
    /// there would be no way back to the MAUI window at all.
    /// </remarks>
    private static void WatchForClose(VisualElement element, IntPtr hwnd)
    {
        lock (Gate)
        {
            if (!Watched.Add(hwnd))
            {
                return;
            }
        }

        if (element.Window is not { } window)
        {
            // No MAUI window to hang the teardown off. The bridge still dies with the process,
            // and Shutdown remains available; what is lost is only the per-window cleanup.
            lock (Gate)
            {
                Watched.Remove(hwnd);
            }

            return;
        }

        window.Destroying += OnWindowDestroying;
        return;

        void OnWindowDestroying(object? sender, EventArgs e)
        {
            window.Destroying -= OnWindowDestroying;

            lock (Gate)
            {
                Watched.Remove(hwnd);
            }

            Close(hwnd);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static BruinetUiaBridge GetOrCreate(IntPtr hwnd)
    {
        lock (Gate)
        {
            // Not merely "is it in the dictionary". A bridge whose window has gone clears its
            // own handle, and it can go without this dictionary hearing about it: teardown is
            // hung off the MAUI window's Destroying event, and Windows destroys child windows
            // with their parent whether or not that event ever fires. A stale entry here would
            // be handed back for a handle Windows has since reissued, and Register on a
            // disposed bridge throws - reaching the app as "could not publish", which names
            // neither the window nor the reason.
            if (Bridges.TryGetValue(hwnd, out var existing) && existing.Handle != IntPtr.Zero)
            {
                return existing;
            }

            var bridge = BruinetUiaBridge.Attach(hwnd);
            Bridges[hwnd] = bridge;
            return bridge;
        }
    }

    /// <summary>
    /// Tears down the bridge for a window that has closed.
    /// </summary>
    /// <remarks>
    /// Called from the platform window's <c>Closed</c> event. Skipping it leaks a provider, and
    /// a leaked provider is not a quiet leak: UI Automation caches provider pointers across
    /// processes and a client holding a stale one blocks until its transaction times out.
    /// </remarks>
    internal static void Close(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (Gate)
        {
            if (Bridges.Remove(hwnd, out var bridge))
            {
                bridge.Dispose();
            }
        }
    }
#endif
}
