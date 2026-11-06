using System.Diagnostics;
using System.Drawing;

namespace Bruinet.Core.Recording;

/// <summary>What a test did to the app, as a recorded video shows it.</summary>
public enum InteractionKind
{
    /// <summary>A tap or click: a button, a toggle, a row, a choice.</summary>
    Tap,

    /// <summary>Two taps.</summary>
    DoubleTap,

    /// <summary>A press held for a while.</summary>
    LongPress,

    /// <summary>A secondary click (Html, desktop).</summary>
    RightClick,

    /// <summary>The pointer over an element, nothing pressed (Html, desktop).</summary>
    Hover,

    /// <summary>A swipe or drag from one point to another; pull to refresh is one.</summary>
    Swipe,

    /// <summary>Text typed into a field.</summary>
    Type,

    /// <summary>A field emptied.</summary>
    Clear,

    /// <summary>A key or a navigation without a target: Enter, Back, Menu.</summary>
    Key,
}

/// <summary>
/// One interaction, taken just before it ran: its target's bounds while the screen still showed it.
/// Coordinates are the driver's own (screen, device or viewport pixels); the recording maps them to
/// the video.
/// </summary>
/// <param name="Kind">What was done.</param>
/// <param name="At">When, on the <see cref="Stopwatch"/> clock the recorders use.</param>
/// <param name="Bounds">The target's rectangle; empty for a <see cref="InteractionKind.Key"/> without one.</param>
public sealed record Interaction(InteractionKind Kind, long At, Rectangle Bounds)
{
    /// <summary>Where a swipe starts; null for anything else.</summary>
    public Point? From { get; init; }

    /// <summary>Where a swipe ends; null for anything else.</summary>
    public Point? To { get; init; }

    /// <summary>How long it lasted (a long press, a real swipe); zero when instant.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>The target's AutomationId or name, for reading the timeline; never drawn.</summary>
    public string? Label { get; init; }

    /// <summary>The key's name for a <see cref="InteractionKind.Key"/>, e.g. <c>Enter</c> or <c>Back</c>.</summary>
    public string? Key { get; init; }
}

/// <summary>Where a recording keeps the interactions it is told about.</summary>
public interface IInteractionLog
{
    /// <summary>Keeps one interaction; called from the test's thread.</summary>
    /// <param name="interaction">The interaction.</param>
    void Add(Interaction interaction);
}

/// <summary>
/// The interactions of the scenes being recorded, one log per recording target, so two
/// collections recording at once keep their taps apart. A driver's element reports each action
/// here just before it runs it, naming its target where it knows it; with no recording,
/// <see cref="Log"/> is null and a report costs a null check (AD-024: a run that does not record
/// pays nothing).
/// </summary>
public static class Interactions
{
    private static readonly object Gate = new();

    // The logs by target. Copied on change and read without the lock: a report reads it once.
    private static Dictionary<RecordingTarget, IInteractionLog> _logs = [];

    // An action that runs another (a double tap as a gesture, an Invoke as a Click) is one interaction.
    [ThreadStatic]
    private static int _depth;

    /// <summary>The log of the only scene being recorded; null when none, or several, record.</summary>
    public static IInteractionLog? Log => Volatile.Read(ref _logs) is { Count: 1 } logs ? logs.Values.First() : null;

    /// <summary>
    /// Sends the interactions on <paramref name="target"/> to <paramref name="log"/> until the
    /// returned scope is disposed.
    /// </summary>
    /// <param name="target">What the recording shows.</param>
    /// <param name="log">The recording's log.</param>
    public static IDisposable Begin(RecordingTarget target, IInteractionLog log)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(log);
        lock (Gate)
        {
            _logs = new Dictionary<RecordingTarget, IInteractionLog>(_logs) { [target] = log };
        }

