using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Bruinet.Maui.UITests.Pages;
using Xunit.Abstractions;

// Bruinet.Maui's global usings bring in Microsoft.Maui.Graphics, which has its own Color.
using DrawingColor = System.Drawing.Color;

namespace Bruinet.Maui.UITests.Tests.Diagnostics;

/// <summary>
/// Proves a screenshot is of the app under test even when the app is behind another window.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this guards.</b> FlaUI's <c>Capture.Element</c> reads the screen at the element's
/// bounding rectangle, so an occluded app screenshots as whatever is on top. Bruinet's own
/// guidance is to inspect screenshots first when a UI test fails, which makes a
/// confidently-wrong screenshot worse than a missing one. The driver now asks the window to
/// render itself instead; this test is the evidence that it works.
/// </para>
/// <para>
/// <b>Windows only, and excluded from the mobile head.</b> Every other file under
/// <c>Bruinet.Maui.UITests</c> is linked into <c>Bruinet.Maui.UITests.Mobile</c> and compiled
/// against Appium on plain <c>net10.0</c>. This one needs <c>System.Drawing</c> and user32, so
/// it is excluded by name in that project rather than being quietly platform-guarded.
/// </para>
/// <para>
/// <b>The occluder is checked, not assumed.</b> If the covering window failed to appear, every
/// other assertion here would pass while proving nothing, so
/// <see cref="Screenshot_OfOccludedWindow_ShowsTheApp"/> asserts the screen really did go dark
/// before it draws any conclusion from the capture.
/// </para>
/// </remarks>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Diagnostics")]
public class OccludedScreenshotTests
{
    /// <summary>Fraction of sampled pixels that must match for two captures to be the same image.</summary>
    private const double SameImageThreshold = 0.95;

    /// <summary>Per-channel tolerance, absorbing compression and sub-pixel rendering noise.</summary>
    private const int ChannelTolerance = 8;

    /// <summary>Samples per axis used to compare two captures.</summary>
    private const int ComparisonGrid = 64;

    private readonly MauiFixture _fixture;
    private readonly ITestOutputHelper _output;

    public OccludedScreenshotTests(MauiFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// A screenshot taken while the app is fully covered still shows the app.
    /// </summary>
    [DeviceFact("it needs pixels")]
    public void Screenshot_OfOccludedWindow_ShowsTheApp()
    {
        // Two waits, and both are load-bearing. NavigateToButtons only clicks — it does not wait
        // for the page it opens — so without the marker the captures race the navigation and
        // compare two different pages. And a marker appearing in the UIA tree does not mean the
        // frame is painted, which for a test about pixels is the state that actually matters.
        var page = _fixture.NavigateToButtons();
        Assert.True(
            page.StatusLabel.WaitExists(),
            "The page did not open, so the captures below would be racing navigation.");

        // A third wait, and it is the one the other two cannot cover: let the page finish
        // painting before anything is captured, and throw the result away.
        //
        // CaptureWhenSettled gives up waiting after its grace period if nothing has changed, and
        // an unpainted page is perfectly stable - so a capture taken soon enough after navigation
        // returns chrome with no content, twice, in agreement. Doing it here means the first
        // frame this samples is the unpainted one, so the paint that follows registers as a
        // change and the settle waits for it properly.
        //
        // This became necessary when the suite got faster: stage B replaced the toolbar click in
        // ReturnToHub with a bridge call, navigation stopped costing seconds, and the captures
        // moved inside the window where the page is realised but not yet drawn. The test was
        // relying on incidental slowness, which is the kind of dependency that only shows up when
        // something unrelated improves.
        CaptureWhenSettled().Dispose();

        Bitmap occluded;

        using (ScreenOccluder.CoverPrimaryScreen())
        {
            // The instrument check. Without this, a covering window that never appeared would
            // make every assertion below pass for the wrong reason.
            //
            // Waited for rather than asserted outright: painting the occluder and DWM presenting
            // it are separate events, so reading the screen immediately after CreateWindowEx
            // races composition and reports a cover that is on its way.
            Assert.True(
                WaitUntilScreenIsDark(),
                "The occluding window did not cover the screen, so this test proves nothing.");

            occluded = CaptureWhenSettled();

            // The load-bearing assertion, and it stands on its own: every pixel of the screen is
            // black, yet the capture is a detailed image. There is nowhere for that image to have
            // come from except the window rendering itself on request.
            Assert.False(
                IsUniformlyDark(occluded),
                "The occluded capture came back black: the window declined to render itself and " +
                "the screen-reading fallback captured the occluder instead.");
        }

        using (occluded)
        {
            // Corroboration: the same app, captured normally, is the same picture. Taken after
            // the occluded one deliberately — a capture during the first moments of a freshly
            // opened page catches the chrome painted and the content not, which is a property of
            // the app's rendering, not of the capture, and it made the earlier ordering compare
            // an empty page against a full one.
            using var visible = CaptureWhenSettled();
            ReportScreenAgreement(visible);

            Assert.Equal(visible.Size, occluded.Size);

            var similarity = Similarity(visible, occluded);
            Assert.True(
                similarity >= SameImageThreshold,
                $"Occluded capture matched the uncovered capture only {similarity:P1} " +
                $"(needed {SameImageThreshold:P0}). The screenshot is of something other than the app.");
        }
    }

    /// <summary>
    /// Reports how closely the rendered capture matches what is physically on screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reported rather than asserted, in the same spirit as the layout probes. A settled window
    /// measured around 90%: the rendered capture and the screen agree, with the shortfall coming
    /// from <c>GetWindowRect</c> including the DWM resize border that the rendered content does
    /// not fill. So this is a sanity reading on <c>PrintWindow</c> rendering the right thing at
    /// the right size, and there is no calibrated threshold to gate on.
    /// </para>
    /// <para>
    /// A <i>low</i> reading almost always means the capture was taken before the app finished
    /// painting rather than that the capture is wrong — see <see cref="CaptureWhenSettled"/>,
    /// which exists because of exactly that. Check the timing before suspecting the mechanism.
    /// </para>
    /// </remarks>
    private void ReportScreenAgreement(Bitmap rendered)
    {
        var handle = ParseWindowHandle(_fixture.Context.Driver.CurrentWindowHandle);
        if (handle == 0 || !GetWindowRect(handle, out var bounds))
        {
            _output.WriteLine("Screen agreement: window rectangle unavailable.");
            return;
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
        {
            _output.WriteLine("Screen agreement: window rectangle is empty.");
            return;
        }

        using var fromScreen = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(fromScreen))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new System.Drawing.Size(width, height));
        }

