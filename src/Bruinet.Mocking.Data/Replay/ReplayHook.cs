using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>
/// The sessions of an app in a process of its own (IIS, a started exe), one per test id, each recording its own file
/// <c>&lt;folder&gt;/&lt;test&gt;.json</c>. The tests run elsewhere and name their test on each request (<see cref="ReplayHook"/>).
/// </summary>
public sealed class ReplayRegistry(ReplayMode mode, string folder, Schema? schema = null)
{
    /// <summary>The app's recordings folder.</summary>
    public const string FolderVariable = "BRUINET_SQL_RECORDINGS";

    /// <summary>An optional schema snapshot, for the stamp.</summary>
    public const string SchemaVariable = "BRUINET_SQL_SCHEMA";

    /// <summary><c>Record</c> or <c>Replay</c>; unset, the hook is off.</summary>
    public const string ModeVariable = "BRUINET_SQL_REPLAY";

    private readonly object _gate = new();
    private readonly Dictionary<string, ReplaySession> _sessions = new(StringComparer.Ordinal);

    public ReplayMode Mode { get; } = mode;

    public string Folder { get; } = folder;

    /// <summary>The registry the app's environment asks for; null when replay is off, so nothing of it runs.</summary>
    public static ReplayRegistry? FromEnvironment()
    {
        if (!Enum.TryParse<ReplayMode>(Environment.GetEnvironmentVariable(ModeVariable), ignoreCase: true, out var mode) || mode == ReplayMode.Off)
        {
            return null;
        }

        var folder = Environment.GetEnvironmentVariable(FolderVariable);
        if (string.IsNullOrEmpty(folder))
        {
            throw new InvalidOperationException($"{ModeVariable} is {mode}, but {FolderVariable} names no recordings folder.");
        }

        var schema = Environment.GetEnvironmentVariable(SchemaVariable) is { Length: > 0 } file ? SchemaSnapshot.Load(file).ToSchema() : null;
        return new ReplayRegistry(mode, folder!, schema);
    }

    /// <summary>Starts <paramref name="test"/>'s session; one still open for it is dropped unfinished.</summary>
    public ReplaySession Begin(string test)
    {
        var session = new ReplaySession(Mode, Path.Combine(Folder, ReplayHook.CheckTest(test) + ".json"), schema);
        lock (_gate)
        {
            _sessions[test] = session;
        }

        return session;
    }

    /// <summary><paramref name="test"/>'s open session; null when it has none.</summary>
    public ReplaySession? Find(string test)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(test, out var session) ? session : null;
        }
    }

    /// <summary>Finishes <paramref name="test"/>'s session (a recording is saved) and gives its misses; null when it has none.</summary>
    public IReadOnlyList<string>? End(string test)
    {
        ReplaySession? session;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(test, out session))
            {
                return null;
            }

            _sessions.Remove(test);
        }

        session.Finish();
        return session.Misses;
    }
}

/// <summary>What a control path answers.</summary>
public sealed record HookResponse(int Status, string Body, string? Cookie);

/// <summary>
/// The test-id hook: what a host (ASP.NET Core's middleware, System.Web's module) does with a request, and what a test
/// sends. A request names its test by the cookie (a browser) or the header (an API client); the control paths begin and
/// end a test's session.
/// </summary>
public static class ReplayHook
{
    public const string Cookie = "bruinet-test";

    public const string Header = "X-Bruinet-Test";

    /// <summary>The header an API client sends the hook's key in; a browser's begin address carries it as <c>key</c>.</summary>
    public const string KeyHeader = "X-Bruinet-Key";

    /// <summary>
    /// The hook's key, in the app's environment and the test's. Unset, the control paths answer callers on this machine
    /// only; set, they answer whoever sends it (an app on another machine) and nobody else (review 09, D4).
    /// </summary>
    public const string KeyVariable = "BRUINET_SQL_HOOK_KEY";

    public const string BeginPath = "/__bruinet/sql/begin";

    public const string EndPath = "/__bruinet/sql/end";

    private static readonly Regex TestId = new("^[A-Za-z0-9._-]{1,200}$", RegexOptions.Compiled);

    /// <summary>The test a request names: its header, else its cookie; null when none or not a valid id.</summary>
    public static string? TestOf(Func<string, string?> header, Func<string, string?> cookie)
        => (header(Header) is { Length: > 0 } fromHeader ? fromHeader : cookie(Cookie)) is { } test && TestId.IsMatch(test) ? test : null;

