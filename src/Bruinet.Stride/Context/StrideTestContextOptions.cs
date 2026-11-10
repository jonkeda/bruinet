namespace Bruinet.Stride.Context;

/// <summary>
/// Configuration options for Stride UI tests.
/// </summary>
public class StrideTestContextOptions : Bruinet.Core.Configuration.TestContextOptionsBase
{
    /// <summary>
    /// Stride's call budgets by default: shorter than the other stacks', since a game answers in
    /// frames (proposal 11 put them on the shared <see cref="TestContextOptionsBase.Timeouts"/>).
    /// </summary>
    public StrideTestContextOptions()
        => Timeouts = new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(3000),
            PageLoad = TimeSpan.FromMilliseconds(10000),
            ElementFind = TimeSpan.FromMilliseconds(1000),
            ElementState = TimeSpan.FromMilliseconds(3000),
            Animation = TimeSpan.FromMilliseconds(150),
            PollingInterval = TimeSpan.FromMilliseconds(100),
        };

    #region Timeouts

    /// <summary>Timeout for game startup.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromMilliseconds(10000);

    /// <summary>Timeout for connecting to the automation channel.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    #endregion

    #region Calls




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

}
