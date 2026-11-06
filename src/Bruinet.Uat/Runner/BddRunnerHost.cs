using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Channels;
using Bruinet.Bdd.Runner;
using Bruinet.Narrator;
using Bruinet.Recording;

namespace Bruinet.Uat.Runner;

/// <summary>
/// The runner's end of the protocol: opens a scenario of the suite it was started in, runs it a
/// step at a time as a host asks, and paces or records it. See AD-025.
/// </summary>
public sealed class BddRunnerHost
{
    private static readonly Lazy<ScreenRecorders> Recorders = new(ScreenRecorders.Discover);
    private readonly BddRunnerChannel _channel;
    private readonly object _stepLock = new();
    private CancellationTokenSource? _step;

    // Requests are numbered as they are read. A cancel covers every request read before it, so a
    // step still queued behind the reading loop starts canceled instead of missing the cancel.
    private long _read;
    private long _canceledThrough;
    private OpenScenario? _open;

    // A fixture kept for the next open of the same suite: a prepared one, or one that starts a
    // scenario clean without a launch (AD-025).
    private KeptFixture? _kept;
    private BddSceneRecording? _recording;
    private BddNarrationPacer? _rehearsal;

    /// <summary>Creates the host over a connected channel.</summary>
    /// <param name="channel">The channel to the host process.</param>
    public BddRunnerHost(BddRunnerChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>Says hello, then answers requests until the host says exit or goes away.</summary>
    /// <param name="cancellationToken">Ends the loop.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _channel.SendAsync(new BddRunnerHello(BddRunnerProtocol.Version), cancellationToken).ConfigureAwait(false);

        // Requests are answered one at a time, off the reading loop: a cancel has to be read
        // while the step it cancels is running.
        var requests = Channel.CreateUnbounded<(BddRunnerMessage Message, long Number)>();
        var worker = Task.Run(() => AnswerAsync(requests.Reader, cancellationToken), cancellationToken);
        try
        {
            while (await _channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } message and not BddRunnerExit)
            {
                if (message is BddRunnerCancel)
                {
                    CancelStep();
                }
                else
                {
                    long number;
                    lock (_stepLock)
                    {
                        number = ++_read;
                    }

                    requests.Writer.TryWrite((message, number));
                }
            }
        }
        finally
        {
            CancelStep();
            requests.Writer.TryComplete();
            await worker.ConfigureAwait(false);
            await DropRecordingAsync().ConfigureAwait(false);
            Close();
        }
    }

    private async Task AnswerAsync(ChannelReader<(BddRunnerMessage Message, long Number)> requests, CancellationToken cancellationToken)
    {
        await foreach (var (request, number) in requests.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            BddRunnerMessage answer;
            try
            {
                answer = await AnswerAsync(request, number).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                answer = new BddRunnerFailed(Describe(exception));
            }

            await _channel.SendAsync(answer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<BddRunnerMessage> AnswerAsync(BddRunnerMessage request, long number)
    {
        switch (request)
        {
            case BddRunnerOpen open:
                await DropRecordingAsync().ConfigureAwait(false);
                Close(keep: true);
                _open = OpenScenario.Open(open, TakeKept(open.Config, open.Pages));
                return new BddRunnerOpened(_open.Scenario.Invocations.Count, _open.Discovery, _open.Catalog, Position(_open.Session.Next, _open.Runner.Context), _open.WhySkipped);

            case BddRunnerNext:
                return await StepAsync(Opened(), number).ConfigureAwait(false);

            case BddRunnerSkip skip:
                var session = Opened();
                var passedOver = session.Session.Next;
                var skipped = Step(session, session.Session.SkipNext(skip.Message), passedOver);
                session.RecordIfFinished();
                return skipped;

            case BddRunnerRecord record:
                await DropRecordingAsync().ConfigureAwait(false);
                var fixture = Opened().Fixture;
                var scene = new BddSceneRequest(record.Number, record.Name, record.Scenario, record.Folder, Clips(record.Clips), TimeSpan.FromMilliseconds(record.BeatMilliseconds))
                {
                    Hash = record.Hash,
                    Pacing = new BddScenePacing(
                        record.PauseMilliseconds is { } pause ? TimeSpan.FromMilliseconds(pause) : BddScenePacing.DefaultPause,
                        record.SayBefore ? BddSayMode.Before : BddSayMode.While),
                };
                _recording = new BddSceneRecording(scene, () => UatRecording.TargetOf(fixture), Recorders.Value, record.SpeakAloud ? SayAsync : null);
                Opened().Runner.Context.Pacer = _recording;
                return new BddRunnerDone();

            case BddRunnerRehearse rehearse:
                await DropRecordingAsync().ConfigureAwait(false);
                _rehearsal = new BddNarrationPacer(Clips(rehearse.Clips), TimeSpan.FromMilliseconds(rehearse.BeatMilliseconds), rehearse.SpeakAloud ? SayAsync : null);
                Opened().Runner.Context.Pacer = _rehearsal;
                return new BddRunnerDone();

            case BddRunnerFinishRecording:
                return await FinishRecordingAsync().ConfigureAwait(false);

            case BddRunnerDropRecording:
                await DropRecordingAsync().ConfigureAwait(false);
                return new BddRunnerDone();

            case BddRunnerScreenshot screenshot:
                return Screenshot(Opened(), screenshot.Description);

            case BddRunnerReadPage:
                return Opened().ReadPage();

            case BddRunnerBreak debug:
                return await BreakAsync(Opened(), debug).ConfigureAwait(false);

            case BddRunnerClose close:
                await DropRecordingAsync().ConfigureAwait(false);
                Close(close.Keep);
                return new BddRunnerDone();

            case BddRunnerPrepare prepare:
                if (_open is not null)
                {
                    return new BddRunnerFailed("A scenario is open: close it before preparing the fixture.");
                }

                // A fixture kept from the run before is ready already.
                if (_kept?.Same(prepare.Config, prepare.Pages) != true)
                {
                    DisposeKept();
                    _kept = new KeptFixture(prepare.Config, prepare.Pages, OpenScenario.MakeFixture(prepare.Config, prepare.Pages));
                }

                return new BddRunnerDone();

            default:
                return new BddRunnerFailed($"The runner does not answer a '{request.GetType().Name}'.");
        }
    }

    private OpenScenario Opened()
        => _open ?? throw new InvalidOperationException("No scenario is open.");

    private async Task<BddRunnerStep> StepAsync(OpenScenario open, long number)
    {
        var cancel = new CancellationTokenSource();
        lock (_stepLock)
        {
            _step = cancel;
            if (_canceledThrough >= number)
            {
                cancel.Cancel();
            }
        }

        try
        {
            var ran = open.Session.Next;
            var result = await open.Session.RunNextAsync(cancel.Token).ConfigureAwait(false);

            // A step that called no definition leaves no break for the next.
            BddInvoker.DisarmDefinitionBreak();
            var step = Step(open, result, ran);
            open.RecordIfFinished();
            return step;
        }
        finally
        {
            lock (_stepLock)
            {
                _step = null;
            }

            cancel.Dispose();
        }
    }

    private static BddRunnerStep Step(OpenScenario open, BddStepResult result, BddStepInvocation? ran)
        => new(
            open.Session.Results.Count - 1,
            result.Status,
            result.Message,
            open.Session.HasNext,
            [.. BddDiagnosticsFormatter.FormatSteps(open.Session.Results)],
            [.. open.Runner.Context.Diagnostics],
            Position(ran, open.Runner.Context),
            Position(open.Session.Next, open.Runner.Context));

    // Plain text and numbers: a host holds no engine types (AD-025). The page is the context's now:
    // for the next step, where the step before left the scenario.
    private static BddRunnerPosition? Position(BddStepInvocation? invocation, BddExecutionContext context)
    {
        if (invocation is null)
        {
            return null;
        }

        var details = BddBreakpoints.Describe(invocation, context);
        return new BddRunnerPosition(
            invocation.Step.Source.FilePath,
            invocation.Step.Source.LineNumber,
            invocation.Step.Text,
            invocation.Call?.Depth ?? 0,
            invocation.Call?.Chain.Select(frame => new BddRunnerCall(frame.Describe(), frame.Location.FilePath, frame.Location.LineNumber, frame.Depth)).ToList() ?? [])
        {
            Arguments = details.Arguments.Count == 0 ? null : details.Arguments.ToDictionary(),
            App = details.App,
            Page = details.Page,
        };
    }

    // D6: once a debugger is attached, the next step that runs breaks in it just before its step
    // definition is called, so Step into goes straight into the definition.
    private static async Task<BddRunnerMessage> BreakAsync(OpenScenario open, BddRunnerBreak request)
    {
        if (request.Launch && !System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Launch();
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(request.WaitSeconds);
        while (!System.Diagnostics.Debugger.IsAttached && DateTime.UtcNow < deadline)
        {
            await Task.Delay(AttachPoll).ConfigureAwait(false);
        }

        if (!System.Diagnostics.Debugger.IsAttached)
        {
            return new BddRunnerFailed($"No debugger attached to the runner (process {Environment.ProcessId}) within {request.WaitSeconds} s.");
        }

        open.Runner.Context.Breaks = new BreakOnce(open.Runner.Context);
        return new BddRunnerDone();
    }

    // How often a Debug from here looks for the debugger.
    private static readonly TimeSpan AttachPoll = TimeSpan.FromMilliseconds(100);

    // The next step that runs breaks; then the host's pauses are the only ones again.
    private sealed class BreakOnce(BddExecutionContext context) : IBddBreakHook
    {
        public BddStepBreak? Before(BddStepInvocation invocation, BddExecutionContext stepContext)
            => BddBreakpoints.Describe(invocation, stepContext);

        public bool AfterFailure(BddStepResult result) => false;

        // The engine calls this right after Before, through the context: only now is it let go. The
        // break itself is where the step's definition is called, past the engine's lookups.
        public void Break(BddStepBreak stepBreak)
        {
            context.Breaks = null;
            BddInvoker.ArmDefinitionBreak(stepBreak);
        }
    }

    // Between steps, while a host is paused: the runner is idle, so this never waits on a step.
    private static BddRunnerMessage Screenshot(OpenScenario open, string description)
    {
        if (UatScenarioResults.ScreenshotServiceOf(open.Fixture) is not { } screenshots)
        {
            return new BddRunnerFailed("The suite's fixture takes no screenshots.");
        }

        var path = screenshots.Capture("BddRunner", UatScenarioResults.EvidenceName(open.Scenario), description);
        return string.IsNullOrEmpty(path) ? new BddRunnerFailed("The screenshot was not written; the test log says why.") : new BddRunnerScreenshotTaken(path);
    }

    private void CancelStep()
    {
        lock (_stepLock)
        {
            _canceledThrough = _read;
            _step?.Cancel();
        }
    }

    private async Task<BddRunnerRecorded> FinishRecordingAsync()
    {
        if (_recording is { } recording)
        {
            _recording = null;
            ClearPacer();
            await using (recording.ConfigureAwait(false))
            {
                var timeline = await recording.FinishAsync().ConfigureAwait(false);
                return new BddRunnerRecorded(recording.Request.TimelinePath, [.. timeline.Warnings]);
            }
        }

        if (_rehearsal is { } rehearsal)
        {
            _rehearsal = null;
            ClearPacer();
            await rehearsal.FinishAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return new BddRunnerRecorded(null, []);
    }

    private async Task DropRecordingAsync()
    {
        _rehearsal = null;
        ClearPacer();
        if (_recording is { } recording)
        {
            _recording = null;
            await recording.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void ClearPacer()
    {
        if (_open is { } open)
        {
            open.Runner.Context.Pacer = null;
        }
    }

    // Closes the open scenario. With keep, a fixture that starts clean without a launch stays, its
    // app open, for the next open of the same suite; without, the kept one ends too.
    private void Close(bool keep = false)
    {
        if (_open is { } open)
        {
            _open = null;
            object fixture;
            try
            {
                fixture = open.Release();
            }
            catch
            {
                // AfterScenario failed: the fixture's app is in an unknown state, so it is not kept.
                End(open.Fixture);
                DisposeKept();
                throw;
            }

            if (keep && fixture is IUatScenarioLifecycle { StartsCleanWithoutLaunch: true })
            {
                DisposeKept();
                _kept = new KeptFixture(open.Config, open.Pages, fixture);
            }
            else
            {
                End(fixture);
            }
        }

        if (!keep)
        {
            DisposeKept();
        }
    }

    // The kept fixture, when it is the same suite's; another suite's ends.
    private object? TakeKept(string config, string pages)
    {
        if (_kept is { } kept && kept.Same(config, pages))
        {
            _kept = null;
            return kept.Fixture;
        }

        DisposeKept();
        return null;
    }

    private void DisposeKept()
    {
        End(_kept?.Fixture);
        _kept = null;
    }

    // A fixture ended as xunit ends one: DisposeAsync, then Dispose.
    private static void End(object? fixture)
    {
        try
        {
            (fixture as Xunit.IAsyncLifetime)?.DisposeAsync().GetAwaiter().GetResult();
        }
        finally
        {
            (fixture as IDisposable)?.Dispose();
        }
    }

    private sealed record KeptFixture(string Config, string Pages, object Fixture)
    {
        public bool Same(string config, string pages)
            => Path.GetFullPath(config).Equals(Path.GetFullPath(Config), StringComparison.OrdinalIgnoreCase)
               && Path.GetFileName(pages).Equals(Path.GetFileName(Pages), StringComparison.OrdinalIgnoreCase);
    }

    // The host plays the line: it has the voice's player, and a suite need not.
    private Task SayAsync(SpeechClip clip, CancellationToken cancellationToken)
        => clip.WavPath is { } wav ? _channel.SendAsync(new BddRunnerSay(wav, clip.Duration.TotalSeconds), cancellationToken) : Task.CompletedTask;

    private static BddSceneClips Clips(BddRunnerClips clips)
    {
        static SpeechClip? Clip(BddRunnerClip? clip) => clip is null ? null : new SpeechClip(clip.Text, clip.Wav, TimeSpan.FromSeconds(clip.Seconds));
        return new BddSceneClips(Clip(clips.Intro), clips.Says.ToDictionary(pair => pair.Key, pair => Clip(pair.Value)!), Clip(clips.Outro));
    }

    // Every message of the chain once, without reflection's own wrapper.
    private static string Describe(Exception exception)
    {
        List<string> messages = [];
        for (var current = exception; current is not null; current = current.InnerException)
        {
            // A message of one word is a bare name (a key, a folder, an assembly): its type says what of.
            var message = current.Message.Contains(' ', StringComparison.Ordinal) ? current.Message : $"{current.GetType().Name}: {current.Message}";
            if (current is not TargetInvocationException && !messages.Contains(message, StringComparer.Ordinal))
            {
                messages.Add(message);
            }
        }

        return messages.Count == 0 ? exception.Message : string.Join(Environment.NewLine, messages);
    }

    // A scenario with its fixture, bound and ready for its first step.
    private sealed class OpenScenario : IDisposable
    {
        private readonly IDisposable? _scope;

        private readonly UatRuntime _runtime;
        private readonly BddCommandCatalog _commands;
        private readonly string _mode;
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private bool _recorded;

        private OpenScenario(object fixture, BddScenarioRunner runner, BddBoundScenario scenario, IDisposable? scope, string discovery, string catalog, UatRuntime runtime, BddCommandCatalog commands, string mode, BddRunnerOpen open)
        {
            Config = open.Config;
            Pages = open.Pages;
            Fixture = fixture;
            Runner = runner;
            Scenario = scenario;
            Session = runner.CreateSession(scenario);
            _scope = scope;
            Discovery = discovery;
            Catalog = catalog;
            _runtime = runtime;
            _commands = commands;
            _mode = mode;
        }

        public object Fixture { get; }

        public string Config { get; }

        public string Pages { get; }

        public BddScenarioRunner Runner { get; }

        public BddBoundScenario Scenario { get; }

        public BddStepExecutionSession Session { get; }

        public string Discovery { get; }

        public string Catalog { get; }

        // Once its last step has run, or a step failed (where the gate stops too), the scenario's
        // result is written as the gate writes it, so a run from a host shows in the IDEs' preview,
        // the BDD Explorer (AD-022) and the MCP server's last_runs.
        public void RecordIfFinished()
        {
            if (_recorded)
            {
                return;
            }

            var result = Session.ToResult();
            if (Session.HasNext && result.Steps is not [.., { Status: BddStepResultStatus.Failed }])
            {
                return;
            }

            _recorded = true;
            var evidence = !result.Passed && !result.Skipped && _runtime.Config.Reporting.ScreenshotOnFailure
                ? UatScenarioResults.ScreenshotServiceOf(Fixture)?.Capture("BddRunner", UatScenarioResults.EvidenceName(Scenario), "failure")
                : null;
            UatScenarioResults.Record(
                _runtime,
                _commands,
                Scenario,
                new BddScenarioExecutionResult(result, Runner.Context, evidence),
                _clock.Elapsed,
                Fixture.GetType().Assembly.GetName().Name,
                UatScenarioResults.EvidenceName(Scenario),
                _mode);
        }

        // Why a run leaves this scenario out, as the xUnit gate does; a Step-through is someone looking at it, so it goes on.
        public string? WhySkipped => _mode == "run" && _runtime.Config.EvaluateSkip(Scenario.Source.Tags) is { ShouldSkip: true } skip
            ? skip.Reason ?? "Skipped by the suite's config."
            : null;

        // The current page's controls, read between steps. With none current (a phrase of the fixture's drove the page, or
        // a state started the app), the page the steps so far drove last, as the trace names it; else the page that shows.
        public BddRunnerPageState ReadPage()
            => _runtime.Reflection.ReadCurrentPage(Runner.Context, findShowing: true, drivenLast: Session.Results.SelectMany(step => step.Calls).LastOrDefault()?.Page);

        // The suite's fixture, made as an open makes it: for a prepare ahead of the first open.
        public static object MakeFixture(string config, string pages) => CreateFixture(BddConfigParser.ParseFile(config), LoadPages(pages));

        // With a kept fixture, its app as it is: BeforeFile and BeforeScenario give the clean start.
        public static OpenScenario Open(BddRunnerOpen open, object? kept)
        {
            var fixture = kept ?? MakeFixture(open.Config, open.Pages);
            IDisposable? scope = null;
            try
            {
                // As the suite's test class does, and before binding: it may restart the app on
                // the file's test set, and binding reads the fixture's composition.
                var lifecycle = fixture as IUatScenarioLifecycle;
                if (lifecycle is not null && BddMarkdownParser.ParseFile(open.Feature).Document is { } document)
                {
                    lifecycle.BeforeFile(document);
                }

                var runtime = new UatRuntime(fixture, open.Config);
                var catalog = runtime.CreateCommandCatalog();
                var file = BddScenarioFiles.Load(open.Feature);
                if (!file.Success)
                {
                    throw new InvalidOperationException(BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
                }

                var catalogReport = BddDiagnosticsFormatter.FormatCatalog(catalog);
                var bind = file.Bind(runtime.CreateCatalogSet(catalog));
                if (!bind.Success || bind.Document is null)
                {
                    throw new InvalidOperationException(string.Join(
                        Environment.NewLine,
                        BddDiagnosticsFormatter.FormatDiagnostics(bind.Diagnostics),
                        runtime.DiscoveryReport,
                        catalogReport));
                }

                var scenario = bind.Document.Scenarios.FirstOrDefault(candidate => candidate.Source.Name.Equals(open.Scenario, StringComparison.Ordinal))
                               ?? throw new InvalidOperationException($"Scenario '{open.Scenario}' was not found in {open.Feature}.");

                lifecycle?.BeforeScenario(scenario);
                var runner = new BddScenarioRunner();
                runtime.ConfigureContext(runner.Context);

                // The host pauses between steps itself; the engine's own breaks are for a debugger.
                runner.Context.Breaks = null;
                scope = runtime.CreateScope();
                if (scope is Microsoft.Extensions.DependencyInjection.IServiceScope serviceScope)
                {
                    UatRuntime.ConfigureScope(runner.Context, serviceScope.ServiceProvider);
                }

                return new OpenScenario(fixture, runner, scenario, scope, runtime.DiscoveryReport, catalogReport, runtime, catalog, open.Mode ?? UatScenarioResults.RunnerMode, open);
            }
            catch
            {
                scope?.Dispose();
                End(fixture);
                throw;
            }
        }

        public void Dispose()
        {
            _scope?.Dispose();
            End(Fixture);
        }

        // The scenario is over (AfterScenario) and its scope ends; its fixture is the caller's to keep or end.
        public object Release()
        {
            try
            {
                (Fixture as IUatScenarioLifecycle)?.AfterScenario(Scenario);
            }
            finally
            {
                _scope?.Dispose();
            }

            return Fixture;
        }

        // By name first: the suite's own deps.json knows it, and its types are then the ones the
        // suite's other assemblies see.
        private static Assembly LoadPages(string path)
        {
            try
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(path)));
            }
            catch (FileNotFoundException)
            {
                return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
        }

        private static object CreateFixture(BddConfig config, Assembly pages)
        {
            if (!config.Runtime.TryGetValue("Fixture", out var name) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("The suite's config must set Runtime Fixture.");
            }

            name = name.Trim();
            var type = pages.GetTypes().FirstOrDefault(type =>
                           type is { IsAbstract: false, IsClass: true } &&
                           type.GetConstructor(Type.EmptyTypes) is not null &&
                           (type.FullName?.Equals(name, StringComparison.Ordinal) == true ||
                            type.Name.Equals(name, StringComparison.Ordinal) ||
                            type.Name.Equals(name + "Fixture", StringComparison.Ordinal)))
                       ?? throw new InvalidOperationException($"Fixture '{name}' was not found in {pages.GetName().Name}.");

            var fixture = Activator.CreateInstance(type)
                          ?? throw new InvalidOperationException($"Fixture '{type.FullName}' could not be created.");

            // As xunit starts a collection fixture: a suite whose app starts in InitializeAsync (the Todo web one) has
            // none before it.
            if (fixture is Xunit.IAsyncLifetime lifetime)
            {
                try
                {
                    lifetime.InitializeAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    End(fixture);
                    throw;
                }
            }

            return fixture;
        }
    }
}
