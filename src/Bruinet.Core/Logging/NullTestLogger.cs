namespace Bruinet.Core.Logging;

/// <summary>
/// Null logger implementation that discards all messages.
/// Used as default when no logger is configured.
/// </summary>
public class NullTestLogger : ITestLogger
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static NullTestLogger Instance { get; } = new();

    public void Log(string testName, string pageName, string controlId, string action,
        string? value, string? expectedValue, LogResult result, string? message)
    { }

    public void LogEntry(string testName, string pageName, string controlId, string action, string? value) { }

    public void LogExit(string testName, string pageName, string controlId, string action,
        LogResult result, int durationMs, string? message = null)
    { }

    public void LogAssertExit(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue, LogResult result, int durationMs, string? message = null)
    { }

    public void LogAction(string testName, string pageName, string controlId, string action, string? value = null) { }

    public void LogAssertPass(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue)
    { }

    public void LogAssertFail(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue, string? message = null)
    { }

    public void LogWait(string testName, string pageName, string controlId, string waitType,
        bool success, int elapsedMs)
    { }

    public void LogNavigation(string testName, string sourcePage, string targetPage) { }

    public void LogInfo(string testName, string pageName, string message) { }

    public void LogError(string testName, string pageName, string controlId, string action, Exception ex) { }

    public void LogScreenshot(string testName, string pageName, string screenshotPath, ScreenshotReason reason) { }

    public void Flush() { }

    public void Dispose() { }
}
