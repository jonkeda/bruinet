namespace Bruinet.Maui.UITests;

/// <summary>
/// Constants for UI test configuration.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Default timeout for individual tests in milliseconds.
    /// Tests exceeding this duration will be cancelled.
    /// </summary>
    public const int DefaultTestTimeoutMs = 15_000;

    /// <summary>
    /// Short timeout for fast tests in milliseconds.
    /// </summary>
    /// <summary>
    /// Whether the run's own build step installed the apps on the device, so a session need not.
    /// </summary>
    /// <remarks>
    /// The Android fixtures reinstall their app at every session start (<c>enforceAppInstall</c>):
    /// MAUI does not bump versionCode between builds, so otherwise a run could test the previous
    /// build. That is 13 s of each 32-43 s session start (measured 2026-10-21, a 99 MB Debug APK).
    /// A runner that installs both fresh APKs itself sets <c>BRUINET_APP_PREINSTALLED=1</c> and
    /// the sessions start on what is there.
    /// </remarks>
    public static bool AppPreinstalled
        => Environment.GetEnvironmentVariable("BRUINET_APP_PREINSTALLED") == "1";

    public const int ShortTestTimeoutMs = 10_000;

    /// <summary>
    /// Long timeout for complex tests in milliseconds.
    /// </summary>
    public const int LongTestTimeoutMs = 60_000;
}
