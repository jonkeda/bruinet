using System.Runtime.InteropServices;

namespace Bruinet.FlaUI.Windowing;

/// <summary>
/// The Win32 calls the driver makes about the app's window, in one place.
/// </summary>
internal static class NativeMethods
{
    internal const int SmCxScreen = 0;
    internal const int SmCyScreen = 1;
    internal const int SmXVirtualScreen = 76;
    internal const int SmYVirtualScreen = 77;
    internal const int SmCxVirtualScreen = 78;
    internal const int SmCyVirtualScreen = 79;

    /// <summary>MONITORINFOF_PRIMARY.</summary>
    internal const uint MonitorInfoPrimary = 0x1;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;

    internal const int GwlExStyle = -20;
    internal const nint WsExNoActivate = 0x08000000;

    /// <summary>SPI_GETWORKAREA.</summary>
    internal const uint SpiGetWorkArea = 0x0030;

    /// <summary>HWND_BOTTOM: behind every other window, without being minimized.</summary>
    internal static readonly IntPtr HwndBottom = new(1);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal const uint WmKeyDown = 0x0100;
    internal const uint WmKeyUp = 0x0101;
    internal const uint WmChar = 0x0102;
    internal const uint WmLButtonDown = 0x0201;
    internal const uint WmLButtonUp = 0x0202;

    /// <summary>MK_LBUTTON: the left button is down.</summary>
    internal const uint MkLButton = 0x0001;

    /// <summary>SMTO_ABORTIFHUNG: a hung window fails the send instead of blocking it.</summary>
    internal const uint SmtoAbortIfHung = 0x0002;

    /// <summary>MAPVK_VK_TO_VSC.</summary>
    internal const uint MapVkToScanCode = 0;

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static extern nint SendMessageTimeout(
        IntPtr hwnd, uint message, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint result);

    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    internal static extern uint MapVirtualKey(uint code, uint mapType);

    [DllImport("user32.dll", EntryPoint = "VkKeyScanW")]
    internal static extern short VkKeyScan(char character);

    internal delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref NativeRect clip, nint data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint deviceContext, nint clip, MonitorEnumProc callback, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        out NativeRect pvParam,
        uint fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
