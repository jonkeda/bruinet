using System.Runtime.InteropServices;
using Bruinet.Uia.Interop;

namespace Bruinet.Uia.Provider;

/// <summary>
/// The window that carries the Bruinet UI Automation provider, and the set of targets beneath
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a window at all.</b> A custom UI Automation pattern can only be answered by a native
/// <c>IRawElementProviderSimple</c>, and the only supported way to get one into the tree is to
/// return it from <c>WM_GETOBJECT</c>. XAML's <c>AutomationPeer.GetPatternCore</c> takes a
/// closed enum and cannot express a custom pattern at all, so there is no route through the
/// UI framework - the provider has to hang off an HWND of our own. This one is one pixel,
/// disabled, transparent to input, and draws nothing.
/// </para>
/// <para>
/// <b>It does not touch the host's automation tree.</b> No handler is replaced and no
/// automation peer is overridden. That is the whole point of the design: overriding the peer
/// of a WinUI control was measured to collapse the entire tree for the app under test, leaving
/// every element unaddressable while the app kept rendering normally.
/// </para>
/// <para>
/// <b>Threading.</b> Create and dispose on the thread that owns the parent window. The window
/// procedure runs on that thread's message loop, so a bridge created on a thread with no pump
/// would never answer <c>WM_GETOBJECT</c> and would look, from the client, exactly like an app
/// with no instrumentation.
/// </para>
/// </remarks>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class BruinetUiaBridge : IDisposable
{
    private static readonly Lock ClassGate = new();
    private static bool _classRegistered;

    /// <summary>
    /// Held for the life of the process, and for the same reason the class registration is.
    /// </summary>
    private static Win32.WndProc? _classWndProc;

    /// <summary>Counted by <see cref="Disconnect"/>; read through <see cref="DisconnectFailures"/>.</summary>
    private static int _disconnectFailures;

    /// <summary>Recorded by <see cref="Disconnect"/>; read through <see cref="LastDisconnectHResult"/>.</summary>
    private static int _lastDisconnectHResult;

    private readonly BridgeFragmentRoot _root;
    private readonly Lock _gate = new();
    private readonly uint _ownerThreadId;

    private readonly List<BridgeTargetProvider> _targets = [];

    private IntPtr _hwnd;
    private int _nextRuntimeId = 1;
    private bool _disposed;

    private BruinetUiaBridge(IntPtr hwnd, BridgeFragmentRoot root, uint ownerThreadId)
    {
        _hwnd = hwnd;
        _root = root;
        _ownerThreadId = ownerThreadId;
    }

    /// <summary>The bridge window. Diagnostics only.</summary>
    public IntPtr Handle => _hwnd;

    /// <summary>
    /// Creates a bridge as a child of the given window.
    /// </summary>
    /// <remarks>
    /// Call on the thread that owns <paramref name="parentHwnd"/>.
    /// </remarks>
    /// <param name="parentHwnd">The app window to attach to.</param>
    /// <returns>The bridge, which the caller owns and must dispose.</returns>
    /// <exception cref="BruinetUiaException">The window class or the window could not be created.</exception>
    public static BruinetUiaBridge Attach(IntPtr parentHwnd)
    {
        if (parentHwnd == IntPtr.Zero || !Win32.IsWindow(parentHwnd))
        {
            throw new BruinetUiaException(
                $"Cannot attach a Bruinet bridge to 0x{parentHwnd:X}: not a window.");
        }

        // Registering the pattern here rather than lazily means a misconfigured contract fails
        // at startup, where it is one clear error, instead of on the first verb in the middle
        // of a test run.
        _ = BruinetPatternRegistration.Current;

        EnsureWindowClass();

        var hwnd = Win32.CreateWindowEx(
            exStyle: Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT,
            className: BruinetUiaIds.BridgeClassName,
            windowName: BruinetUiaIds.BridgeClassName,

            // Visible, because an invisible window is excluded from the automation tree and
            // the whole point is to be found. Disabled and transparent, because being found is
            // all it is for - it must never take input, focus or a hit test.
            style: Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_DISABLED,
            x: 0,
            y: 0,
            width: 1,
            height: 1,
            parent: parentHwnd,
            menu: IntPtr.Zero,
            instance: Win32.GetModuleHandle(null),
            param: IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            throw new BruinetUiaException(
                "Could not create the Bruinet bridge window "
                + $"(Win32 error {Marshal.GetLastWin32Error()}).");
        }

        var root = new BridgeFragmentRoot(hwnd);
        var bridge = new BruinetUiaBridge(hwnd, root, Win32.GetCurrentThreadId());

        Registry.Add(hwnd, bridge);
        return bridge;
    }

    /// <summary>
    /// Publishes a target alongside any others with the same <c>AutomationId</c>.
    /// </summary>
    /// <remarks>
    /// Added, not replaced: the same template can be used more than once on a page, so one
    /// <c>AutomationId</c> can stand for several live elements. A client tells them apart with
    /// <see cref="BruinetVerb.GetBounds"/>. A host that publishes the same element again must
    /// first withdraw it with <see cref="Unregister(string, Func{IBruinetVerbTarget, bool})"/>.
    /// </remarks>
    /// <param name="target">The target to publish.</param>
    public void Register(IBruinetVerbTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            _targets.Add(new BridgeTargetProvider(_root, target, _nextRuntimeId++));
            PublishChildren();
        }
    }

    /// <summary>Removes every target published under an <c>AutomationId</c>.</summary>
    /// <param name="automationId">The <c>AutomationId</c> the targets were published under.</param>
    /// <returns>Whether anything was removed.</returns>
    public bool Unregister(string automationId) => Unregister(automationId, _ => true);

    /// <summary>
    /// Removes the targets under an <c>AutomationId</c> that satisfy
    /// <paramref name="stillTheSameTarget"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This exists because of navigation order.</b> A UI framework generally loads the
    /// incoming page before it unloads the outgoing one, so an element that registers on load
    /// and unregisters on unload produces: new element registers, old element unregisters - and
    /// an unregister by <c>AutomationId</c> alone would remove the newcomer too. The result is a
    /// bridge that works on the first visit to a page and is empty on every visit after, which
    /// reads as an intermittent fault rather than an ordering one.
    /// </para>
    /// <para>
    /// So an unregister has to prove it is removing its own registration, not its successor's.
    /// </para>
    /// </remarks>
    /// <param name="automationId">The <c>AutomationId</c> the target was published under.</param>
    /// <param name="stillTheSameTarget">
    /// Given a published target, whether it is one the caller means to remove.
    /// </param>
    /// <returns>Whether anything was removed.</returns>
    public bool Unregister(string automationId, Func<IBruinetVerbTarget, bool> stillTheSameTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(stillTheSameTarget);

        if (_disposed)
        {
            return false;
        }

        lock (_gate)
        {
            var removed = _targets.FindAll(published =>
                string.Equals(published.Target.AutomationId, automationId, StringComparison.Ordinal)
                && stillTheSameTarget(published.Target));

            if (removed.Count == 0)
            {
                return false;
            }

            foreach (var published in removed)
            {
                _targets.Remove(published);

                // RETIRED, NOT DISCONNECTED - and this is the root cause of the "app freeze".
                //
                // UiaDisconnectProvider was called here, on every page unload. Measured under a
                // stress walk of 150 page changes: with it, UI Automation retired the client's
                // element for the app's top-level window seven or eight times - every later lookup
                // through that element answered UIA_E_ELEMENTNOTAVAILABLE and the driver, which
                // held it for the whole run, went blind. Without it: zero. A disconnect evidently
                // invalidates more than the one provider it names.
                //
                // What the disconnect was for still matters, so it is kept where it matters: when
                // the bridge itself goes, a provider left connected to a dead window is what hangs
                // clients (step 28), and Teardown still disconnects. A target withdrawn while its
                // window lives is a different case - its provider is still hosted and still
                // answers, so retiring it makes a stale reference fail promptly without touching
                // UI Automation's connection.
                published.Retire();
            }

            PublishChildren();
            return true;
        }
    }

    /// <summary>The <c>AutomationId</c>s currently published, once per target.</summary>
    public IReadOnlyCollection<string> RegisteredTargets
    {
        get
        {
            lock (_gate)
            {
                return _targets.Select(t => t.Target.AutomationId).ToArray();
            }
        }
    }

    /// <summary>
    /// Disconnects every provider and destroys the window.
    /// </summary>
    /// <remarks>
    /// Order matters and the disconnect is not optional. UI Automation caches provider pointers
    /// across the process boundary, and a client left holding one blocks until its transaction
    /// times out rather than failing - so a leaked provider stalls every accessibility client
    /// on the desktop, not only the test that caused it.
    /// </remarks>
    public void Dispose() => Teardown(destroyWindow: true);

    /// <summary>
    /// How many bridges are live in this process. Diagnostics and lifetime tests.
    /// </summary>
    /// <remarks>
    /// <b>A leaked provider is not a quiet leak.</b> UI Automation caches provider pointers
    /// across the process boundary, and a client holding a stale one blocks until its
    /// transaction times out rather than failing - so this number failing to come back down is
    /// a denial of service against every accessibility client on the desktop, not only against
    /// the test that caused it. It is exposed so a soak test can watch it rather than infer it.
    /// </remarks>
    public static int ActiveCount => Registry.Count;

    /// <summary>
    /// How many times <c>UiaDisconnectProvider</c> has refused, process-wide.
    /// </summary>
    /// <remarks>
    /// Should be zero. Anything else means providers were left connected during a teardown that
    /// reported no trouble, which is the failure the disconnect exists to prevent and the one
    /// that used to be invisible.
    /// </remarks>
    public static int DisconnectFailures => Volatile.Read(ref _disconnectFailures);

    /// <summary>What <c>UiaDisconnectProvider</c> returned last.</summary>
    /// <remarks>
    /// The count says a disconnect failed; this says why, which is the difference between
    /// "UI Automation does not track this provider" and "the call was made too late".
    /// </remarks>
    public static int LastDisconnectHResult => Volatile.Read(ref _lastDisconnectHResult);

    /// <summary>
    /// Tears the bridge down because its window has already been destroyed.
    /// </summary>
    private void OnWindowDestroyed() => Teardown(destroyWindow: false);

    /// <param name="destroyWindow">
    /// False when the window is already being destroyed and this is the notification.
    /// </param>
    private void Teardown(bool destroyWindow)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var provider in _targets)
            {
                Disconnect(provider);
            }

            _targets.Clear();
            _root.SetChildren([]);
            Disconnect(_root);

            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            Registry.Remove(_hwnd);

            // Destroying a window from a thread that does not own it is a no-op that
            // reports success, which would leave the window and its provider alive with
            // nothing referencing them.
            if (destroyWindow && Win32.GetCurrentThreadId() == _ownerThreadId)
            {
                Win32.DestroyWindow(_hwnd);
            }

            _hwnd = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Severs a provider from UI Automation, counting the times it could not be done.
    /// </summary>
    private static void Disconnect(IRawElementProviderSimple provider)
    {
        try
        {
            var hr = UiaNativeMethods.UiaDisconnectProvider(provider);
            Volatile.Write(ref _lastDisconnectHResult, hr);

            if (hr < 0)
            {
                Interlocked.Increment(ref _disconnectFailures);
            }
        }
        catch (COMException)
        {
            // Already gone, or no client ever connected. Either way there is nothing to clean.
        }
    }

    /// <summary>Rebuilds the root's child snapshot and tells any listening client.</summary>
    private void PublishChildren()
    {
        _root.SetChildren([.. _targets]);

        if (!UiaNativeMethods.UiaClientsAreListening())
        {
            return;
        }

        try
        {
            _ = UiaNativeMethods.UiaRaiseStructureChangedEvent(
                _root, StructureChangeType.ChildrenInvalidated, null, 0);
        }
        catch (COMException)
        {
            // An event nobody receives is not a reason to fail a registration.
        }
    }

    private static void EnsureWindowClass()
    {
        lock (ClassGate)
        {
            if (_classRegistered)
            {
                return;
            }

            _classWndProc = WindowProcedure;

            var windowClass = new Win32.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_classWndProc),
                hInstance = Win32.GetModuleHandle(null),
                lpszClassName = BruinetUiaIds.BridgeClassName,
            };

            if (Win32.RegisterClassEx(ref windowClass) != 0)
            {
                _classRegistered = true;
                return;
            }

            var error = Marshal.GetLastWin32Error();

            // Already registered by an earlier load of these sources in this process - which is
            // expected, since the app under test and any library carrying the bridge compile
            // the same file.
            if (error == Win32.ERROR_CLASS_ALREADY_EXISTS)
            {
                _classRegistered = true;
                return;
            }

            _classWndProc = null;
            throw new BruinetUiaException(
                $"Could not register the Bruinet bridge window class (Win32 error {error}).");
        }
    }

    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == Win32.WM_GETOBJECT
            && (int)(long)lParam == Win32.UiaRootObjectId
            && Registry.TryGet(hwnd, out var bridge))
        {
            return UiaNativeMethods.UiaReturnRawElementProvider(hwnd, wParam, lParam, bridge!._root);
        }

        // However the window died - disposed, parent closed, or the app torn down around it -
        // this is the notification that arrives. Disconnecting here rather than only in Dispose
        // is what stops a closed window leaving live providers behind it.
        if (message == Win32.WM_DESTROY && Registry.TryGet(hwnd, out var closing))
        {
            closing!.OnWindowDestroyed();
        }

        return Win32.DefWindowProc(hwnd, message, wParam, lParam);
    }

    /// <summary>
    /// Maps a window back to its bridge.
    /// </summary>
    private static class Registry
    {
        private static readonly Lock Gate = new();
        private static readonly Dictionary<IntPtr, BruinetUiaBridge> Bridges = [];

        internal static void Add(IntPtr hwnd, BruinetUiaBridge bridge)
        {
            lock (Gate)
            {
                Bridges[hwnd] = bridge;
            }
        }

        internal static void Remove(IntPtr hwnd)
        {
            lock (Gate)
            {
                Bridges.Remove(hwnd);
            }
        }

        internal static int Count
        {
            get
            {
                lock (Gate)
                {
                    return Bridges.Count;
                }
            }
        }

        internal static bool TryGet(IntPtr hwnd, out BruinetUiaBridge? bridge)
        {
            lock (Gate)
            {
                return Bridges.TryGetValue(hwnd, out bridge);
            }
        }
    }
}
