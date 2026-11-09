namespace Bruinet.FlaUI.Windowing;

/// <summary>
/// The app's top-level window, as UI Automation hands it out - and handed out again when it goes stale.
/// </summary>
internal sealed class AppWindow
{
    private const int ElementNotAvailable = unchecked((int)0x80040201);

    private readonly UIA3Automation _automation;
    private readonly Lock _gate = new();
    private AutomationElement _element;
    private int _reattachments;

    internal AppWindow(UIA3Automation automation, AutomationElement element)
    {
        _automation = automation;
        _element = element;
        Handle = element.Properties.NativeWindowHandle.ValueOrDefault;
    }

    private AppWindow(UIA3Automation automation, AutomationElement element, IntPtr handle)
    {
        _automation = automation;
        _element = element;
        Handle = handle;
    }

    /// <summary>Attaches to a window by handle, waiting for UI Automation to resolve it.</summary>
    internal static AppWindow FromHandle(UIA3Automation automation, IntPtr windowHandle)
        => new(automation, Attach(automation, windowHandle), windowHandle);

    /// <summary>The native handle. Zero when UI Automation reported none.</summary>
    internal IntPtr Handle { get; }

    /// <summary>The window's element, re-attached by handle if UI Automation has retired it.</summary>
    internal AutomationElement Element
    {
        get
        {
            var element = _element;

            if (IsStillAvailable(element) || Handle == IntPtr.Zero)
            {
                return element;
            }

            lock (_gate)
            {
                if (!ReferenceEquals(element, _element) && IsStillAvailable(_element))
                {
                    return _element;
                }

                _element = Attach(_automation, Handle);
                Interlocked.Increment(ref _reattachments);
                return _element;
            }
        }
    }

    /// <summary>How many times the element had gone stale and was attached again.</summary>
    internal int Reattachments => Volatile.Read(ref _reattachments);

    private static bool IsStillAvailable(AutomationElement element)
    {
        try
        {
            _ = element.Properties.ProcessId.Value;
            return true;
        }
        catch (System.Runtime.InteropServices.COMException stale)
            when (stale.HResult == ElementNotAvailable)
        {
            return false;
        }
    }

    /// <summary>
    /// Attaches to a window, waiting for UI Automation to be willing to resolve it.
    /// </summary>
    private static AutomationElement Attach(UIA3Automation automation, IntPtr windowHandle)
    {
        const int timeoutMs = 10_000;
        const int pollMs = 20;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        Exception? last = null;

        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                return automation.FromHandle(windowHandle);
            }
            catch (Exception attaching)
            {
                last = attaching;
                Thread.Sleep(pollMs);
            }
        }

        throw new InvalidOperationException(
            $"UI Automation would not resolve window 0x{windowHandle:X} within {timeoutMs} ms. "
            + "The window exists but the automation stack will not hand it over, which is not "
            + "the same as the app being absent.",
            last);
    }
}
