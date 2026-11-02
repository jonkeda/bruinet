using System.Net;
using Bruinet.Bdd;
using Bruinet.Mocking.Http;

namespace Bruinet.Samples.Todo.TestSupport;

/// <summary>
/// HTTP replay for one app that outlives its scenarios (AI11b), as <see cref="TodoSqlReplay"/> is for its database: the
/// app's calls go through <see cref="Handler"/>, which sends them through the session of the scenario running now, begun
/// before it and ended after it. Only for a head that composes the app in the test's process (MauiHeadless, bUnit).
/// </summary>
/// <remarks>
/// The mode is the run's <c>BRUINET_HTTP_REPLAY</c> (what <c>run_scenario {replay}</c> sets), else the suite's
/// <c>backend</c> dependency (<c>## Dependencies</c>, <c>BRUINET_DEP_BACKEND</c> over it); a <c>stub</c> or <c>real</c>
/// backend takes no session. In a hermetic replay nothing reaches the stub server: a call made outside a session fails.
/// </remarks>
public sealed class TodoHttpReplay
{
    private readonly string _folder;
    private readonly HttpReplayOptions _options;
    private HttpReplaySession? _session;
    private bool _startedUp;

    /// <param name="project">The suite's project folder name; recordings are <c>Recordings/&lt;head&gt;/http/&lt;scenario&gt;.json</c> beside it.</param>
    /// <param name="head">The head the app runs on (<c>headless</c>, <c>bunit</c>); null when it runs the app in a process of its own.</param>
    /// <param name="config">The suite's config, for its <c>backend</c> dependency and replay fields; null for a suite without one.</param>
    /// <exception cref="InvalidOperationException">The suite's dependencies have a problem: a mode its kind does not take, or one leaving a hermetic suite.</exception>
    public TodoHttpReplay(string project, string? head, BddConfig? config = null)
    {
        if (config is not null && BddDependencies.Problems(config) is { Count: > 0 } problems)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems));
        }

        Mode = head is null ? HttpReplayMode.Off : ModeOf(config);
        _folder = Path.Combine(TodoSqlReplay.RecordingsOf(project), head ?? "none");
        var backend = config is null ? null : BddDependencies.Find(config, "backend");
        _options = HttpReplayOptions.ForSuite(
            config is not null && BddDependencies.IsHermetic(config),
            config?.Runtime.GetValueOrDefault(BddReplayFields.HttpRedact),
            config?.Runtime.GetValueOrDefault(BddReplayFields.HttpReplayPace),
            backend?.Where);
        Handler = new ScenarioHandler(this, Mode == HttpReplayMode.Replay && _options.Hermetic
            ? HermeticGuard.Handler()
            : new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });
    }

    /// <summary>This app's mode: off on a head that runs the app in a process of its own, or for a stubbed backend.</summary>
    public HttpReplayMode Mode { get; }

    /// <summary>The app's HTTP handler (<c>TodoPlatform.HttpHandler</c>): through the scenario's session, or straight on.</summary>
    public HttpMessageHandler Handler { get; }

    /// <summary>The running scenario's calls while it is replayed: what "the server received" reads then. Null otherwise.</summary>
    public IReadOnlyList<HttpCall>? ReplayedCalls => Mode == HttpReplayMode.Replay ? _session?.Calls : null;

    /// <summary>The mode a suite asks for: the run's <c>BRUINET_HTTP_REPLAY</c>, else its <c>backend</c> dependency's.</summary>
    public static HttpReplayMode ModeOf(BddConfig? config, Func<string, string?>? environment = null)
    {
        var run = HttpReplaySession.ModeFromEnvironment(environment);
        if (run != HttpReplayMode.Off || config is null || BddDependencies.Find(config, "backend", environment) is not { } backend)
        {
            return run;
        }

        return backend.Mode switch
        {
            "record" => HttpReplayMode.Record,
            "replay" => HttpReplayMode.Replay,
            "refresh" => HttpReplayMode.Refresh,
            _ => HttpReplayMode.Off,
        };
    }

    /// <summary>
    /// Begins the session of what the app does before the first scenario (its first sync), named after
    /// <paramref name="fixture"/>. Once per app; the first scenario's <see cref="Begin"/> ends it.
    /// </summary>
    public void BeginStartup(string fixture)
    {
        if (Mode == HttpReplayMode.Off || _startedUp)
        {
            return;
        }

        _startedUp = true;
        _session = new HttpReplaySession(Mode, HttpReplaySession.PathFor(_folder, fixture + ".startup"), _options);
    }

    /// <summary>Begins <paramref name="scenario"/>'s session; a startup session still open is ended first, its misses thrown here.</summary>
    public void Begin(string scenario)
    {
        if (Mode == HttpReplayMode.Off)
        {
            return;
        }

        End();
        _session = new HttpReplaySession(Mode, HttpReplaySession.PathFor(_folder, scenario), _options);
    }

    /// <summary>Ends the session: its recording saved and its call log written, or its misses thrown into the scenario that made them.</summary>
    public void End()
    {
        var session = _session;
        _session = null;
        if (session is null)
        {
            return;
        }

        session.Finish();
        session.ThrowIfMissed();
    }

    private HttpReplaySession? Current => _session;

    // Through the scenario's session when there is one; the inner handler (the network, or the hermetic guard) otherwise.
    private sealed class ScenarioHandler(TodoHttpReplay replay, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => replay.Current is { } session
                ? session.SendAsync(request, base.SendAsync, cancellationToken)
                : base.SendAsync(request, cancellationToken);
    }
}
