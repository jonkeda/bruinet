using System.Drawing;
using System.Runtime.InteropServices;
using static Bruinet.FlaUI.Windowing.NativeMethods;

namespace Bruinet.FlaUI.Windowing;

/// <summary>
/// Puts the app under test where the harness asked: <c>BRUINET_AUT_PLACE</c> in the app's launch
/// environment (a stack's driver options set it, as <c>MauiDriverOptions.Placement</c> does), or else in
/// the test process's.
/// </summary>
internal static class WindowPlacement
{
    private enum AutPlacement
    {
        /// <summary>Leave the window wherever Windows put it.</summary>
        Default,

        /// <summary>The right of the primary work area, leaving a column for a presenter.</summary>
        Right,

        /// <summary>Beyond every monitor bar a sliver: driveable, and not in anyone's way.</summary>
        OffScreen,

        /// <summary>A non-primary monitor, falling back to <see cref="Right"/> when there is none.</summary>
        Secondary,

        /// <summary>The left half of the primary work area: one of two apps side by side.</summary>
        LeftHalf,

        /// <summary>The right half of the primary work area: the other of two apps side by side.</summary>
        RightHalf,
    }

    /// <summary>Applies the requested placement, if any, and writes the placement report.</summary>
    internal static void ApplyRequested(AppWindow window, IReadOnlyDictionary<string, string>? launchEnvironment = null)
    {
        var placement = ReadRequestedPlacement(launchEnvironment, out var unknownValue);

        if (unknownValue != null)
        {
            WriteReport(
                Rectangle.Empty, Rectangle.Empty, AutPlacement.Default, "not supported",
                $"BRUINET_AUT_PLACE='{unknownValue}' is not one of right, offscreen, secondary, left-half, right-half.");
            return;
        }

        if (placement == AutPlacement.Default)
        {
            return;
        }

        var workArea = GetPrimaryWorkArea();
        var requested = ComputeRequestedBounds(window, placement, workArea, out var presenter, out var effective);

        try
        {
            var root = window.Element;
            if (!root.Patterns.Transform.IsSupported)
            {
                WriteReport(presenter, requested, effective, "not supported", "Transform pattern is not supported.");
                return;
            }

            var transform = root.Patterns.Transform.Pattern;
            if (!transform.CanMove.Value)
            {
                WriteReport(presenter, requested, effective, "not supported", "Window cannot be moved.");
                return;
            }

            if (transform.CanResize.Value)
            {
                transform.Resize(requested.Width, requested.Height);
            }

            transform.Move(requested.Left, requested.Top);

            var actual = window.Element.BoundingRectangle;
            if (!LandedWhereAsked(actual, requested))
            {
                // UIA's Transform pattern keeps an element reachable on screen, so WinUI clamps a
                // move past the desktop edge back to the origin. Measured - the placement report
                // is what caught it. Positioning the app under test is harness business, so drop
                // to the Win32 call, which holds no such opinion.
                if (TryMoveDirectly(window.Handle, requested))
                {
                    actual = window.Element.BoundingRectangle;
                }
            }

            var landed = LandedWhereAsked(actual, requested);
            WriteReport(
                presenter, requested, effective,
                landed ? "moved" : "clamped",
                landed ? null : "The window was not allowed to move where asked.",
                actual);
        }
        catch (Exception ex)
        {
            WriteReport(presenter, requested, effective, $"failed: {ex.Message}");
        }
    }

    private static AutPlacement ReadRequestedPlacement(IReadOnlyDictionary<string, string>? launchEnvironment, out string? unknownValue)
    {
        unknownValue = null;

        var requested = launchEnvironment is not null && launchEnvironment.TryGetValue("BRUINET_AUT_PLACE", out var perLaunch)
            ? perLaunch
            : Environment.GetEnvironmentVariable("BRUINET_AUT_PLACE");
        if (!string.IsNullOrWhiteSpace(requested))
        {
            switch (requested.Trim().ToLowerInvariant())
            {
                case "right":
                    return AutPlacement.Right;
                case "offscreen":
                    return AutPlacement.OffScreen;
                case "secondary":
                    return AutPlacement.Secondary;
                case "left-half":
                    return AutPlacement.LeftHalf;
                case "right-half":
                    return AutPlacement.RightHalf;
                default:
                    // A typo should not silently leave the window where it was, and should not
                    // end the run either. Record it and place nothing.
                    unknownValue = requested;
                    return AutPlacement.Default;
            }
        }

        return string.Equals(
                Environment.GetEnvironmentVariable("BRUINET_AUT_PLACE_RIGHT"),
                "1",
                StringComparison.Ordinal)
            ? AutPlacement.Right
            : AutPlacement.Default;
    }

