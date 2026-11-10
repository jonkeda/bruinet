using Bruinet.Core.Testing;
using Xunit;

namespace Bruinet.Maui.Testing;

/// <summary>
/// A MAUI test that needs a device (layout, pixels, a platform's own tree or driver): skipped on the
/// MauiHeadless head with the reason, so the run still lists it (docs/guides/maui-headless.md). The scenario's counterpart is
/// <c>@device</c>; the web's is <c>BrowserFact</c> (<c>Bruinet.Html.Testing</c>).
/// </summary>
public sealed class DeviceFactAttribute : FactAttribute
{
    /// <param name="reason">What the test needs the device for, shown in the skip.</param>
    public DeviceFactAttribute(string reason = "it needs a device")
    {
        if (MauiHeads.IsHeadless)
        {
            Skip = $"Device only: {reason}; this run uses the MauiHeadless head ({MauiHeads.Variable}=headless).";
        }
    }
}
