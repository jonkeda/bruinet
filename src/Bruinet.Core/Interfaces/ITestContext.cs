using Bruinet.Core.Configuration;
using Bruinet.Core.Logging;

namespace Bruinet.Core.Interfaces;

/// <summary>
/// Base test context interface (non-generic).
/// Manages test execution environment, navigation, and screenshots.
/// </summary>
public interface ITestContext : IDisposable
{
    /// <summary>
    /// Timeout configuration for this test context.
    /// </summary>
    TimeoutSettings Timeouts { get; }

    /// <summary>
    /// Logger for test actions and diagnostics.
    /// </summary>
    ITestLogger Logger { get; }

    /// <summary>
    /// Navigate to a destination (URL for web, route for mobile).
    /// </summary>
    void NavigateTo(string destination);

    /// <summary>
    /// Navigate back in history.
    /// </summary>
    void NavigateBack();

    /// <summary>
    /// Refresh the current page/screen.
    /// </summary>
    void Refresh();

    /// <summary>
    /// Capture a screenshot as byte array.
    /// </summary>
    byte[] TakeScreenshot();

    /// <summary>
    /// Save a screenshot to the specified path.
    /// </summary>
    void SaveScreenshot(string path);

    /// <summary>
    /// Reset application state (clear cache, cookies, etc.).
    /// </summary>
    void ResetAppState();
}
