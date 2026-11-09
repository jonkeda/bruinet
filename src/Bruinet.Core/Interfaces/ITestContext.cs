using Bruinet.Core.Calls;
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
    /// The context's name in call logs and messages, such as the app's name when a scenario runs
    /// several apps (?A10); null when unnamed.
    /// </summary>
    string? Name => null;

    /// <summary>When a call that succeeded is still logged as a near-miss; the defaults unless a stack sets them.</summary>
    NearMissSettings NearMiss => NearMissSettings.Default;

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
