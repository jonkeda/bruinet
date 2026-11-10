using Bruinet.Core.Testing;
using Bruinet.Maui.Testing;

namespace Bruinet.Maui.Tests;

public sealed class DeviceFactAttributeTests
{
    // Run with BRUINET_HEAD=headless, this test shows as skipped with its reason; without, it runs.
    [DeviceFact("it checks that the attribute skips on the MauiHeadless head")]
    public void DeviceFact_RunsOnlyOnTheDeviceHead()
    {
        Assert.False(MauiHeads.IsHeadless);
        Assert.Null(new DeviceFactAttribute().Skip);
    }
}
