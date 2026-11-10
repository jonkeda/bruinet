namespace Bruinet.Core.Logging;

/// <summary>What a <see cref="LogEvent"/> records.</summary>
public enum LogEventKind
{
    /// <summary>A control call began.</summary>
    Entry,

    /// <summary>A control call ended, with its result and duration.</summary>
    Exit,

    /// <summary>The app moved to another page.</summary>
    Navigation,

    /// <summary>Something failed outside a call's own result, such as a screenshot.</summary>
    Error,

    /// <summary>A screenshot was taken.</summary>
    Screenshot,

    /// <summary>A note.</summary>
    Info,
}

/// <summary>
/// One event for an <see cref="ITestLogger"/>. The kind, test and page are always there; the rest is
/// set where the kind has it. Make one with the factories (<see cref="Entry"/>, <see cref="Exit"/>…).
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="TestName">The test it happened in; empty when not known.</param>
/// <param name="PageName">The page it happened on; for a navigation, the page left.</param>
public sealed record LogEvent(LogEventKind Kind, string TestName, string PageName)
{
    /// <summary>When it happened.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    /// <summary>The control, for a call.</summary>
    public string? ControlId { get; init; }

    /// <summary>The call (<c>Click</c>, <c>AssertText</c>…) or the operation that failed.</summary>
    public string? Action { get; init; }

    /// <summary>The value the call was given; for a navigation, the page reached; for a screenshot, its path.</summary>
    public string? Value { get; init; }

    /// <summary>What an assertion expected.</summary>
    public string? Expected { get; init; }

    /// <summary>What an assertion found.</summary>
    public string? Actual { get; init; }

    /// <summary>How a call ended.</summary>
    public LogResult? Result { get; init; }

    /// <summary>How long a call took.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>A note: why a call failed, or nearly did.</summary>
    public string? Message { get; init; }

    /// <summary>The exception, for an <see cref="LogEventKind.Error"/>.</summary>
    public Exception? Error { get; init; }

    /// <summary>Why a screenshot was taken.</summary>
    public ScreenshotReason? ScreenshotReason { get; init; }

    /// <summary>A call began.</summary>
    public static LogEvent Entry(string testName, string pageName, string controlId, string action, string? value)
        => new(LogEventKind.Entry, testName, pageName) { ControlId = controlId, Action = action, Value = value };

    /// <summary>A call ended.</summary>
    public static LogEvent Exit(string testName, string pageName, string controlId, string action,
        LogResult result, TimeSpan duration, string? message = null)
        => new(LogEventKind.Exit, testName, pageName)
        {
            ControlId = controlId, Action = action, Result = result, Duration = duration, Message = message,
        };

    /// <summary>The app moved from <paramref name="sourcePage"/> to <paramref name="targetPage"/>.</summary>
    public static LogEvent Navigation(string testName, string sourcePage, string targetPage)
        => new(LogEventKind.Navigation, testName, sourcePage) { Action = "Navigate", Value = targetPage, Result = LogResult.Info };

    /// <summary><paramref name="action"/> failed with <paramref name="error"/>.</summary>
    public static LogEvent Failed(string testName, string pageName, string controlId, string action, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(LogEventKind.Error, testName, pageName)
        {
            ControlId = controlId, Action = action, Result = LogResult.Error, Message = error.Message, Error = error,
        };
    }

    /// <summary>A screenshot was saved at <paramref name="path"/>.</summary>
    public static LogEvent Screenshot(string testName, string pageName, string path, ScreenshotReason reason)
        => new(LogEventKind.Screenshot, testName, pageName)
        {
            Action = "Screenshot", Value = path, Result = LogResult.Info, ScreenshotReason = reason,
        };

    /// <summary>A note.</summary>
    public static LogEvent Info(string testName, string pageName, string message)
        => new(LogEventKind.Info, testName, pageName) { Action = "Info", Result = LogResult.Info, Message = message };
}
