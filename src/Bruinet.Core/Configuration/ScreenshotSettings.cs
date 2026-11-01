using Bruinet.Core.Artifacts;

namespace Bruinet.Core.Configuration;

/// <summary>
/// Configuration settings for screenshot capture behavior.
/// </summary>
public class ScreenshotSettings
{
    /// <summary>
    /// Output directory for screenshots.
    /// </summary>
    public string OutputDirectory { get; set; } =
        DefaultTestArtifactPathProvider.Create().ScreenshotsDirectory;

    /// <summary>
    /// Screenshot image format.
    /// </summary>
    public ScreenshotFormat Format { get; set; } = ScreenshotFormat.Png;

    /// <summary>
    /// JPEG quality (1-100) when Format is Jpeg.
    /// </summary>
    public int JpegQuality { get; set; } = 85;

    /// <summary>
    /// Whether to capture screenshots on test failure.
    /// </summary>
    public bool CaptureOnFailure { get; set; } = true;

    /// <summary>
    /// Whether to include timestamp in filename.
    /// </summary>
    public bool IncludeTimestamp { get; set; } = true;

    /// <summary>
    /// Default screenshot settings.
    /// </summary>
    public static ScreenshotSettings Default => new();

    /// <summary>
    /// Create a copy with modified values.
    /// </summary>
    public ScreenshotSettings With(
        string? outputDirectory = null,
        ScreenshotFormat? format = null,
        int? jpegQuality = null,
        bool? captureOnFailure = null,
        bool? includeTimestamp = null)
    {
        return new ScreenshotSettings
        {
            OutputDirectory = outputDirectory ?? OutputDirectory,
            Format = format ?? Format,
            JpegQuality = jpegQuality ?? JpegQuality,
            CaptureOnFailure = captureOnFailure ?? CaptureOnFailure,
            IncludeTimestamp = includeTimestamp ?? IncludeTimestamp
        };
    }
}
