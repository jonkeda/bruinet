using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

public sealed class MauiHeadsTests
{
    [Theory]
    [InlineData("headless", MauiHead.Headless)]
    [InlineData(" Headless ", MauiHead.Headless)]
    [InlineData("bunit", MauiHead.Device)]
    [InlineData("", MauiHead.Device)]
    [InlineData(null, MauiHead.Device)]
    [InlineData("device", MauiHead.Device)]
    public void Parse_NamesTheHead_TheDeviceUnlessMauiHeadless(string? value, MauiHead expected)
        => Assert.Equal(expected, MauiHeads.Parse(value));

    [Fact]
    public void The_MAUI_and_web_heads_are_picked_by_one_variable()
        => Assert.Equal(WebHeads.Variable, MauiHeads.Variable);
}
