namespace Bruinet.Core.Logging;

/// <summary>
/// Result categories for log entries.
/// </summary>
public enum LogResult
{
    /// <summary>Operation completed successfully.</summary>
    Success,

    /// <summary>Assertion or condition not met.</summary>
    Fail,

    /// <summary>Exception occurred during operation.</summary>
    Error,

    /// <summary>Informational message (navigation, info).</summary>
    Info,

    /// <summary>Potential issue detected.</summary>
    Warning
}
