using System.Drawing;
using Bruinet.Core.Recording;
using Bruinet.Appium;


namespace Bruinet.Appium.Tests;

public sealed class IosRecorderTests
{
    [Theory]
    [InlineData(1206, 2622, 3.0, 402, 874)]
    [InlineData(1170, 2532, 3.0, 390, 844)]
    [InlineData(1640, 2360, 2.0, 820, 1180)]
    public void The_screen_is_in_points_the_pixels_over_the_scale(int width, int height, double scale, int pointWidth, int pointHeight)
    {
        Assert.Equal(new Rectangle(0, 0, pointWidth, pointHeight), IosRecorder.Points(new Size(width, height), scale));
    }

    [Fact]
    public void A_window_is_not_for_the_iOS_recorder()
    {
        Assert.False(new IosRecorder().CanRecord(new RecordingTarget.Window(1)));
    }
}