        return new Restore(target, log);
    }

    // A report's log: its target's, or the only one there is when it names none. Null when nothing
    // records, and when several record and the report cannot say which it belongs to.
    private static IInteractionLog? LogFor(Func<RecordingTarget?>? target)
    {
        var logs = Volatile.Read(ref _logs);
        if (logs.Count == 0)
        {
            return null;
        }

        if (target is not null && TryRead(target, out var named) && named is not null)
        {
            return logs.GetValueOrDefault(named);
        }

        return logs.Count == 1 ? logs.Values.First() : null;
    }

    /// <summary>
    /// Reports an action about to run, unless another action is already reporting it. Dispose the
    /// scope when the action has run. Nothing is read when nothing records.
    /// </summary>
    /// <param name="kind">What is done.</param>
    /// <param name="bounds">Reads the target's rectangle; only called while recording.</param>
    /// <param name="label">Reads the target's AutomationId or name; only called while recording.</param>
    /// <param name="from">Where a swipe starts.</param>
    /// <param name="to">Where a swipe ends.</param>
    /// <param name="duration">How long the action lasts.</param>
    /// <param name="key">The key's name, for <see cref="InteractionKind.Key"/>.</param>
    /// <param name="target">Reads what the action is shown on (the browser page, the device); only called while recording.</param>
    public static Scope Report(InteractionKind kind, Func<Rectangle> bounds, Func<string?>? label = null, Point? from = null, Point? to = null, TimeSpan duration = default, string? key = null, Func<RecordingTarget?>? target = null)
        => Report(kind, bounds, label, _ => (from, to), duration, key, target);

    /// <summary>
    /// Reports a swipe that has a direction but no points (a bridge's <c>SwipeDown</c>): it is drawn
    /// across the middle half of its target, in that direction.
    /// </summary>
    /// <param name="bounds">Reads the target's rectangle; only called while recording.</param>
    /// <param name="dx">-1, 0 or 1: left or right.</param>
    /// <param name="dy">-1, 0 or 1: up or down.</param>
    /// <param name="label">Reads the target's AutomationId or name; only called while recording.</param>
    /// <param name="target">Reads what the action is shown on; only called while recording.</param>
    public static Scope ReportSwipe(Func<Rectangle> bounds, int dx, int dy, Func<string?>? label = null, Func<RecordingTarget?>? target = null)
        => Report(InteractionKind.Swipe, bounds, label, rectangle => (Across(rectangle, -dx, -dy), Across(rectangle, dx, dy)), default, null, target);

    private static Scope Report(InteractionKind kind, Func<Rectangle> bounds, Func<string?>? label, Func<Rectangle, (Point? From, Point? To)> path, TimeSpan duration, string? key, Func<RecordingTarget?>? target)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (LogFor(target) is not { } log)
        {
            return default;
        }

        if (_depth++ == 0 && TryRead(bounds, out var rectangle))
        {
            var (from, to) = path(rectangle);
            var name = label is not null && TryRead(label, out var read) ? read : null;
            log.Add(new Interaction(kind, Stopwatch.GetTimestamp(), rectangle) { From = from, To = to, Duration = duration, Label = name, Key = key });
        }

        return new Scope(counted: true);
    }

    // A point a quarter of the way out from the centre, in a direction.
    private static Point Across(Rectangle bounds, int dx, int dy)
        => new(bounds.X + (bounds.Width / 2) + (dx * bounds.Width / 4), bounds.Y + (bounds.Height / 2) + (dy * bounds.Height / 4));

    // A target that cannot be read (the element went stale) leaves no mark: a recording never fails a step.
    private static bool TryRead<T>(Func<T> read, out T value)
    {
        try
        {
            value = read();
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine($"Interactions: the target could not be read ({exception.Message}); no mark.");
            value = default!;
            return false;
        }
    }

    /// <summary>
    /// An action being reported; dispose it once the action has run. Disposing it also ends what the
    /// running attempt knew about the app (<see cref="Calls.AttemptMemo.Forget"/>), recorded or not.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        private readonly bool _counted;

        internal Scope(bool counted) => _counted = counted;

        /// <inheritdoc />
        public void Dispose()
        {
            Calls.AttemptMemo.Forget();

            if (_counted)
            {
                _depth--;
            }
        }
    }

    private sealed class Restore(RecordingTarget target, IInteractionLog log) : IDisposable
    {
        public void Dispose()
        {
            lock (Gate)
            {
                if (_logs.TryGetValue(target, out var current) && ReferenceEquals(current, log))
                {
                    var logs = new Dictionary<RecordingTarget, IInteractionLog>(_logs);
                    logs.Remove(target);
                    _logs = logs;
                }
            }
        }
    }
}
