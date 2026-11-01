using System.Drawing;

namespace Bruinet.Presenter.Services;

/// <summary>The side of the screen the Presenter bar docks to; the app under test takes the rest.</summary>
public enum PresenterDock
{
    Left = 0,
    Right = 1,
}

/// <summary>Where the bar and the app go on a monitor's work area.</summary>
public static class PresenterLayout
{
    /// <summary>The gap between the bar and the app.</summary>
    public const int Gap = 8;

    private const int MinimumBarWidth = 320;

    /// <summary>The bar: a quarter of the work area (at least 320 px), full height, on its side.</summary>
    /// <param name="workArea">The monitor's work area.</param>
    /// <param name="dock">The side.</param>
    public static Rectangle Bar(Rectangle workArea, PresenterDock dock)
    {
        var width = Math.Min(workArea.Width, Math.Max(MinimumBarWidth, workArea.Width / 4));
        return dock == PresenterDock.Left
            ? new Rectangle(workArea.Left, workArea.Top, width, workArea.Height)
            : new Rectangle(workArea.Right - width, workArea.Top, width, workArea.Height);
    }

    /// <summary>The app: the work area beside the bar, less the gap.</summary>
    /// <param name="workArea">The monitor's work area.</param>
    /// <param name="bar">Where the bar is.</param>
    /// <param name="dock">The side the bar is on.</param>
    public static Rectangle App(Rectangle workArea, Rectangle bar, PresenterDock dock)
        => dock == PresenterDock.Left
            ? Rectangle.FromLTRB(bar.Right + Gap, workArea.Top, workArea.Right, workArea.Bottom)
            : Rectangle.FromLTRB(workArea.Left, workArea.Top, bar.Left - Gap, workArea.Bottom);

    /// <summary>The work area of another monitor for a maximized app: the largest that is not the bar's; null with one monitor.</summary>
    /// <param name="workAreas">Every monitor's work area.</param>
    /// <param name="barWorkArea">The work area of the bar's monitor.</param>
    public static Rectangle? OtherMonitor(IEnumerable<Rectangle> workAreas, Rectangle barWorkArea)
        => workAreas
            .Where(area => area != barWorkArea)
            .OrderByDescending(area => (long)area.Width * area.Height)
            .Cast<Rectangle?>()
            .FirstOrDefault();
}
