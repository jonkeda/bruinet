using Bruinet.Core.Testing;

namespace Bruinet.Core.Logging;

/// <summary>A level of what the app said: a warning and up are kept, information only when asked for.</summary>
public enum StepLogLevel
{
    /// <summary>Information, kept with <c>BRUINET_STEP_LOG=all</c>.</summary>
    Information,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error.</summary>
    Error,
}

/// <summary>What the app said during a step: its log, or the browser's console.</summary>
/// <param name="AtMs">When, on <see cref="Timing.NowMs"/>'s clock.</param>
/// <param name="Level">How serious.</param>
/// <param name="Source">Who said it: a logger's category, or <c>browser</c>.</param>
/// <param name="Message">What it said, cut at 500 characters.</param>
/// <param name="Exception">The exception's type and message, cut at 500 characters, when there was one.</param>
public sealed record StepLogEntry(long AtMs, StepLogLevel Level, string Source, string Message, string? Exception);

/// <summary>
/// What the app said, for the step that made it say it: a step takes a <see cref="Mark"/> when it starts and its
/// entries with <see cref="Since"/> when it ends, so its result can show them (an agent reading a failure sees the
/// app's error beside the step). An entry is the step's when it was said on the step's own flow, or said where no test
/// flow reaches (the app's UI thread, the browser's events), which <see cref="TestScope.Latest"/> files. In memory,
/// the newest 500 entries of the process; each text 500 characters. The app's logs come through an
/// <c>ILoggerProvider</c> (Bruinet.Logging), the browser's console through the Playwright context.
/// <c>BRUINET_STEP_LOG</c>: <c>all</c> keeps information too, <c>off</c> keeps nothing.
/// </summary>
public static class StepLog
{
    /// <summary>Names the level kept: <c>all</c> keeps information too, <c>off</c> nothing.</summary>
    public const string Variable = "BRUINET_STEP_LOG";

    private const int Kept = 500;
    private const int TextKept = 500;

    private static readonly object Gate = new();
    private static readonly LinkedList<(long Number, TestScope Scope, StepLogEntry Entry)> Entries = [];
    private static long _number;

    /// <summary>Whether anything is kept: not with <c>BRUINET_STEP_LOG=off</c>.</summary>
    public static bool Enabled { get; } = !string.Equals(Environment.GetEnvironmentVariable(Variable), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>The lowest level kept.</summary>
    public static StepLogLevel Minimum { get; } = string.Equals(Environment.GetEnvironmentVariable(Variable), "all", StringComparison.OrdinalIgnoreCase)
        ? StepLogLevel.Information
        : StepLogLevel.Warning;

    /// <summary>
    /// Keeps what the app said; below <see cref="Minimum"/> it is dropped. <paramref name="elsewhere"/>: said where no
    /// test flow reaches (a browser's event loop), filed under the scope entered last.
    /// </summary>
    /// <param name="level">How serious.</param>
    /// <param name="source">Who said it.</param>
    /// <param name="message">What it said.</param>
    /// <param name="exception">The exception, when there was one.</param>
    /// <param name="elsewhere">Said off the test's flow, whatever scope that flow carried.</param>
    public static void Add(StepLogLevel level, string source, string message, Exception? exception = null, bool elsewhere = false)
    {
        if (!Enabled || level < Minimum)
        {
            return;
        }

        var entry = new StepLogEntry(
            Timing.NowMs,
            level,
            Cut(source),
            Cut(message),
            exception is null ? null : Cut($"{exception.GetType().Name}: {exception.Message}"));

        // On a flow with no step (the app's UI thread), it is said elsewhere too: the step entered last is its.
        var scope = elsewhere || TestScope.Current.Step is null ? TestScope.Latest : TestScope.Current;
        lock (Gate)
        {
            Entries.AddLast((++_number, scope, entry));
            if (Entries.Count > Kept)
            {
                Entries.RemoveFirst();
            }
        }
    }

    /// <summary>Where the log stands now: a step takes it when it starts.</summary>
    public static long Mark()
    {
        lock (Gate)
        {
            return _number;
        }
    }

    /// <summary>
    /// What was said since <paramref name="mark"/> for <paramref name="step"/> (the very scope, not one alike): on its flow,
    /// an async continuation it started included, or elsewhere while it was the scope entered last. Oldest first.
    /// </summary>
    /// <param name="mark">The step's <see cref="Mark"/>.</param>
    /// <param name="step">The step's scope, <see cref="TestScope.Current"/> while it ran.</param>
    public static IReadOnlyList<StepLogEntry> Since(long mark, TestScope step)
    {
        ArgumentNullException.ThrowIfNull(step);
        lock (Gate)
        {
            return [.. Entries
                .Where(item => item.Number > mark && ReferenceEquals(item.Scope, step))
                .Select(item => item.Entry)];
        }
    }

    private static string Cut(string text) => text.Length > TextKept ? text[..TextKept] + "…" : text;
}
