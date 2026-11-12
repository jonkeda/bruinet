using Bruinet.Maui.Testing;

namespace Bruinet.Maui.UITests;

/// <summary>
/// A test about Windows alone: skipped on the mobile head, with the reason in the skip.
/// </summary>
/// <remarks>
/// <para>
/// For tests of the UI Automation bridge's own contract - what it publishes, how it refuses a verb
/// an element never declared, the app-side sinks for gestures MAUI has no recognizer for - and of
/// Windows' accessibility tree. Android has no bridge (AD-008): Appium drives the device's own
/// input, so these questions have no answer there, and a failure would only say so slowly.
/// </para>
/// <para>
/// A skip rather than excluding the file from the mobile head, so the test still shows in a mobile
/// run and says why it did not run. The heads are told apart at compile time: the Windows head
/// targets <c>net10.0-windows</c>, which defines <c>WINDOWS</c>; the mobile head is plain
/// <c>net10.0</c>.
/// </para>
/// </remarks>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    /// <summary>The usual reason: the test is about the Windows bridge.</summary>
    public const string Bridge = "tests the Windows UI Automation bridge, which Android and iOS do not have (AD-008)";

    public const string AccessibilityTree = "asserts what Windows' accessibility tree leaves out";

    /// <param name="reason">Why it is Windows only, shown as the skip reason elsewhere.</param>
    public WindowsOnlyFactAttribute(string reason = Bridge)
    {
#if !WINDOWS
        Skip = $"Windows only: {reason}.";
#else
        // The DevFlow runner drives the app through its in-app agent and reads MAUI's own tree, not
        // the bridge and the accessibility tree these tests are about.
        if (reason is Bridge or AccessibilityTree && string.Equals(Environment.GetEnvironmentVariable("BRUINET_MAUI_RUNNER")?.Trim(), "devflow", StringComparison.OrdinalIgnoreCase))
            Skip = $"FlaUI only: {reason}; this run uses the DevFlow runner (BRUINET_MAUI_RUNNER=devflow).";
#endif
    }

    /// <summary>
    /// What the test needs the Windows device for, when it does: also skipped on the MauiHeadless head, as
    /// <see cref="DeviceFactAttribute"/> is. Unset, the test runs headless too, where the head
    /// calls the app's bridge sinks as Windows does.
    /// </summary>
    public string? Device
    {
        get => _device;
        set
        {
            _device = value;
            if (value is not null && MauiHeads.IsHeadless)
            {
                Skip = $"Device only: {value}; this run uses the MauiHeadless head ({MauiHeads.Variable}=headless).";
            }
        }
    }

    private string? _device;
}
