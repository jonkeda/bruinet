using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Microsoft.Maui.Dispatching;

namespace MauiHeadless.Hosting;

/// <summary>
/// The dispatcher of one test context: a queue drained on the test's own thread at every MauiHeadless call,
/// and a virtual clock for delayed work and timers. Nothing runs by itself and nothing waits on the
/// wall clock, so a test sees the same order of work on every run.
/// </summary>
/// <remarks>
/// Bindings never need to dispatch (<see cref="IsDispatchRequired"/> is false), so a view model's
/// property change reaches its label at once. Work an app dispatches itself, async continuations
/// and async void handlers run at the next drain: the end of a render or an interaction, a query,
/// an assertion, or a wait.
/// </remarks>
public sealed class TestDispatcher : IDispatcher
{
    [ThreadStatic] private static TestDispatcher? s_running;

    private readonly object _gate = new();
    private readonly Queue<Action> _queue = new();
    private readonly List<Scheduled> _scheduled = [];
    private readonly SemaphoreSlim _signal = new(0);
    private readonly TestSynchronizationContext _synchronizationContext;
    private long _sequence;
    private bool _draining;

    public TestDispatcher()
    {
        _synchronizationContext = new TestSynchronizationContext(this);
    }

    /// <summary>The dispatcher whose work runs on this thread now, if any.</summary>
    internal static TestDispatcher? Running => s_running;

    /// <summary>Called when this thread starts running the dispatcher's work, outermost entry only; its result is disposed when it stops.</summary>
    internal Func<IDisposable?>? OnEnter { get; set; }

    /// <summary>The virtual time: advanced only by <see cref="AdvanceTime"/>.</summary>
    public TimeSpan Now { get; private set; }

    /// <summary>Whether work is queued, delayed work not counted.</summary>
    public bool HasPendingWork
    {
        get { lock (_gate) return _queue.Count > 0; }
    }

    /// <inheritdoc />
    public bool IsDispatchRequired => false;

    /// <inheritdoc />
    public bool Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enqueue(action);
        return true;
    }

    /// <inheritdoc />
    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Schedule(delay, action);
        return true;
    }

    /// <inheritdoc />
    public IDispatcherTimer CreateTimer() => new TestDispatcherTimer(this);

    /// <summary>Runs an action as the UI thread would, then drains what it queued.</summary>
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var _ = Enter();
        action();
        Drain();
    }

    /// <summary>Runs a function as the UI thread would, then drains what it queued.</summary>
    public T Run<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        using var _ = Enter();
        var result = function();
        Drain();
        return result;
    }

    /// <summary>Runs the queued work, and the work it queues, until the queue is empty.</summary>
    public void RunPending()
    {
        using var _ = Enter();
        Drain();
    }

    /// <summary>
    /// Moves the virtual clock forward, running delayed work and timer ticks in due order, each
    /// followed by the work it queued.
    /// </summary>
    public void AdvanceTime(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(by), by, "Time only moves forward.");

        using var _ = Enter();
        Drain();
        var until = Now + by;
        while (TakeNextDue(until) is { } next)
        {
            Now = next.Due;
            next.Action();
            Drain();
        }

        Now = until;
    }

    /// <summary>
    /// Drains the queue until <paramref name="condition"/> holds, waiting on the wall clock only for
    /// work that real asynchrony (a runtime timer, I/O) has yet to post back.
    /// </summary>
    /// <returns>Whether the condition held within <paramref name="timeout"/>.</returns>
    public bool WaitFor(Func<bool> condition, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var clock = Stopwatch.StartNew();
        while (true)
        {
            // The wait itself is outside the scope, so other contexts are not held up by it.
            using (Enter())
            {
                Drain();
                if (condition())
                    return true;
            }

            var left = timeout - clock.Elapsed;
            if (left <= TimeSpan.Zero)
                return false;

            _signal.Wait(left);
        }
    }

    /// <summary>
    /// Runs the queued work until no new work has arrived for <paramref name="quiet"/>: the app has
    /// settled, its awaited I/O answered and continued. Within <paramref name="timeout"/> on the wall
    /// clock; virtual time does not move.
    /// </summary>
    /// <returns>Whether it settled within the timeout.</returns>
    public bool WaitForQuiet(TimeSpan quiet, TimeSpan timeout)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            using (Enter())
            {
                Drain();
            }

            var left = timeout - clock.Elapsed;
            if (left <= TimeSpan.Zero)
                return !HasPendingWork;

            if (!_signal.Wait(left < quiet ? left : quiet))
                return true;
        }
    }

    internal void Enqueue(Action action)
    {
        lock (_gate)
            _queue.Enqueue(action);
        _signal.Release();
    }

    internal object Schedule(TimeSpan delay, Action action)
    {
        var entry = new Scheduled(Now + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay), action);
        lock (_gate)
        {
            entry.Sequence = ++_sequence;
            _scheduled.Add(entry);
        }

        return entry;
    }

    internal void Cancel(object scheduled)
    {
        lock (_gate)
            _scheduled.Remove((Scheduled)scheduled);
    }

    private Scheduled? TakeNextDue(TimeSpan until)
    {
        lock (_gate)
        {
            Scheduled? next = null;
            foreach (var entry in _scheduled)
            {
                if (entry.Due <= until
                    && (next is null || entry.Due < next.Due || (entry.Due == next.Due && entry.Sequence < next.Sequence)))
                    next = entry;
            }

            if (next is not null)
                _scheduled.Remove(next);
            return next;
        }
    }

    private void Drain()
    {
        // A nested drain (an interaction from inside a handler) leaves the work to the outer loop.
        if (_draining)
            return;

        _draining = true;
        try
        {
            while (true)
            {
                Action? action;
                lock (_gate)
                {
                    if (!_queue.TryDequeue(out action))
                        return;
                }

                action();
            }
        }
        finally
        {
            _draining = false;
        }
    }

    private Scope Enter() => new(this);

    private readonly struct Scope : IDisposable
    {
        private readonly TestDispatcher? _previous;
        private readonly SynchronizationContext? _previousContext;
        private readonly IDisposable? _entered;

        public Scope(TestDispatcher dispatcher)
        {
            _previous = s_running;
            _previousContext = SynchronizationContext.Current;
            _entered = _previous == dispatcher ? null : dispatcher.OnEnter?.Invoke();
            s_running = dispatcher;
            SynchronizationContext.SetSynchronizationContext(dispatcher._synchronizationContext);
        }

        public void Dispose()
        {
            s_running = _previous;
            SynchronizationContext.SetSynchronizationContext(_previousContext);
            _entered?.Dispose();
        }
    }

    private sealed class Scheduled(TimeSpan due, Action action)
    {
        public TimeSpan Due { get; } = due;
        public Action Action { get; } = action;
        public long Sequence { get; set; }
    }

    /// <summary>
    /// Posts continuations to the dispatcher's queue, so an awaited task resumes on the test's thread
    /// at the next drain, and an async void handler's exception fails the test instead of the process.
    /// </summary>
    private sealed class TestSynchronizationContext(TestDispatcher dispatcher) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => dispatcher.Enqueue(() => d(state));

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (s_running == dispatcher)
            {
                d(state);
                return;
            }

            ExceptionDispatchInfo? failure = null;
            using var done = new ManualResetEventSlim();
            dispatcher.Enqueue(() =>
            {
                try
                { d(state); }
                catch (Exception e) { failure = ExceptionDispatchInfo.Capture(e); }
                finally { done.Set(); }
            });
            done.Wait();
            failure?.Throw();
        }

        public override SynchronizationContext CreateCopy() => this;
    }
}
