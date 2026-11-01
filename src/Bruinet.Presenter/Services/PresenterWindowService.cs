using System.Drawing;
using System.Runtime.InteropServices;

namespace Bruinet.Presenter.Services;

/// <summary>
/// Docks the Presenter window, and shows the app under test by its top-level window: the one way
/// that works for every target, since a fixture starts its app (or its browser) in a process of
/// its own. The launchers only leave the window shown (<c>BRUINET_AUT_WINDOW=shown</c>).
/// </summary>
/// <param name="window">The Presenter's window, once it exists.</param>
public sealed class PresenterWindowService(Func<Window?> window) : IPresenterWindowService
{
    private const int MinimumWidth = 200;
    private const int MinimumHeight = 150;

    private int _appProcess;

    /// <inheritdoc />
    public void Dock(PresenterDock dock)
    {
#if WINDOWS
        if (Handle() is not { } handle || handle == IntPtr.Zero || WorkArea(handle) is not { } workArea)
        {
            return;
        }

        var bar = PresenterLayout.Bar(workArea, dock);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(bar.X, bar.Y, bar.Width, bar.Height));
#endif
    }

    /// <inheritdoc />
    public Rectangle? TargetBounds(PresenterAppWindow mode, PresenterDock dock)
    {
#if WINDOWS
        if (Handle() is not { } own || own == IntPtr.Zero || WorkArea(own) is not { } workArea)
        {
            return null;
        }

        return mode switch
        {
            PresenterAppWindow.BesideBar => PresenterLayout.App(workArea, Bounds(own), dock),
            PresenterAppWindow.Maximized => workArea,
            PresenterAppWindow.MaximizedOtherMonitor => PresenterLayout.OtherMonitor(WorkAreas(), workArea) ?? PresenterLayout.App(workArea, Bounds(own), dock),
            _ => null,
        };
#else
        return null;
#endif
    }

    /// <inheritdoc />
    public object Snapshot() => new HashSet<IntPtr>(TopLevelWindows().Select(entry => entry.Handle));

    /// <inheritdoc />
    public string? PlaceApp(object before, PresenterDock dock, PresenterAppWindow mode)
    {
#if WINDOWS
        if (Handle() is not { } own || own == IntPtr.Zero || WorkArea(own) is not { } workArea)
        {
            return "AUT placement:" + Environment.NewLine + "Result: not supported";
        }

        var known = before as HashSet<IntPtr> ?? [];
        var ownProcess = Environment.ProcessId;
        var candidate = TopLevelWindows()
            .Where(entry => !known.Contains(entry.Handle) && entry.ProcessId != ownProcess)
            .Where(entry => entry.Bounds.Width >= MinimumWidth && entry.Bounds.Height >= MinimumHeight)
            .OrderByDescending(entry => entry.Bounds.Width * entry.Bounds.Height)
            .FirstOrDefault();
        if (candidate.Handle == IntPtr.Zero)
        {
            return null;
        }

        _appProcess = candidate.ProcessId;
        var bar = Bounds(own);
        var other = mode == PresenterAppWindow.MaximizedOtherMonitor ? PresenterLayout.OtherMonitor(WorkAreas(), workArea) : null;
        if (mode == PresenterAppWindow.MaximizedOtherMonitor && other is null)
        {
            // One monitor: beside the bar instead.
            mode = PresenterAppWindow.BesideBar;
        }

        bool moved;
        Rectangle? target = null;
        switch (mode)
        {
            case PresenterAppWindow.Background:
                moved = SetWindowPos(candidate.Handle, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
                break;
            case PresenterAppWindow.WhereItOpens:
                moved = SetWindowPos(candidate.Handle, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
                break;
            case PresenterAppWindow.Maximized:
            case PresenterAppWindow.MaximizedOtherMonitor:
                // Filled rather than maximized: a real maximize activates the window, and the keys
                // would leave Presenter.
                target = other ?? workArea;
                Restore(candidate.Handle);
                moved = SetWindowPos(candidate.Handle, HwndTop, target.Value.X, target.Value.Y, target.Value.Width, target.Value.Height, SwpNoActivate);
                break;
            default:
                target = PresenterLayout.App(workArea, bar, dock);
                Restore(candidate.Handle);
                moved = SetWindowPos(candidate.Handle, HwndTop, target.Value.X, target.Value.Y, target.Value.Width, target.Value.Height, SwpNoActivate);
                break;
        }

        ReclaimFocus();
        return string.Join(
            Environment.NewLine,
            "AUT placement:",
            $"Placement: {mode}",
            $"Presenter bounds: {Format(bar)}",
            $"Requested AUT bounds: {(target is { } requested ? Format(requested) : "where it opened")}",
            $"Result: {(moved ? "moved" : "failed")}",
            $"Window: {candidate.Title}");
#else
        return "AUT placement:" + Environment.NewLine + "Result: not supported";
#endif
    }

    /// <inheritdoc />
    public void ReclaimFocus()
    {
#if WINDOWS
        // Only from the app it showed: a person who went to another window keeps it.
        if (Handle() is { } own && own != IntPtr.Zero && _appProcess != 0
            && GetWindowThreadProcessId(GetForegroundWindow(), out var foreground) != 0 && foreground == _appProcess)
        {
            SetForegroundWindow(own);
        }
#endif
    }

    /// <inheritdoc />
    public void SetBarTopmost(bool topmost)
    {
#if WINDOWS
        if (Handle() is { } own && own != IntPtr.Zero)
        {
            SetWindowPos(own, topmost ? HwndTopmost : HwndNoTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
#endif
    }

    private static string Format(Rectangle rectangle)
        => $"x={rectangle.X} y={rectangle.Y} w={rectangle.Width} h={rectangle.Height}";

#if WINDOWS
    private const int SwRestore = 9;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint GwOwner = 4;
    private const uint MonitorDefaultToNearest = 2;

    private static Rectangle Bounds(IntPtr handle)
    {
        GetWindowRect(handle, out var rect);
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static void Restore(IntPtr handle)
    {
        if (IsZoomed(handle))
        {
            ShowWindow(handle, SwRestore);
        }
    }

    private static List<Rectangle> WorkAreas()
    {
        List<Rectangle> areas = [];
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                areas.Add(Rectangle.FromLTRB(info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Right, info.WorkArea.Bottom));
            }

            return true;
        }, IntPtr.Zero);
        return areas;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    private IntPtr? Handle()
        => window()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window platform
            ? WinRT.Interop.WindowNative.GetWindowHandle(platform)
            : null;

    private static Rectangle? WorkArea(IntPtr handle)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info)
            ? Rectangle.FromLTRB(info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Right, info.WorkArea.Bottom)
            : null;
    }

    // Visible, unowned, titled windows: an app's main window, not its tooltips or popups.
    private static List<(IntPtr Handle, int ProcessId, Rectangle Bounds, string Title)> TopLevelWindows()
    {
        List<(IntPtr, int, Rectangle, string)> windows = [];
        EnumWindows((handle, lParam) =>
        {
            var length = GetWindowTextLength(handle);
            if (IsWindowVisible(handle) && GetWindow(handle, GwOwner) == IntPtr.Zero && length > 0 && GetWindowRect(handle, out var rect))
            {
                var title = new char[length + 1];
                var read = GetWindowText(handle, title, title.Length);
                _ = GetWindowThreadProcessId(handle, out var processId);
                windows.Add((handle, (int)processId, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), new string(title, 0, read)));
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, uint command);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, [Out] char[] text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
#else
    private static List<(IntPtr Handle, int ProcessId, Rectangle Bounds, string Title)> TopLevelWindows() => [];
#endif
}
