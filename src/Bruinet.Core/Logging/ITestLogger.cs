namespace Bruinet.Core.Logging;

/// <summary>
/// Logger interface for test actions and diagnostics with entry/exit pattern.
/// </summary>
public interface ITestLogger : IDisposable
{
    #region Core Log Method

    /// <summary>
    /// Core log method - all convenience methods delegate to this.
    /// </summary>
    /// <param name="testName">Name of the current test.</param>
    /// <param name="pageName">Name of the current page.</param>
    /// <param name="controlId">Control AutomationId or locator string.</param>
    /// <param name="action">Action name (Click, Enter, etc.).</param>
    /// <param name="value">Actual/input value.</param>
    /// <param name="expectedValue">Expected value (for assertions).</param>
    /// <param name="result">Result category.</param>
    /// <param name="message">Additional context or error message.</param>
    void Log(
        string testName,
        string pageName,
        string controlId,
        string action,
        string? value,
        string? expectedValue,
        LogResult result,
        string? message);

    #endregion

    #region Entry/Exit Pattern

    /// <summary>
    /// Log entry point before an operation starts.
    /// </summary>
    /// <param name="testName">Name of the current test.</param>
    /// <param name="pageName">Name of the current page.</param>
    /// <param name="controlId">Control AutomationId or locator string.</param>
    /// <param name="action">Action name (Click, Enter, etc.).</param>
    /// <param name="value">Input value for the operation.</param>
    void LogEntry(
        string testName,
        string pageName,
        string controlId,
        string action,
        string? value);

    /// <summary>
    /// Log exit point after an operation completes.
    /// </summary>
    /// <param name="testName">Name of the current test.</param>
    /// <param name="pageName">Name of the current page.</param>
    /// <param name="controlId">Control AutomationId or locator string.</param>
    /// <param name="action">Action name (Click, Enter, etc.).</param>
    /// <param name="result">Result category (Success, Fail, Error).</param>
    /// <param name="durationMs">Operation duration in milliseconds.</param>
    /// <param name="message">Additional context or error message.</param>
    void LogExit(
        string testName,
        string pageName,
        string controlId,
        string action,
        LogResult result,
        int durationMs,
        string? message = null);

    /// <summary>
    /// Log exit point for assertion operations with expected/actual values.
    /// </summary>
    /// <param name="testName">Name of the current test.</param>
    /// <param name="pageName">Name of the current page.</param>
    /// <param name="controlId">Control AutomationId or locator string.</param>
    /// <param name="assertType">Assertion type (AssertText, AssertVisible, etc.).</param>
    /// <param name="actualValue">Actual value found.</param>
    /// <param name="expectedValue">Expected value.</param>
    /// <param name="result">Result category (Success, Fail).</param>
    /// <param name="durationMs">Operation duration in milliseconds.</param>
    /// <param name="message">Additional context or error message.</param>
    void LogAssertExit(
        string testName,
        string pageName,
        string controlId,
        string assertType,
        string? actualValue,
        string? expectedValue,
        LogResult result,
        int durationMs,
        string? message = null);

    #endregion

    #region Convenience Methods

    /// <summary>
    /// Log an action performed by a control.
    /// </summary>
    void LogAction(
        string testName,
        string pageName,
        string controlId,
        string action,
        string? value = null);

    /// <summary>
    /// Log a passed assertion.
    /// </summary>
    void LogAssertPass(
        string testName,
        string pageName,
        string controlId,
        string assertType,
        string? actualValue,
        string? expectedValue);

    /// <summary>
    /// Log a failed assertion.
    /// </summary>
    void LogAssertFail(
        string testName,
        string pageName,
        string controlId,
        string assertType,
        string? actualValue,
        string? expectedValue,
        string? message = null);

    /// <summary>
    /// Log a wait operation result.
    /// </summary>
    void LogWait(
        string testName,
        string pageName,
        string controlId,
        string waitType,
        bool success,
        int elapsedMs);

    /// <summary>
    /// Log a navigation event.
    /// </summary>
    void LogNavigation(
        string testName,
        string sourcePage,
        string targetPage);

    /// <summary>
    /// Log an informational message.
    /// </summary>
    void LogInfo(
        string testName,
        string pageName,
        string message);

    /// <summary>
    /// Log an error with exception details.
    /// </summary>
    void LogError(
        string testName,
        string pageName,
        string controlId,
        string action,
        Exception ex);

    /// <summary>
    /// Log a screenshot capture event.
    /// </summary>
    /// <param name="testName">Name of the current test.</param>
    /// <param name="pageName">Name of the current page.</param>
    /// <param name="screenshotPath">Path where the screenshot was saved.</param>
    /// <param name="reason">Reason for capturing the screenshot.</param>
    void LogScreenshot(
        string testName,
        string pageName,
        string screenshotPath,
        ScreenshotReason reason);

    #endregion

    #region Lifecycle

    /// <summary>
    /// Flush pending writes to storage.
    /// </summary>
    void Flush();

    #endregion
}
