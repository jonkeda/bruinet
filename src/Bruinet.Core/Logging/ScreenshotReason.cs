namespace Bruinet.Core.Logging;

/// <summary>
/// Reason why a screenshot was captured.
/// </summary>
public enum ScreenshotReason
{
    /// <summary>Manual screenshot requested by test code.</summary>
    Manual,

    /// <summary>Screenshot captured due to assertion failure.</summary>
    AssertionFailure,

    /// <summary>Screenshot captured due to unhandled exception.</summary>
    Exception,

    /// <summary>Screenshot captured due to timeout.</summary>
    Timeout,

    /// <summary>Screenshot captured due to element not found.</summary>
    ElementNotFound
}
