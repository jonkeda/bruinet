namespace Bruinet.Maui.Enums;

/// <summary>
/// Represents the target platform for MAUI tests.
/// </summary>
public enum MauiPlatform
{
    /// <summary>
    /// Windows platform (WinUI/WinAppDriver).
    /// </summary>
    Windows,

    /// <summary>
    /// Android platform (UiAutomator2).
    /// </summary>
    Android,

    /// <summary>
    /// iOS platform (XCUITest).
    /// </summary>
    iOS,

    /// <summary>
    /// No device: the app in the test's own process, rendered by MauiHeadless (<c>BRUINET_HEAD=headless</c>).
    /// Its tree answers as Windows' UI Automation does (off-screen elements are in it, finding is not
    /// seeing), so what holds for Windows usually holds here.
    /// </summary>
    Headless
}
