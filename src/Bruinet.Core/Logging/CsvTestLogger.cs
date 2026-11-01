using System.Text;
using System.Globalization;

namespace Bruinet.Core.Logging;

/// <summary>
/// CSV logger implementation that writes to a file with entry/exit pattern.
/// Thread-safe with lock object, semicolon delimiter, ISO 8601 timestamps.
/// </summary>
public class CsvTestLogger : ITestLogger
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _headerWritten = false;

    /// <summary>
    /// Creates a new CSV logger writing to the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the CSV output file.</param>
    public CsvTestLogger(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
    }

    public void Log(string testName, string pageName, string controlId, string action,
        string? value, string? expectedValue, LogResult result, string? message)
    {
        var timestamp = DateTime.Now.ToString("O");
        var line = BuildCsvLine(timestamp, "", testName, pageName, controlId, action, 
            value, expectedValue, result.ToString(), null, message);
        WriteLine(line);
    }

    public void LogEntry(string testName, string pageName, string controlId, string action, string? value)
    {
        var timestamp = DateTime.Now.ToString("O");
        var line = BuildCsvLine(timestamp, "→", testName, pageName, controlId, action,
            value, null, null, null, null);
        WriteLine(line);
    }

    public void LogExit(string testName, string pageName, string controlId, string action,
        LogResult result, int durationMs, string? message = null)
    {
        var timestamp = DateTime.Now.ToString("O");
        var line = BuildCsvLine(timestamp, "←", testName, pageName, controlId, action,
            null, null, result.ToString(), durationMs, message);
        WriteLine(line);
    }

    public void LogAssertExit(string testName, string pageName, string controlId, string assertType,
        string? actualValue, string? expectedValue, LogResult result, int durationMs, string? message = null)
    {
        var timestamp = DateTime.Now.ToString("O");
        var line = BuildCsvLine(timestamp, "←", testName, pageName, controlId, assertType,
            actualValue, expectedValue, result.ToString(), durationMs, message);
        WriteLine(line);
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
    {
        var timestamp = DateTime.Now.ToString("O");
        var line = BuildCsvLine(timestamp, "←", testName, pageName, controlId, waitType,
            null, null, success ? "Success" : "Fail", elapsedMs, null);
        WriteLine(line);
    }

    public void LogNavigation(string testName, string sourcePage, string targetPage)
        => Log(testName, sourcePage, "", "Navigate", targetPage, null, LogResult.Info, null);

    public void LogInfo(string testName, string pageName, string message)
        => Log(testName, pageName, "", "Info", null, null, LogResult.Info, message);

    public void LogError(string testName, string pageName, string controlId, string action, Exception ex)
        => Log(testName, pageName, controlId, action, null, null, LogResult.Error, ex.Message);

    public void LogScreenshot(string testName, string pageName, string screenshotPath, ScreenshotReason reason)
        => Log(testName, pageName, "", "Screenshot", screenshotPath, null, LogResult.Info, reason.ToString());

    public void Flush()
    {
        lock (_lock)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        Flush();
        _writer.Dispose();
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            if (!_headerWritten)
            {
                _writer.WriteLine("Timestamp;Direction;TestName;PageName;ControlId;Action;Value;Expected;Result;DurationMs;Message");
                _headerWritten = true;
            }
            _writer.WriteLine(line);
        }
    }

    private static string BuildCsvLine(string timestamp, string direction, string testName, 
        string pageName, string controlId, string action, string? value, string? expected, 
        string? result, int? durationMs, string? message)
    {
        return string.Join(";",
            EscapeCsvValue(timestamp),
            EscapeCsvValue(direction),
            EscapeCsvValue(testName),
            EscapeCsvValue(pageName),
            EscapeCsvValue(controlId),
            EscapeCsvValue(action),
            EscapeCsvValue(value),
            EscapeCsvValue(expected),
            EscapeCsvValue(result),
            durationMs?.ToString(CultureInfo.InvariantCulture) ?? "",
            EscapeCsvValue(message));
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
