using Bruinet.Appium;

namespace Bruinet.Appium.Tests;

/// <summary>How a device id tells a simulator from a real device.</summary>
public class IosHostTests
{
    [Theory]
    [InlineData("A4395365-6928-480E-BE73-4C04C175420F", true)]
    [InlineData("00008110-001A2B3C4D5E801E", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSimulator_IsTrueOnlyForASimulatorsGuid(string? udid, bool expected)
        => Assert.Equal(expected, IosHost.IsSimulator(udid));
}
