using Bruinet.Core.Testing;

namespace Bruinet.Core.Logging;

/// <summary>A level of what the app said: a warning and up are kept, information only when asked for.</summary>
public enum StepLogLevel
{
    /// <summary>Information, kept with <c>BRUINET_STEP_LOG=all</c>.</summary>
    Information,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error, or an unhandled exception.</summary>
    Error,
}

/// <summary>What the app said during a step: its log, an exception nothing caught, the browser's console.</summary>
/// <param name="AtMs">When, on <see cref="Timing.NowMs"/>'s clock.</param>
/// <param name="Level">How serious.</param>
/// <param name="Source">Who said it: a logger's category, <c>browser</c>, or <c>unhandled</c>.</param>
/// <param name="Message">What it said, cut at 500 characters.</param>
/// <param name="Exception">The exception's type and message, when there was one.</param>
public sealed record StepLogEntry(long AtMs, StepLogLevel Level, string Source, string Message, string? Exception);

/// <summary>
/// What the app said, kept by the step it said it in (<see cref="TestScope"/>), so a step's result can show it: an
/// agent reading a failure sees the app's error beside the step. In memory, bounded: the newest 500 entries of the
/// process, 50 a step, each message 500 characters. The app's logs come through an <c>ILoggerProvider</c>
/// (Bruinet.Logging), the browser's console through the Playwright context, exceptions nothing caught through
/// <see cref="Watch"/>.
/// </summary>
public static class StepLog
{
    /// <summary>Names the level kept: <c>all</c> keeps information too.</summary>
    public const string Variable = "BRUINET_STEP_LOG";

    private const int Kept = 500;
    private const int KeptPerStep = 50;
    private const int MessageKept = 500;

    private static readonly object Gate = new();
    private static readonly LinkedList<(TestScope Scope, StepLogEntry Entry)> Entries = [];
    private static int _watching;

    /// <summary>The lowest level kept.</summary>
    public static StepLogLevel Minimum { get; } = string.Equals(Environment.GetEnvironmentVariable(Variable), "all", StringComparison.OrdinalIgnoreCase)
        ? StepLogLevel.Information
        : StepLogLevel.Warning;

    /// <summary>Keeps what the app said, under the step that runs now; below <see cref="Minimum"/> it is dropped.</summary>
    /// <param name="level">How serious.</param>
    /// <param name="source">Who said it.</param>
    /// <param name="message">What it said.</param>
    /// <param name="exception">The exception, when there was one.</param>
    public static void Add(StepLogLevel level, string source, string message, Exception? exception = null)
    {
        if (level < Minimum)
        {
            return;
        }

        var entry = new StepLogEntry(
            Timing.NowMs,
            level,
            source,
            message.Length > MessageKept ? message[..MessageKept] + "…" : message,
            exception is null ? null : $"{exception.GetType().Name}: {exception.Message}");
        lock (Gate)
        {
            // The step that runs now on this flow, else the one entered last (an app's UI thread has no flow of ours).
            Entries.AddLast((TestScope.Current.Step is not null ? TestScope.Current : TestScope.Latest, entry));
            if (Entries.Count > Kept)
            {
                Entries.RemoveFirst();
            }
        }
    }

    /// <summary>What was said during one step of a test or scenario, oldest first (an entry made where no scenario was entered, as in a host's runner, counts for any).</summary>
    /// <param name="test">The test, as <see cref="TestScope.Test"/> names it; null for any.</param>
    /// <param name="scenario">The scenario, as <see cref="TestScope.Scenario"/> names it.</param>
    /// <param name="step">The step, as <see cref="TestScope.Step"/> names it.</param>
    public static IReadOnlyList<StepLogEntry> Of(string? test, string? scenario, string? step)
    {
        lock (Gate)
        {
            return [.. Entries
                .Where(item => (test is null || item.Scope.Test == test)
                    && (item.Scope.Scenario is null || item.Scope.Scenario == scenario)
                    && item.Scope.Step == step)
                .Select(item => item.Entry)
                .TakeLast(KeptPerStep)];
        }
    }

    /// <summary>Keeps the exceptions nothing caught (on any thread, and in tasks nobody awaited), from now on; once a process.</summary>
    public static void Watch()
    {
        if (Interlocked.Exchange(ref _watching, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Add(StepLogLevel.Error, "unhandled", exception.Message, exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) => Add(StepLogLevel.Error, "unhandled", args.Exception.GetBaseException().Message, args.Exception.GetBaseException());
    }
}
