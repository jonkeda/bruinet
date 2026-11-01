namespace Bruinet.Bdd;

public sealed class BddExecutionContext
{
    // The fixture's own app, before any switch, is the slot with the empty name.
    private readonly Dictionary<string, AppState> _apps = new(StringComparer.OrdinalIgnoreCase);
    private AppState _app = new();

    /// <summary>The current app's current page.</summary>
    public string? CurrentPageName
    {
        get => _app.CurrentPageName;
        set => _app.CurrentPageName = value;
    }

    /// <summary>The app the scenario is using, after a switch to one; null before the first.</summary>
    public string? CurrentSession { get; private set; }

    /// <summary>The fixture that starts the scenario's apps, when it hosts several.</summary>
    public IBddSessionHost? SessionHost { get; set; }

    /// <summary>What relative dates (<c>today</c>, <c>+2d</c>) resolve against; <see cref="Clock"/>, then the current time, when null.</summary>
    public DateTimeOffset? Now { get; set; }

    /// <summary>The fixture's pinned clock, read only when a step needs the time.</summary>
    public IBddClock? Clock { get; set; }

    /// <summary>The config's <c>Apps</c> table, which user arguments are read against.</summary>
    public IReadOnlyList<BddAppConfig> Apps { get; set; } = [];

    /// <summary>Values that hold for the whole scenario, whichever app is current.</summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Values of the current app only, such as its page and its services.</summary>
    public IDictionary<string, object?> AppItems => _app.Items;

    public IList<string> Diagnostics { get; } = [];

    /// <summary>Where the steps stop for the debugger; the IDE's step breakpoints while one is attached, null otherwise.</summary>
    public IBddBreakHook? Breaks { get; set; } = BddBreakpoints.ForDebugger();

    /// <summary>What sets the run's pace, such as a narrated recording; null to run at full speed.</summary>
    public IBddStepPacer? Pacer { get; set; }

    /// <summary>
    /// Makes <paramref name="app"/> current: the host makes it the app page objects drive, and its
    /// own page and services come back. Nothing happens when it is current already.
    /// </summary>
    /// <param name="app">The app's name, as in the <c>Apps</c> table.</param>
    public void SwitchApp(string app)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        if (string.Equals(CurrentSession, app, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var host = SessionHost ?? throw new InvalidOperationException(
            $"The scenario switches to the app '{app}', but its fixture does not host apps (IBddSessionHost).");
        var services = host.UseSession(app);

        _apps[CurrentSession ?? string.Empty] = _app;
        _app = _apps.TryGetValue(app, out var state) ? state : new AppState();
        CurrentSession = app;
        BddStepDefinitionRuntime.SetServiceProvider(this, services);
        Diagnostics.Add($"session:{app}");
    }

    private sealed class AppState
    {
        public string? CurrentPageName { get; set; }

        public Dictionary<string, object?> Items { get; } = new(StringComparer.Ordinal);
    }
}

public enum BddStepResultStatus
{
    Waiting,
    Running,
    Passed,
    Failed,
    Skipped,
    Canceled
}

public sealed record BddStepResult(
    BddStepResultStatus Status,
    BddStepInvocation Invocation,
    string? Message = null,
    Exception? Exception = null)
{
    /// <summary>How long the step ran; null for a step that did not run (skipped, canceled before it began).</summary>
    public TimeSpan? Duration { get; init; }

    public static BddStepResult Passed(BddStepInvocation invocation, string? message = null)
    {
        return new BddStepResult(BddStepResultStatus.Passed, invocation, message);
    }

    public static BddStepResult Failed(BddStepInvocation invocation, string message, Exception? exception = null)
    {
        return new BddStepResult(BddStepResultStatus.Failed, invocation, message, exception);
    }

    public static BddStepResult Canceled(BddStepInvocation invocation, string? message = null)
    {
        return new BddStepResult(BddStepResultStatus.Canceled, invocation, message);
    }
}

public sealed record BddScenarioRunResult(
    BddBoundScenario Scenario,
    IReadOnlyList<BddStepResult> Steps,
    BddSkipDecision? SkipDecision = null)
{
    public bool Skipped => SkipDecision?.ShouldSkip == true;

    public bool Passed => Steps.Count > 0 && Steps.All(x => x.Status == BddStepResultStatus.Passed);
}

public sealed class BddScenarioRunner
{
    private readonly BddExecutionContext _context;

    public BddScenarioRunner(BddExecutionContext? context = null)
    {
        _context = context ?? new BddExecutionContext();
    }

    public BddExecutionContext Context => _context;

    public BddStepExecutionSession CreateSession(BddBoundScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return new BddStepExecutionSession(_context, scenario);
    }

    public async Task<BddScenarioRunResult> RunAsync(
        BddBoundScenario scenario,
        CancellationToken cancellationToken = default)
    {
        var session = CreateSession(scenario);
        if (scenario.Hooks.Count > 0 && session.HasNext)
        {
            return await BddHooks.RunAsync(scenario, _context, session, cancellationToken).ConfigureAwait(false);
        }

        while (session.HasNext)
        {
            var result = await session.RunNextAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status is BddStepResultStatus.Failed or BddStepResultStatus.Canceled)
            {
                break;
            }
        }

        return session.ToResult();
    }

    public Task<BddScenarioRunResult> RunAsync(
        BddBoundScenario scenario,
        BddConfig config,
        Func<string, string?>? getEnvironmentVariable = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(config);

        var skipDecision = config.EvaluateSkip(scenario.Source.Tags, getEnvironmentVariable);
        return skipDecision.ShouldSkip
            ? Task.FromResult(new BddScenarioRunResult(scenario, [], skipDecision))
            : RunAsync(scenario, cancellationToken);
    }
}

