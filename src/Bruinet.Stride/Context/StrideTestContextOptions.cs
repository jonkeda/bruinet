namespace Bruinet.Stride.Context;

/// <summary>
/// Configuration options for Stride UI tests.
/// </summary>
public class StrideTestContextOptions
{
    #region Timeouts

    /// <summary>
    /// Default timeout in milliseconds for wait operations.
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Short timeout in milliseconds for quick checks.
    /// </summary>
    public int ShortTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Polling interval in milliseconds for wait operations.
    /// </summary>
    public int PollingIntervalMs { get; set; } = 100;

    /// <summary>
    /// Timeout in milliseconds for game startup.
    /// </summary>
    public int StartupTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Timeout in milliseconds for connecting to automation channel.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    #endregion

    #region Calls

    /// <summary>The call log; <c>BRUINET_CALL_LOG</c>'s, or none, when null.</summary>
    public ITestLogger? Logger { get; set; }

    /// <summary>The context's name in call logs, when a scenario runs several apps.</summary>
    public string? Name { get; set; }

    /// <summary>When a call that passed is logged as a near-miss.</summary>
    public Bruinet.Core.Calls.NearMissSettings NearMiss { get; set; } = Bruinet.Core.Calls.NearMissSettings.Default;

    #endregion

    #region Game Configuration

    /// <summary>
    /// Path to the game executable. If null, will attempt to find automatically.
    /// </summary>
    public string? GameExecutablePath { get; set; }

    /// <summary>
    /// Arguments to pass to the game executable.
    /// </summary>
    public string[] GameArguments { get; set; } = [];

    /// <summary>
    /// If true, attach to an existing game instance instead of starting a new one.
    /// </summary>
    public bool AttachToExisting { get; set; }

    /// <summary>
    /// Name of the named pipe for automation communication.
    /// </summary>
    public string PipeName { get; set; } = Communication.NamedPipeChannel.DefaultPipeName;

    #endregion

    #region Screenshots

    public string ScreenshotDirectory { get; set; } =
        Bruinet.Core.Artifacts.DefaultTestArtifactPathProvider.Create().ScreenshotsDirectory;
    public bool CaptureScreenshotOnFailure { get; set; } = true;

    #endregion

    #region Logging

    public string LogDirectory { get; set; } =
        Bruinet.Core.Artifacts.DefaultTestArtifactPathProvider.Create().LogsDirectory;
    public bool EnableCsvLogging { get; set; } = true;

    #endregion

    /// <summary>
    /// Convert to Core TimeoutSettings.
    /// </summary>
    public TimeoutSettings ToTimeoutSettings() => new()
    {
        DefaultWait = DefaultTimeoutMs,
        PageLoad = StartupTimeoutMs,
        ElementFind = ShortTimeoutMs,
        ElementState = DefaultTimeoutMs,
        Animation = 150,
        PollingInterval = PollingIntervalMs
    };
}
