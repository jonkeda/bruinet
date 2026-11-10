using System.Text.Json.Serialization;

namespace Bruinet.Core.Configuration;

/// <summary>
/// Timeout configuration for test operations. Settings are <see cref="TimeSpan"/>s; a control verb's
/// own budget is <c>int? timeoutMs</c>, and <see cref="TimeSpanMilliseconds.ToMs"/> bridges the two
/// (public API review, proposal 9). In a settings file each one is a number of milliseconds or a
/// <see cref="TimeSpan"/> string.
/// </summary>
public class TimeoutSettings
{
    /// <summary>The environment variable that scales every budget on a slow machine (a factor, such as 2 or 1.5).</summary>
    public const string ScaleEnvironmentVariable = "BRUINET_TIMEOUT_SCALE";

    /// <summary>
    /// The run's scale on the waits a call takes from these settings (<see cref="DefaultWait"/>,
    /// <see cref="PageLoad"/>, <see cref="ElementFind"/>, <see cref="ElementState"/>), read once from
    /// <see cref="ScaleEnvironmentVariable"/>; 1 when it is unset or not a factor from 0.1 to 20. A
    /// <c>timeoutMs</c> a test passes to a call is its own and is not scaled; polling and animation
    /// are not either.
    /// </summary>
    public static double Scale { get; } = ReadScale(Environment.GetEnvironmentVariable(ScaleEnvironmentVariable));

    internal static double ReadScale(string? value)
        => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale)
           && scale is >= 0.1 and <= 20
            ? scale
            : 1;

    private static TimeSpan Scaled(TimeSpan value)
        => Scale == 1 ? value : TimeSpan.FromMilliseconds(Math.Round(value.TotalMilliseconds * Scale));

    /// <summary>Default timeout for wait operations.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan DefaultWait { get => Scaled(_defaultWait); set => _defaultWait = value; }

    private TimeSpan _defaultWait = TimeSpan.FromMilliseconds(5000);

    /// <summary>Timeout for page load operations.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan PageLoad { get => Scaled(_pageLoad); set => _pageLoad = value; }

    private TimeSpan _pageLoad = TimeSpan.FromMilliseconds(10000);

    /// <summary>Timeout for element finding.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan ElementFind { get => Scaled(_elementFind); set => _elementFind = value; }

    private TimeSpan _elementFind = TimeSpan.FromMilliseconds(3000);

    /// <summary>Timeout for element state changes.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan ElementState { get => Scaled(_elementState); set => _elementState = value; }

    private TimeSpan _elementState = TimeSpan.FromMilliseconds(3000);

    /// <summary>Delay for animation settling.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan Animation { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Polling interval for wait operations.</summary>
    [JsonConverter(typeof(MillisecondsTimeSpanConverter))]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Default timeout settings.
    /// </summary>
    public static TimeoutSettings Default => new();

    /// <summary>
    /// Fast timeout settings for quick checks.
    /// </summary>
    public static TimeoutSettings Fast => new()
    {
        DefaultWait = TimeSpan.FromMilliseconds(2000),
        PageLoad = TimeSpan.FromMilliseconds(5000),
        ElementFind = TimeSpan.FromMilliseconds(1000),
        ElementState = TimeSpan.FromMilliseconds(1000),
        Animation = TimeSpan.FromMilliseconds(150),
        PollingInterval = TimeSpan.FromMilliseconds(50)
    };

    /// <summary>
    /// Slow timeout settings for flaky environments.
    /// </summary>
    public static TimeoutSettings Slow => new()
    {
        DefaultWait = TimeSpan.FromMilliseconds(15000),
        PageLoad = TimeSpan.FromMilliseconds(30000),
        ElementFind = TimeSpan.FromMilliseconds(10000),
        ElementState = TimeSpan.FromMilliseconds(10000),
        Animation = TimeSpan.FromMilliseconds(500),
        PollingInterval = TimeSpan.FromMilliseconds(200)
    };

    /// <summary>
    /// Create a copy with modified values.
    /// </summary>
    public TimeoutSettings With(
        TimeSpan? defaultWait = null,
        TimeSpan? pageLoad = null,
        TimeSpan? elementFind = null,
        TimeSpan? elementState = null,
        TimeSpan? animation = null,
        TimeSpan? pollingInterval = null)
    {
        return new TimeoutSettings
        {
            // The settings as set, not as scaled: the copy is scaled once, when read.
            DefaultWait = defaultWait ?? _defaultWait,
            PageLoad = pageLoad ?? _pageLoad,
            ElementFind = elementFind ?? _elementFind,
            ElementState = elementState ?? _elementState,
            Animation = animation ?? Animation,
            PollingInterval = pollingInterval ?? PollingInterval
        };
    }
}
