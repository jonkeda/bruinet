using System.Text.Json.Serialization;

namespace Bruinet.Presenter.Services;

public sealed class PresenterUserSettings
{
    public string? LastOpenedFolder { get; set; }

    public List<string> RecentFolders { get; set; } = [];

    /// <summary>The video script last opened in Narrator mode.</summary>
    public string? LastScript { get; set; }

    /// <summary>
    /// Serialized by name so the settings file stays readable. A file written before
    /// this member existed has no "Theme" property and loads as <see cref="PresenterTheme.System" />.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PresenterTheme>))]
    public PresenterTheme Theme { get; set; } = PresenterTheme.System;

    /// <summary>The side the Presenter bar docks to; the app under test takes the rest of the screen.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PresenterDock>))]
    public PresenterDock Dock { get; set; } = PresenterDock.Left;

    /// <summary>The pace a run goes at: the Speed button's choice.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PresenterSpeed>))]
    public PresenterSpeed Speed { get; set; } = PresenterSpeed.Normal;

    /// <summary>The pause between steps of each speed, set on the Settings page.</summary>
    public PresenterSpeeds Speeds { get; set; } = new();

    /// <summary>How the app under test's window is shown during a run.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PresenterAppWindow>))]
    public PresenterAppWindow AppWindow { get; set; } = PresenterAppWindow.BesideBar;

    /// <summary>What happens to the app when a run ends.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PresenterRunEnd>))]
    public PresenterRunEnd RunEnd { get; set; } = PresenterRunEnd.CloseApp;

    /// <summary>Where Record puts its videos; null for the suite's <c>TestResults\recordings</c>.</summary>
    public string? RecordingsFolder { get; set; }

    /// <summary>The voice a recording speaks with when the video script names none; null for the default voice.</summary>
    public string? Voice { get; set; }

    /// <summary>How long a recording waits after a step, in milliseconds, when the video script does not say.</summary>
    public int Beat { get; set; } = 600;

    /// <summary>Whether a recording also plays each line aloud while the run waits for it.</summary>
    public bool SpeakAloud { get; set; }

    /// <summary>The touch marks' accent, <c>#RRGGBB</c>, when the video script names none; null for Exact red.</summary>
    public string? TouchColor { get; set; }

    /// <summary>
    /// The build configuration and platform chosen per workspace folder.
    /// </summary>
    /// <remarks>
    /// Session state, not config: a configuration and a platform are things you switch while
    /// demoing, so writing them into <c>uat.config.md</c> would mean editing the file to show
    /// the other one.
    /// </remarks>
    public Dictionary<string, PresenterWorkspacePreferences> WorkspacePreferences { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>What one workspace was last run as.</summary>
public sealed class PresenterWorkspacePreferences
{
    /// <summary>The build configuration, such as <c>Debug</c>.</summary>
    public string Configuration { get; set; } = "Debug";

    /// <summary>The platform, as <c>APPIUM_PLATFORM</c> takes it.</summary>
    public string Platform { get; set; } = "windows";
}

/// <summary>A run's pace: how long it waits between steps.</summary>
public enum PresenterSpeed
{
    Fast = 0,
    Normal = 1,
    Slow = 2,
}

/// <summary>The pause between steps of each speed, in milliseconds.</summary>
public sealed class PresenterSpeeds
{
    public int Fast { get; set; } = 250;

    public int Normal { get; set; } = 800;

    public int Slow { get; set; } = 1500;

    /// <summary>The pause of a speed.</summary>
    public int Of(PresenterSpeed speed) => speed switch
    {
        PresenterSpeed.Fast => Fast,
        PresenterSpeed.Slow => Slow,
        _ => Normal,
    };
}

/// <summary>How the app under test's window is shown during a run.</summary>
public enum PresenterAppWindow
{
    /// <summary>In front of other windows, in the rest of the bar's monitor.</summary>
    BesideBar = 0,

    /// <summary>Maximized on a monitor without the bar; beside the bar when there is none.</summary>
    MaximizedOtherMonitor = 1,

    /// <summary>Maximized on the bar's monitor, the bar kept on top while a run is on.</summary>
    Maximized = 2,

    /// <summary>In front of other windows, where it opened.</summary>
    WhereItOpens = 3,

    /// <summary>Behind everything and never activated, as in a test run.</summary>
    Background = 4,
}

/// <summary>What happens to the app under test when a run ends.</summary>
public enum PresenterRunEnd
{
    /// <summary>Closed when the run passes, fails or is stopped.</summary>
    CloseApp = 0,

    /// <summary>Left open until the next run starts, Stop is pressed with no run on, or Presenter closes.</summary>
    KeepOpen = 1,
}
