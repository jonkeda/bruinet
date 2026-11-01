using System.Globalization;

namespace Bruinet.Core.Logging;

/// <summary>
/// Console logger implementation that writes to standard output.
/// </summary>
public class ConsoleTestLogger : ITestLogger
{
    public void Log(string testName, string pageName, string controlId, string action,
        string? value, string? expectedValue, LogResult result, string? message)
    {
        var valueStr = value != null ? $" Value={value}" : "";
        var expectedStr = expectedValue != null ? $" Expected={expectedValue}" : "";
        var msgStr = message != null ? $" ({message})" : "";
        Console.WriteLine($"[{result}] [{testName}][{pageName}] {controlId}.{action}{valueStr}{expectedStr}{msgStr}");
    }

    public void LogEntry(string testName, string pageName, string controlId, string action, string? value)
    {
        var valueStr = value != null ? $"({value})" : "()";
        Console.WriteLine($"[→] [{testName}][{pageName}] {controlId}.{action}{valueStr}");
    }

    public void LogExit(string testName, string pageName, string controlId, string action,
        LogResult result, int durationMs, string? message = null)
    {
        var msgStr = message != null ? $" ({message})" : "";
        Console.WriteLine($"[←] [{testName}][{pageName}] {controlId}.{action} {result} {durationMs}ms{msgStr}");
    }

    public void LogAssertExit(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue, LogResult result, int durationMs, string? message = null)
    {
        var msgStr = message != null ? $" ({message})" : "";
        Console.WriteLine($"[←] [{testName}][{pageName}] {controlId}.{assertType} " +
            $"Actual={actualValue ?? "null"} Expected={expectedValue ?? "null"} {result} {durationMs}ms{msgStr}");
    }

    public void LogAction(string testName, string pageName, string controlId, string action, string? value = null)
        => Log(testName, pageName, controlId, action, value, null, LogResult.Success, null);

    public void LogAssertPass(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue)
        => Log(testName, pageName, controlId, assertType, actualValue, expectedValue, LogResult.Success, null);

    public void LogAssertFail(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue, string? message = null)
        => Log(testName, pageName, controlId, assertType, actualValue, expectedValue, LogResult.Fail, message);

    public void LogWait(string testName, string pageName, string controlId, string waitType,
        bool success, int elapsedMs)
        => Log(testName, pageName, controlId, waitType, elapsedMs.ToString(CultureInfo.InvariantCulture), null,
            success ? LogResult.Success : LogResult.Fail, null);

    public void LogNavigation(string testName, string sourcePage, string targetPage)
        => Log(testName, sourcePage, "", "Navigate", targetPage, null, LogResult.Info, null);

    public void LogInfo(string testName, string pageName, string message)
        => Log(testName, pageName, "", "Info", null, null, LogResult.Info, message);

    public void LogError(string testName, string pageName, string controlId, string action, Exception ex)
        => Log(testName, pageName, controlId, action, null, null, LogResult.Error, ex.Message);

    public void LogScreenshot(string testName, string pageName, string screenshotPath, ScreenshotReason reason)
        => Console.WriteLine($"[📷] [{testName}][{pageName}] Screenshot ({reason}): {screenshotPath}");

    public void Flush() { }

    public void Dispose() { }
}
