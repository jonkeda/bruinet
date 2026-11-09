using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Bruinet.FlaUI;

/// <summary>
/// Captures a window's own rendered content, including while it is behind other windows.
/// </summary>
internal static class WindowCapture
{
    /// <summary>Render the full window content, including DWM-composed areas.</summary>
    private const uint PwRenderFullContent = 0x00000002;

    /// <summary>Samples per axis taken by <see cref="LooksBlank"/>.</summary>
    private const int BlankSampleGrid = 16;

    /// <summary>
    /// Captures the window's own content, or null when the window cannot render itself.
    /// </summary>
    /// <param name="windowHandle">The window to capture.</param>
    /// <returns>
    /// A bitmap the caller owns, or null when the handle is unusable, the window is minimized,
    /// or <c>PrintWindow</c> refused.
    /// </returns>
    internal static Bitmap? TryCapture(nint windowHandle)
    {
        if (windowHandle == 0 || !IsWindow(windowHandle) || IsIconic(windowHandle))
            return null;

        if (!GetWindowRect(windowHandle, out var bounds))
            return null;

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
            return null;

        // Format32bppRgb rather than Argb: PrintWindow does not write an alpha channel, so an
        // Argb bitmap saves as a fully transparent PNG — an image that looks blank without
        // being blank, and which LooksBlank would then misreport as a capture failure.
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);

        bool printed;
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var deviceContext = graphics.GetHdc();
            try
            {
                printed = PrintWindow(windowHandle, deviceContext, PwRenderFullContent);
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }
        }

        if (printed)
            return bitmap;

        bitmap.Dispose();
        return null;
    }

    /// <summary>
    /// Reports whether a capture came back empty, meaning the window declined to render.
    /// </summary>
    internal static bool LooksBlank(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        for (var row = 0; row < BlankSampleGrid; row++)
        {
            var y = bitmap.Height * row / BlankSampleGrid;

            for (var column = 0; column < BlankSampleGrid; column++)
            {
                var x = bitmap.Width * column / BlankSampleGrid;
                var pixel = bitmap.GetPixel(x, y);

                var isEmpty = pixel.A == 0 || (pixel.R == 0 && pixel.G == 0 && pixel.B == 0);
                if (!isEmpty)
                    return false;
            }
        }

        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out CaptureRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct CaptureRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
