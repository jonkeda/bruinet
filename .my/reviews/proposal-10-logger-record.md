# Proposal 10: the logger takes one record per event

Drafted 2026-11-10 at `48d8e853`; approved and made the same day. One change from the draft: the error factory is `LogEvent.Failed`, as a static `Error` would clash with the record's `Error` property.

## Outcome

`ITestLogger` shrinks from thirteen methods of five to nine positional strings to two: `Write(LogEvent)` and `Flush()`.
A new field is then one more property on the record, which breaks no logger, where today it breaks all three of Bruinet's and any consumer's own.
Eight of the thirteen methods have no caller at all, so most of the surface goes rather than moves.
Tomorrow neither implements nor calls the logger; the cost is inside Bruinet.

## The design

```csharp
public interface ITestLogger : IDisposable
{
    void Write(LogEvent entry);
    void Flush();
}

public sealed record LogEvent(LogEventKind Kind, string TestName, string PageName)
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string? ControlId { get; init; }
    public string? Action { get; init; }        // the call: Click, AssertText…
    public string? Value { get; init; }
    public string? Expected { get; init; }
    public string? Actual { get; init; }
    public LogResult? Result { get; init; }
    public TimeSpan? Duration { get; init; }    // proposal 9: TimeSpan outside the verbs
    public string? Message { get; init; }
    public Exception? Error { get; init; }
    public string? ScreenshotPath { get; init; }
    public ScreenshotReason? ScreenshotReason { get; init; }
}

public enum LogEventKind { Entry, Exit, Navigation, Error, Screenshot, Info }
```

Static factories on `LogEvent` (`Entry`, `Exit`, `Navigation`, `Error`, `Screenshot`, `Info`) keep Bruinet's ten call sites one line each.

## What changes

| Who | Today | After |
| --- | --- | --- |
| Bruinet's callers | `ControlCall` (5), `ScreenshotService` (3), two stacks' navigation (2) | the same sites, each `Write(LogEvent.…)` |
| Bruinet's loggers | Console, CSV and Null implement thirteen methods each | each implements `Write`; the CSV file keeps its columns, the console its lines |
| Bruinet's tests | 15 `Verify(l => l.LogExit(It.IsAny…` checks | `Verify(l => l.Write(It.Is<LogEvent>(e => e.Kind == LogEventKind.Exit && e.Result == …)))` |
| A consumer's own logger | thirteen methods | one `Write`, with a `switch` on `Kind` if it needs one |

## Why this and not the alternatives

Microsoft.Extensions.Logging's `ILogger` was the other choice: familiar, but its fields are a message template, and the CSV log and the near-miss checks read typed fields (result, duration, control).
An adapter from `LogEvent` to `ILogger` stays possible later as a logger of its own.
Keeping the methods and adding a record overload beside them would double the surface the review set out to shrink.

## Unknown

- Whether projects other than Tomorrow implement `ITestLogger` (the upgrade plan's Q6); the change list's row tells them what to write.
- Whether anything reads the console log's exact wording; the design keeps it, so nothing should break.

## How it is made

One commit: the record and interface, the three loggers, the ten call sites, the 15 test checks, the change-list row and a changelog line.
Then Tomorrow's two suites build in a worktree, as before, and it is pushed.

Next action: none; this design is done.
