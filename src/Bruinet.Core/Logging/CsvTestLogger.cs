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

    /// <inheritdoc />
    public void Write(LogEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        WriteLine(Line(entry));
    }

    /// <summary>The CSV line written for <paramref name="entry"/>, below the header.</summary>
    internal static string Line(LogEvent entry)
    {
        var timestamp = entry.Timestamp.LocalDateTime.ToString("O", CultureInfo.InvariantCulture);
        return entry.Kind switch
        {
            LogEventKind.Entry => BuildCsvLine(timestamp, "→", entry.TestName, entry.PageName, entry.ControlId ?? "", entry.Action ?? "",
                entry.Value, null, null, null, null),
            LogEventKind.Exit => BuildCsvLine(timestamp, "←", entry.TestName, entry.PageName, entry.ControlId ?? "", entry.Action ?? "",
                entry.Actual, entry.Expected, entry.Result?.ToString(), (int?)entry.Duration?.TotalMilliseconds, entry.Message),
            LogEventKind.Screenshot => BuildCsvLine(timestamp, "", entry.TestName, entry.PageName, "", entry.Action ?? "",
                entry.Value, null, entry.Result?.ToString(), null, entry.ScreenshotReason?.ToString()),
            _ => BuildCsvLine(timestamp, "", entry.TestName, entry.PageName, entry.ControlId ?? "", entry.Action ?? "",
                entry.Value, entry.Expected, entry.Result?.ToString(), null, entry.Message),
        };
    }

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
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
