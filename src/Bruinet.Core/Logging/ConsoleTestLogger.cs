using System.Globalization;

namespace Bruinet.Core.Logging;

/// <summary>
/// Console logger implementation that writes to standard output.
/// </summary>
public class ConsoleTestLogger : ITestLogger
{
    /// <inheritdoc />
    public void Write(LogEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Console.WriteLine(Line(entry));
    }

    /// <summary>The line written for <paramref name="entry"/>.</summary>
    internal static string Line(LogEvent entry)
    {
        var where = $"[{entry.TestName}][{entry.PageName}]";
        var message = entry.Message != null ? $" ({entry.Message})" : "";
        switch (entry.Kind)
        {
            case LogEventKind.Entry:
                return $"[→] {where} {entry.ControlId}.{entry.Action}({entry.Value})";

            case LogEventKind.Exit:
                var compared = entry.Actual != null || entry.Expected != null
                    ? $" Actual={entry.Actual ?? "null"} Expected={entry.Expected ?? "null"}"
                    : "";
                var ms = ((int)(entry.Duration ?? TimeSpan.Zero).TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
                return $"[←] {where} {entry.ControlId}.{entry.Action}{compared} {entry.Result} {ms}ms{message}";

            case LogEventKind.Screenshot:
                return $"[📷] {where} Screenshot ({entry.ScreenshotReason}): {entry.Value}";

            default:
                var value = entry.Value != null ? $" Value={entry.Value}" : "";
                var expected = entry.Expected != null ? $" Expected={entry.Expected}" : "";
                return $"[{entry.Result}] {where} {entry.ControlId}.{entry.Action}{value}{expected}{message}";
        }
    }

    /// <inheritdoc />
    public void Flush() { }

    /// <inheritdoc />
    public void Dispose() { }
}
