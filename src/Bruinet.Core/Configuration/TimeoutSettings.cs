namespace Bruinet.Core.Configuration;

/// <summary>
/// Timeout configuration for test operations.
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

    private static int Scaled(int milliseconds) => Scale == 1 ? milliseconds : (int)Math.Round(milliseconds * Scale);

    /// <summary>
    /// Default timeout for wait operations (milliseconds).
    /// </summary>
    public int DefaultWait { get => Scaled(_defaultWait); set => _defaultWait = value; }

    private int _defaultWait = 5000;

    /// <summary>
    /// Timeout for page load operations (milliseconds).
    /// </summary>
    public int PageLoad { get => Scaled(_pageLoad); set => _pageLoad = value; }

    private int _pageLoad = 10000;

    /// <summary>
    /// Timeout for element finding (milliseconds).
    /// </summary>
    public int ElementFind { get => Scaled(_elementFind); set => _elementFind = value; }

    private int _elementFind = 3000;

    /// <summary>
    /// Timeout for element state changes (milliseconds).
    /// </summary>
    public int ElementState { get => Scaled(_elementState); set => _elementState = value; }

    private int _elementState = 3000;

    /// <summary>
    /// Delay for animation settling (milliseconds).
    /// </summary>
    public int Animation { get; set; } = 300;

    /// <summary>
    /// Polling interval for wait operations (milliseconds).
    /// </summary>
    public int PollingInterval { get; set; } = 100;

    /// <summary>
    /// Default timeout settings.
    /// </summary>
    public static TimeoutSettings Default => new();

    /// <summary>
    /// Fast timeout settings for quick checks.
    /// </summary>
    public static TimeoutSettings Fast => new()
    {
        DefaultWait = 2000,
        PageLoad = 5000,
        ElementFind = 1000,
        ElementState = 1000,
        Animation = 150,
        PollingInterval = 50
    };

    /// <summary>
    /// Slow timeout settings for flaky environments.
    /// </summary>
    public static TimeoutSettings Slow => new()
    {
        DefaultWait = 15000,
        PageLoad = 30000,
        ElementFind = 10000,
        ElementState = 10000,
        Animation = 500,
        PollingInterval = 200
    };

    /// <summary>
    /// Create a copy with modified values.
    /// </summary>
    public TimeoutSettings With(
        int? defaultWait = null,
        int? pageLoad = null,
        int? elementFind = null,
        int? elementState = null,
        int? animation = null,
        int? pollingInterval = null)
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
