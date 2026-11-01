using Bruinet.Core.Configuration;

namespace Bruinet.Core.Interfaces;

/// <summary>
/// Service for capturing and saving screenshots during test execution.
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// Capture and save a screenshot with auto-generated name.
    /// </summary>
    /// <param name="description">Optional description for the filename.</param>
    /// <returns>Path to the saved screenshot, or empty string if capture failed.</returns>
    string Capture(string? description = null);

    /// <summary>
    /// Capture and save a screenshot with specific test context.
    /// </summary>
    /// <param name="testClass">Test class name.</param>
    /// <param name="testMethod">Test method name.</param>
    /// <param name="description">Description for the filename.</param>
    /// <returns>Path to the saved screenshot, or empty string if capture failed.</returns>
    string Capture(string testClass, string testMethod, string description);

    /// <summary>
    /// Capture screenshot on test failure.
    /// </summary>
    /// <param name="testClass">Test class name.</param>
    /// <param name="testMethod">Test method name.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>Path to the saved screenshot, or empty string if capture failed or disabled.</returns>
    string CaptureOnFailure(string testClass, string testMethod, Exception exception);

    /// <summary>
    /// Current screenshot settings.
    /// </summary>
    ScreenshotSettings Settings { get; }
}
