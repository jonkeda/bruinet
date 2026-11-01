namespace Bruinet.Core.Configuration;

/// <summary>
/// Timeout configuration for test operations.
/// </summary>
public static class TimeoutDefaults
{
    /// <summary>
    /// Default timeout for wait operations (milliseconds).
    /// </summary>
    public static int DefaultWait => 5000;

    /// <summary>
    /// Timeout for page load operations (milliseconds).
    /// </summary>
    public static int PageLoad => 10000;

    /// <summary>
    /// Timeout for element finding (milliseconds).
    /// </summary>
    public static int ElementFind => 5000;

    /// <summary>
    /// Timeout for element state changes (milliseconds).
    /// </summary>
    public static int ElementState => 5000;

    /// <summary>
    /// Delay for animation settling (milliseconds).
    /// </summary>
    public static int Animation => 300;

    /// <summary>
    /// Polling interval for wait operations (milliseconds).
    /// </summary>
    public static int PollingInterval => 100;
}