        _output.WriteLine(
            $"Screen agreement: rendered {rendered.Width}x{rendered.Height}, " +
            $"screen {width}x{height}, similarity {Similarity(rendered, fromScreen):P1}.");
    }

    /// <summary>
    /// Captures once the rendered output has stopped changing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A concrete condition, not a sleep — but "two captures agree" is not enough on its own.
    /// A page that has not begun painting is perfectly stable at empty, and that is exactly what
    /// an earlier version of this test captured: chrome drawn, client area blank, two frames
    /// running identical. Element presence does not help either; a control joins the UIA tree
    /// before its pixels exist.
    /// </para>
    /// <para>
    /// So settling requires a change to have been seen first, then stability. Painting is the
    /// change; there is no need to guess how long it takes.
    /// </para>
    /// </remarks>
    private Bitmap CaptureWhenSettled(int timeoutMs = 5000, int graceMs = 1000)
    {
        var started = DateTime.UtcNow;
        var deadline = started.AddMilliseconds(timeoutMs);
        var previous = Decode(_fixture.Context.TakeScreenshot());
        var sawChange = false;

        while (DateTime.UtcNow < deadline)
        {
            var current = Decode(_fixture.Context.TakeScreenshot());
            var unchanged = previous.Size == current.Size
                            && Similarity(previous, current) >= SameImageThreshold;

            // Stable after a change means painting finished. Stable for the whole grace period
            // without any change means it had already finished before the first capture — the
            // common case for a page that has been up a while, and not worth waiting out.
            if (unchanged && (sawChange || DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(graceMs)))
            {
                previous.Dispose();
                return current;
            }

            sawChange |= !unchanged;
            previous.Dispose();
            previous = current;
        }

        _output.WriteLine($"Capture never settled within {timeoutMs} ms; using the last frame.");
        return previous;
    }

    private static Bitmap Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);

        // Copy out: a Bitmap built over a stream keeps the stream alive for its lifetime.
        using var decoded = new Bitmap(stream);
        return new Bitmap(decoded);
    }

    /// <summary>Fraction of sampled positions where two images agree within tolerance.</summary>
    private static double Similarity(Bitmap left, Bitmap right)
    {
        var matched = 0;
        var sampled = 0;

        for (var row = 0; row < ComparisonGrid; row++)
        {
            for (var column = 0; column < ComparisonGrid; column++)
            {
                var leftPixel = SampleAt(left, row, column);
                var rightPixel = SampleAt(right, row, column);
                sampled++;

                if (Math.Abs(leftPixel.R - rightPixel.R) <= ChannelTolerance &&
                    Math.Abs(leftPixel.G - rightPixel.G) <= ChannelTolerance &&
                    Math.Abs(leftPixel.B - rightPixel.B) <= ChannelTolerance)
                {
                    matched++;
                }
            }
        }

        return sampled == 0 ? 0d : (double)matched / sampled;
    }

    private static DrawingColor SampleAt(Bitmap bitmap, int row, int column)
        => bitmap.GetPixel(
            Math.Min(bitmap.Width - 1, bitmap.Width * column / ComparisonGrid),
            Math.Min(bitmap.Height - 1, bitmap.Height * row / ComparisonGrid));

    private static bool IsUniformlyDark(Bitmap bitmap)
    {
        for (var row = 0; row < ComparisonGrid; row++)
        {
            for (var column = 0; column < ComparisonGrid; column++)
            {
                var pixel = SampleAt(bitmap, row, column);
                if (pixel.R > ChannelTolerance || pixel.G > ChannelTolerance || pixel.B > ChannelTolerance)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Waits until the primary screen reads as covered, or gives up.
    /// </summary>
    /// <remarks>
    /// Each poll is a full screen capture, which paces the loop on its own — there is no sleep
    /// here, and the condition is the concrete state the caller actually depends on.
    /// </remarks>
    private static bool WaitUntilScreenIsDark(int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        do
        {
            if (PrimaryScreenIsMostlyDark())
                return true;
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static bool PrimaryScreenIsMostlyDark()
    {
        var width = GetSystemMetrics(SmCxScreen);
        var height = GetSystemMetrics(SmCyScreen);
        if (width <= 0 || height <= 0)
            return false;

        using var screen = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(screen))
        {
            graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));
        }

        return IsUniformlyDark(screen);
    }

    private static nint ParseWindowHandle(string handle)
        => long.TryParse(handle, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? (nint)value
            : 0;

    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out ScreenRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// A topmost black window covering the primary screen, for as long as it is held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses the predefined <c>STATIC</c> class with <c>SS_BLACKRECT</c>, so there is no window
    /// class to register and no message loop to run — the window paints itself black on the
    /// <c>WM_PAINT</c> that <c>UpdateWindow</c> sends directly.
    /// </para>
    /// <para>
    /// Shown with <c>SW_SHOWNOACTIVATE</c>: taking the foreground would be an odd thing for a
    /// test whose subject is not needing the foreground.
    /// </para>
    /// </remarks>
    private sealed class ScreenOccluder : IDisposable
    {
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsVisible = 0x10000000;
        private const int SsBlackRect = 0x00000004;
        private const int WsExTopmost = 0x00000008;
        private const int WsExNoActivate = 0x08000000;
        private const int SwShowNoActivate = 4;

        private nint _handle;

        private ScreenOccluder(nint handle) => _handle = handle;

        internal static ScreenOccluder CoverPrimaryScreen()
        {
            var handle = CreateWindowEx(
                WsExTopmost | WsExNoActivate,
                "STATIC",
                "Bruinet screenshot occluder",
                WsPopup | WsVisible | SsBlackRect,
                0, 0,
                GetSystemMetrics(SmCxScreen),
                GetSystemMetrics(SmCyScreen),
                0, 0, 0, 0);

            if (handle == 0)
                throw new InvalidOperationException("Could not create the occluding window.");

            ShowWindow(handle, SwShowNoActivate);

            // Force it to the top of the topmost band. WS_EX_TOPMOST alone only puts it in that
            // band; where it lands relative to another topmost window depends on activation
            // order, and SW_SHOWNOACTIVATE deliberately declines to activate.
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0,
                (uint)(SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow));

            UpdateWindow(handle);

            // Paint it black directly rather than trusting WM_PAINT. This thread runs no message
            // loop, and SS_BLACKRECT alone left the window unpainted — which made the occluder
            // silently transparent and the instrument check fail, correctly.
            PaintBlack(handle);

            return new ScreenOccluder(handle);
        }

        private static void PaintBlack(nint handle)
        {
            var deviceContext = GetDC(handle);
            if (deviceContext == 0)
                throw new InvalidOperationException("Could not get a device context for the occluder.");

            try
            {
                var area = new ScreenRect
                {
                    Left = 0,
                    Top = 0,
                    Right = GetSystemMetrics(SmCxScreen),
                    Bottom = GetSystemMetrics(SmCyScreen),
                };

                _ = FillRect(deviceContext, ref area, GetStockObject(BlackBrush));
            }
            finally
            {
                _ = ReleaseDC(handle, deviceContext);
            }
        }

        public void Dispose()
        {
            if (_handle == 0)
                return;

            DestroyWindow(_handle);
            _handle = 0;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern nint CreateWindowEx(
            int exStyle, string className, string windowName, int style,
            int x, int y, int width, int height,
            nint parent, nint menu, nint instance, nint param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateWindow(nint hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint hwnd);

        private const int BlackBrush = 4;
        private static readonly nint HwndTopmost = -1;
        private const int SwpNoSize = 0x0001;
        private const int SwpNoMove = 0x0002;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern nint GetDC(nint hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(nint hwnd, nint deviceContext);

        [DllImport("user32.dll")]
        private static extern int FillRect(nint deviceContext, ref ScreenRect rect, nint brush);

        [DllImport("gdi32.dll")]
        private static extern nint GetStockObject(int index);
    }
}
