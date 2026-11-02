namespace Bruinet.Maui.UITests;

/// <summary>
/// A known difference of the Windows device head (docs/guides/maui-headless.md, "Differences Between The
/// Heads"): skipped there with the reason, run on every other head, until it is fixed.
/// </summary>
/// <remarks>
/// The reverse of <see cref="DeviceFactAttribute"/>, for what the device cannot do yet and the other heads
/// can. A skip rather than a failure, so a Windows run says what it did not prove and why.
/// <c>BRUINET_RUN_GAPS=1</c> runs it anyway, to work on the gap.
/// </remarks>
public sealed class WindowsGapFactAttribute : FactAttribute
{
    /// <param name="reason">What the Windows head does instead, shown in the skip.</param>
    public WindowsGapFactAttribute(string reason)
    {
#if WINDOWS
        if (!MauiHeads.IsHeadless && Environment.GetEnvironmentVariable("BRUINET_RUN_GAPS") != "1")
        {
            Skip = $"Known Windows gap: {reason}.";
        }
#endif
    }
}
