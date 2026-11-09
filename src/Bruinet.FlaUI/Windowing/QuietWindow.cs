using static Bruinet.FlaUI.Windowing.NativeMethods;

namespace Bruinet.FlaUI.Windowing;

/// <summary>
/// Keeps the app under test behind whatever the person at the machine is using.
/// </summary>
internal sealed class QuietWindow : IDisposable
{
    private readonly int _processId;
    private readonly IntPtr _previousForeground;
    private readonly CancellationTokenSource? _watchdog;
    private int _foregroundGrabs;

    private QuietWindow(int processId, IntPtr previousForeground)
    {
        _processId = processId;
        _previousForeground = previousForeground;

        _watchdog = new CancellationTokenSource();
        var token = _watchdog.Token;
        new Thread(() => WatchTheForeground(token))
        {
            IsBackground = true,
            Name = "Bruinet foreground watchdog",
        }.Start();
    }

    /// <summary>
    /// Starts watching a freshly launched app. Call it before anything waits on the app.
    /// </summary>
    /// <param name="processId">The app under test, whose windows are watched.</param>
    /// <param name="previousForeground">The window the person was using, to hand back.</param>
    internal static QuietWindow ForLaunch(int processId, IntPtr previousForeground)
        => new(processId, previousForeground);

    /// <summary>
    /// How many times the app under test took the foreground and had to be put back.
    /// </summary>
    internal int ForegroundGrabs => Volatile.Read(ref _foregroundGrabs);

    /// <summary>
    /// Settles the main window once it is known: refuses activation, sends it behind, and hands
    /// the foreground back.
    /// </summary>
    internal void Settle(IntPtr mainWindow)
    {
        RefuseActivation(mainWindow);
        SendBehind(mainWindow);
        RestorePreviousForeground();
    }

    /// <summary>
    /// Marks the app's window as one that does not become the foreground window.
    /// </summary>
    private static void RefuseActivation(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var style = GetWindowLongPtr(window, GwlExStyle);
            SetWindowLongPtr(window, GwlExStyle, style | WsExNoActivate);
        }
        catch
        {
            // Out of the way is a courtesy; the watchdog still pushes the window back if this
            // could not be applied.
        }
    }

    /// <summary>
    /// Puts a window at the bottom of the z-order without activating it.
    /// </summary>
    private static void SendBehind(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetWindowPos(window, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
        catch
        {
            // Out of the way is a courtesy, not a requirement. It changes no test outcome.
        }
    }

    /// <summary>
    /// Hands the foreground back to the window that had it before the app was launched.
    /// </summary>
    private void RestorePreviousForeground()
    {
        if (_previousForeground == IntPtr.Zero)
        {
            return;
        }

        try
        {
            SetForegroundWindow(_previousForeground);
        }
        catch
        {
            // Losing this race only means the app keeps focus; it changes no test outcome.
        }
    }

    private void WatchTheForeground(CancellationToken cancellation)
    {
        const int eagerMs = 5;
        const int settledMs = 100;
        const int eagerForMs = 15_000;

        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var foreground = GetForegroundWindow();

                if (foreground != IntPtr.Zero
                    && GetWindowThreadProcessId(foreground, out var owner) != 0
                    && owner == _processId)
                {
                    Interlocked.Increment(ref _foregroundGrabs);
                    SendBehind(foreground);
                    RestorePreviousForeground();
                }
            }
            catch
            {
                // The app has gone, or the handle is stale. Neither is worth taking a test run
                // down over: this is a courtesy thread.
            }

            Thread.Sleep(clock.ElapsedMilliseconds < eagerForMs ? eagerMs : settledMs);
        }
    }

    public void Dispose() => _watchdog?.Cancel();
}