    private static Rectangle ComputeRequestedBounds(
        AppWindow window,
        AutPlacement placement,
        Rectangle workArea,
        out Rectangle presenter,
        out AutPlacement effective)
    {
        if (placement == AutPlacement.OffScreen)
        {
            effective = AutPlacement.OffScreen;
            presenter = workArea;
            return ComputeOffScreenBounds(window);
        }

        if (placement is AutPlacement.LeftHalf or AutPlacement.RightHalf)
        {
            effective = placement;
            presenter = Rectangle.Empty;
            var half = workArea.Width / 2;
            return placement == AutPlacement.LeftHalf
                ? new Rectangle(workArea.Left, workArea.Top, half, workArea.Height)
                : new Rectangle(workArea.Left + half, workArea.Top, workArea.Width - half, workArea.Height);
        }

        if (placement == AutPlacement.Secondary && TryGetSecondaryWorkArea(out var secondary))
        {
            effective = AutPlacement.Secondary;
            presenter = workArea;
            return secondary;
        }

        effective = AutPlacement.Right;

        const int Gap = 20;
        const int MinimumWidth = 320;

        var presenterWidth = Math.Max(MinimumWidth, workArea.Width / 4);
        presenter = new Rectangle(workArea.Left, workArea.Top, presenterWidth, workArea.Height);

        var left = Math.Min(workArea.Right - MinimumWidth, workArea.Left + presenterWidth + Gap);
        var width = Math.Max(MinimumWidth, workArea.Right - left);

        return new Rectangle(left, workArea.Top, width, workArea.Height);
    }

    private static Rectangle ComputeOffScreenBounds(AppWindow window)
    {
        const int Sliver = 8;

        var virtualScreen = new Rectangle(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));
        var current = window.Element.BoundingRectangle;
        var size = current is { Width: > 0, Height: > 0 } ? current.Size : new Size(1024, 768);

        return new Rectangle(virtualScreen.Left - size.Width + Sliver, virtualScreen.Top, size.Width, size.Height);
    }

    private static Rectangle GetPrimaryWorkArea()
    {
        if (SystemParametersInfo(SpiGetWorkArea, 0, out var rect, 0))
        {
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        return new Rectangle(0, 0, GetSystemMetrics(SmCxScreen), GetSystemMetrics(SmCyScreen));
    }

    private static bool LandedWhereAsked(Rectangle actual, Rectangle requested)
    {
        const int Tolerance = 16;

        return Math.Abs(actual.Left - requested.Left) <= Tolerance
               && Math.Abs(actual.Top - requested.Top) <= Tolerance;
    }

    private static bool TryMoveDirectly(IntPtr handle, Rectangle bounds)
        => handle != IntPtr.Zero
           && SetWindowPos(
               handle, IntPtr.Zero,
               bounds.Left, bounds.Top, bounds.Width, bounds.Height,
               SwpNoZOrder | SwpNoActivate);

    private static bool TryGetSecondaryWorkArea(out Rectangle workArea)
    {
        Rectangle? secondary = null;

        MonitorEnumProc callback = (nint monitor, nint _, ref NativeRect _, nint _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

            if (!GetMonitorInfo(monitor, ref info) || (info.Flags & MonitorInfoPrimary) != 0)
            {
                return true;    // keep looking
            }

            secondary = Rectangle.FromLTRB(
                info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Right, info.WorkArea.Bottom);

            return false;       // first one will do
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);

        workArea = secondary ?? Rectangle.Empty;
        return secondary is not null;
    }

    private static void WriteReport(
        Rectangle presenter,
        Rectangle requested,
        AutPlacement placement,
        string result,
        string? reason = null,
        Rectangle? actual = null)
    {
        var path = Environment.GetEnvironmentVariable("BRUINET_AUT_PLACEMENT_RESULT_FILE");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            List<string> lines =
            [
                "AUT placement:",
                $"Placement: {placement}",
                $"Presenter bounds: {Format(presenter)}",
                $"Requested AUT bounds: {Format(requested)}",
                $"Result: {result}"
            ];

            if (actual is not null)
            {
                lines.Add($"Actual AUT bounds: {Format(actual.Value)}");
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                lines.Add($"Reason: {reason}");
            }

            File.WriteAllText(path, string.Join(Environment.NewLine, lines));
        }
        catch
        {
            // Placement diagnostics should never make a test session fail.
        }
    }

    private static string Format(Rectangle rectangle)
        => $"x={rectangle.X} y={rectangle.Y} w={rectangle.Width} h={rectangle.Height}";
}