public sealed class BddStepExecutionSession
{
    private readonly BddExecutionContext _context;
    private readonly List<BddStepResult> _results = [];
    private readonly BddBoundScenario _scenario;
    private int _nextIndex;

    public BddStepExecutionSession(BddExecutionContext context, BddBoundScenario scenario)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
    }

    public bool HasNext => _nextIndex < _scenario.Invocations.Count;

    public IReadOnlyList<BddStepResult> Results => _results;

    /// <summary>The step <see cref="RunNextAsync"/> runs next; null when none remain.</summary>
    public BddStepInvocation? Next => HasNext ? _scenario.Invocations[_nextIndex] : null;

    /// <summary>
    /// Passes over the next step without running it: its result is <see cref="BddStepResultStatus.Skipped"/>.
    /// For a tool that goes on from a later step, when the app is already where the skipped ones lead.
    /// </summary>
    /// <param name="message">Why it was skipped.</param>
    public BddStepResult SkipNext(string? message = null)
    {
        if (!HasNext)
        {
            throw new InvalidOperationException("No BDD steps remain in this execution session.");
        }

        var skipped = new BddStepResult(BddStepResultStatus.Skipped, _scenario.Invocations[_nextIndex++], message);
        _results.Add(skipped);
        return skipped;
    }

    public async Task<BddStepResult> RunNextAsync(CancellationToken cancellationToken = default)
    {
        if (!HasNext)
        {
            throw new InvalidOperationException("No BDD steps remain in this execution session.");
        }

        var invocation = _scenario.Invocations[_nextIndex++];
        if (cancellationToken.IsCancellationRequested)
        {
            var canceled = BddStepResult.Canceled(invocation, "Execution was canceled before the step started.");
            _results.Add(canceled);
            return canceled;
        }

        // A local, not the property, on the line that runs it: F11 there goes into the step's code
        // without Visual Studio's "automatic step-over of a property" notice.
        var handler = invocation.Command.Handler;
        if (handler is null)
        {
            var missingHandler = BddStepResult.Failed(
                invocation,
                $"Command '{invocation.CommandId}' does not have an execution handler.");
            _results.Add(missingHandler);
            return missingHandler;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (invocation.App is { } app)
            {
                _context.SwitchApp(app);
            }

            if (_context.Breaks?.Before(invocation, _context) is { } stepBreak)
            {
                // A step breakpoint: stepBreak shows the step, its arguments, app and page; F11 on the
                // next line goes into its step definition (R2).
                _context.Breaks.Break(stepBreak);
            }

            if (_context.Pacer is { } pacer)
            {
                await pacer.BeforeAsync(invocation, _context, cancellationToken).ConfigureAwait(false);
            }

            BddStepResult result;
            using (Bruinet.Core.Testing.TestScope.EnterStep($"{invocation.Step.Source.LineNumber}: {invocation.Step.Text}"))
            using (Bruinet.Core.Logging.Timing.Span("step", invocation.Step.Text))
            {
                result = NameTheSession(await handler(_context, invocation, cancellationToken).ConfigureAwait(false)) with { Duration = clock.Elapsed };
            }

            BreakOnFailure(invocation, result);
            _results.Add(result);
            await PaceAfterAsync(result, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var canceled = BddStepResult.Canceled(invocation, "Execution was canceled.");
            _results.Add(canceled);
            return canceled;
        }
        catch (Exception ex)
        {
            var failed = NameTheSession(BddStepResult.Failed(invocation, ex.Message, ex)) with { Duration = clock.Elapsed };
            BreakOnFailure(invocation, failed);
            _results.Add(failed);
            await PaceAfterAsync(failed, cancellationToken).ConfigureAwait(false);
            return failed;
        }
    }

    // A failed step is paced too: a recording shows it failing, and its message.
    private Task PaceAfterAsync(BddStepResult result, CancellationToken cancellationToken)
        => _context.Pacer?.AfterAsync(result, _context, cancellationToken) ?? Task.CompletedTask;

    // Break on failure: the failed step's result, with its exception, before the scenario ends.
    private void BreakOnFailure(BddStepInvocation invocation, BddStepResult result)
    {
        if (result.Status == BddStepResultStatus.Failed && _context.Breaks is { } breaks && breaks.AfterFailure(result))
        {
            breaks.Break(BddBreakpoints.Describe(invocation, _context, result));
        }
    }

    // A hook failed before the first step: the scenario fails there, and no step runs.
    internal void Fail(string message)
    {
        _results.Add(BddStepResult.Failed(_scenario.Invocations[_nextIndex], message));
        _nextIndex = _scenario.Invocations.Count;
    }

    // A hook failed after a step: that step's result says so.
    internal void FailLast(string problem)
    {
        if (_results.Count == 0)
        {
            return;
        }

        var last = _results[^1];
        _results[^1] = last.Status == BddStepResultStatus.Passed
            ? BddStepResult.Failed(last.Invocation, $"The step passed, but {problem}")
            : last with { Message = $"{last.Message} Also, {problem}" };
    }

    // In a scenario that uses several apps, a failure says which one it came from.
    private BddStepResult NameTheSession(BddStepResult result)
        => result.Status == BddStepResultStatus.Failed && _context.CurrentSession is { } session
            ? result with { Message = $"[{session}] {result.Message}" }
            : result;

    public BddScenarioRunResult ToResult()
    {
        return new BddScenarioRunResult(_scenario, _results);
    }
}
