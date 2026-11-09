using System.Runtime.InteropServices;

namespace Bruinet.FlaUI.Windowing;

/// <summary>
/// Reads UI Automation coordinates in physical screen pixels, whatever the test process's DPI
/// awareness: the thread is made per-monitor aware for the read, so UI Automation does not scale
/// them to the process's DPI. A recorded video's frames are physical, and so are the bridge's
/// <c>GetBounds</c> answers.
/// </summary>
public static class PhysicalPixels
{
    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.
    private static readonly nint PerMonitorAwareV2 = -4;

    /// <summary>Runs <paramref name="read"/> with the thread per-monitor aware, then restores it.</summary>
    /// <typeparam name="T">What is read.</typeparam>
    /// <param name="read">The read; a bounding rectangle.</param>
    public static T Read<T>(Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            return read();
        }
        finally
        {
            if (previous != 0)
            {
                SetThreadDpiAwarenessContext(previous);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);
}
