using Bruinet.Core.Logging;

namespace Bruinet.Core.Models;

/// <summary>
/// Metadata for a captured screenshot.
/// </summary>
public record ScreenshotInfo
{
    /// <summary>Path to the saved screenshot file.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>Test class name.</summary>
    public string TestClass { get; init; } = "";

    /// <summary>Test method name.</summary>
    public string TestMethod { get; init; } = "";

    /// <summary>When the screenshot was captured.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Why the screenshot was captured.</summary>
    public ScreenshotReason Reason { get; init; }

    /// <summary>Exception message if captured due to failure.</summary>
    public string? ExceptionMessage { get; init; }
}
