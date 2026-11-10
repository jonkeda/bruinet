namespace Bruinet.Stride.Context;

/// <summary>
/// Configuration options for Stride UI tests.
/// </summary>
public class StrideTestContextOptions
{
    #region Timeouts

    /// <summary>Default timeout for wait operations.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMilliseconds(3000);

    /// <summary>Short timeout for quick checks.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan ShortTimeout { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>Polling interval for wait operations.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Timeout for game startup.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMilliseconds(10000);

    /// <summary>Timeout for connecting to the automation channel.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

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
        DefaultWait = DefaultTimeout,
        PageLoad = StartupTimeout,
        ElementFind = ShortTimeout,
        ElementState = DefaultTimeout,
        Animation = TimeSpan.FromMilliseconds(150),
        PollingInterval = PollingInterval
    };
}