    /// <summary>
    /// The answer to a control path; null for any other path, which the app serves in the test's session. A caller the
    /// hook does not serve (<see cref="KeyVariable"/>) gets 404, as if there were no hook.
    /// </summary>
    /// <param name="registry">The app's sessions.</param>
    /// <param name="path">The request's path.</param>
    /// <param name="query">Reads a query parameter.</param>
    /// <param name="header">Reads a request header.</param>
    /// <param name="loopback">Whether the caller is on this machine, or in this process.</param>
    public static HookResponse? Control(ReplayRegistry registry, string path, Func<string, string?> query, Func<string, string?> header, bool loopback)
    {
        var begin = string.Equals(path, BeginPath, StringComparison.OrdinalIgnoreCase);
        if (!begin && !string.Equals(path, EndPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Serves(header(KeyHeader) ?? query("key"), loopback))
        {
            return new HookResponse(404, "Not found.", null);
        }

        if (query("test") is not { } test || !TestId.IsMatch(test))
        {
            return new HookResponse(400, "A test id of letters, digits, '.', '_' or '-' is needed: ?test=<id>.", null);
        }

        if (begin)
        {
            try
            {
                registry.Begin(test);
            }
            catch (ReplayMissException missing)
            {
                return new HookResponse(409, missing.Message, null);
            }

            return new HookResponse(200, $"{registry.Mode} {test}", test);
        }

        return registry.End(test) switch
        {
            null => new HookResponse(404, $"No session for {test}: begin it first.", null),
            { Count: 0 } => new HookResponse(200, $"{registry.Mode} {test} done", null),
            var misses => new HookResponse(409, string.Join("\n\n", misses), null),
        };
    }

    /// <summary>
    /// Where a browser goes first to start <paramref name="test"/>'s session: the answer sets its cookie. A browser's
    /// navigation is a GET and cannot add a header, so the hook's key, when there is one, is in the address.
    /// </summary>
    /// <param name="app">The app's address.</param>
    /// <param name="test">The test's id.</param>
    /// <param name="key">The hook's key; <see cref="KeyVariable"/> when left out.</param>
    public static Uri BeginUrl(Uri app, string test, string? key = null)
        => new(app, BeginPath + "?test=" + Uri.EscapeDataString(CheckTest(test))
            + (KeyOrDefault(key) is { } given ? "&key=" + Uri.EscapeDataString(given) : string.Empty));

    /// <summary>Starts <paramref name="test"/>'s session from an API client; it then sends <see cref="Header"/> itself.</summary>
    /// <param name="client">The client.</param>
    /// <param name="app">The app's address.</param>
    /// <param name="test">The test's id.</param>
    /// <param name="key">The hook's key; <see cref="KeyVariable"/> when left out.</param>
    public static async Task BeginAsync(HttpClient client, Uri app, string test, string? key = null)
    {
        using var request = Keyed(new Uri(app, BeginPath + "?test=" + Uri.EscapeDataString(CheckTest(test))), key);
        using var response = await client.SendAsync(request).ConfigureAwait(false);
        await Check(response, test).ConfigureAwait(false);
    }

    /// <summary>Ends <paramref name="test"/>'s session in the app; throws <see cref="ReplayMissException"/> with its misses.</summary>
    /// <param name="client">The client.</param>
    /// <param name="app">The app's address.</param>
    /// <param name="test">The test's id.</param>
    /// <param name="key">The hook's key; <see cref="KeyVariable"/> when left out.</param>
    public static async Task EndAsync(HttpClient client, Uri app, string test, string? key = null)
    {
        using var request = Keyed(new Uri(app, EndPath + "?test=" + Uri.EscapeDataString(CheckTest(test))), key);
        using var response = await client.SendAsync(request).ConfigureAwait(false);
        await Check(response, test).ConfigureAwait(false);
    }

    private static HttpRequestMessage Keyed(Uri address, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (KeyOrDefault(key) is { } given)
        {
            request.Headers.Add(KeyHeader, given);
        }

        return request;
    }

    private static string? KeyOrDefault(string? key)
        => key is { Length: > 0 } ? key : Environment.GetEnvironmentVariable(KeyVariable) is { Length: > 0 } set ? set : null;

    // With no key set, a caller on this machine only; with one, whoever sends it, wherever they are.
    private static bool Serves(string? sent, bool loopback)
        => Environment.GetEnvironmentVariable(KeyVariable) is { Length: > 0 } key ? sent is not null && Same(sent, key) : loopback;

    // In constant time, so the key cannot be found a character at a time.
    private static bool Same(string sent, string key)
    {
        var difference = sent.Length ^ key.Length;
        for (var i = 0; i < Math.Min(sent.Length, key.Length); i++)
        {
            difference |= sent[i] ^ key[i];
        }

        return difference == 0;
    }

    internal static string CheckTest(string test)
        => TestId.IsMatch(test) ? test : throw new ArgumentException($"'{test}' is not a test id: letters, digits, '.', '_' or '-'.", nameof(test));

    private static async Task Check(HttpResponseMessage response, string test)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw response.StatusCode == HttpStatusCode.Conflict
            ? new ReplayMissException(body)
            : new InvalidOperationException($"The app's SQL replay hook answered {(int)response.StatusCode} for {test}: {body}");
    }
}
