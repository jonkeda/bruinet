namespace Bruinet.Mocking.Http;

/// <summary>How a replayed streamed answer is paced.</summary>
public enum HttpReplayPace
{
    /// <summary>All at once: most tests want speed.</summary>
    None,

    /// <summary>With the recorded gaps between parts: for a test that measures waiting (a loading spinner).</summary>
    Recorded,
}

/// <summary>The fields presets leave out of matching, without a second recording.</summary>
public static class HttpReplayPresets
{
    /// <summary>A model call: matched on its model and messages, not its request id, sampling or time.</summary>
    public static readonly IReadOnlyList<string> Model =
        ["id", "request_id", "requestId", "user", "seed", "temperature", "top_p", "stream_options", "created", "timestamp"];

    /// <summary>A sign-in: tokens, nonces and state differ every time.</summary>
    public static readonly IReadOnlyList<string> OAuth =
        ["state", "nonce", "code_verifier", "code_challenge", "code", "access_token", "refresh_token", "id_token", "client_assertion"];

    /// <summary>A preset's fields by its name (<c>model</c>, <c>oauth</c>); null for an unknown name.</summary>
    public static IReadOnlyList<string>? Named(string name) => name.Trim().ToLowerInvariant() switch
    {
        "model" => Model,
        "oauth" => OAuth,
        _ => null,
    };
}

/// <summary>A test's replay settings, from its suite (<c>uat.config.md</c>) or set in code.</summary>
public sealed class HttpReplayOptions
{
    /// <summary>The headers never written: their values are recorded as <c>***</c>.</summary>
    public static readonly IReadOnlyList<string> DefaultRedactedHeaders =
        ["Authorization", "Proxy-Authorization", "api-key", "Ocp-Apim-Subscription-Key", "x-api-key", "x-functions-key", "Cookie", "Set-Cookie"];

    /// <summary>The query parameters and JSON body fields never written.</summary>
    public static readonly IReadOnlyList<string> DefaultRedactedFields =
        ["api-key", "api_key", "apikey", "sig", "password", "client_secret", "access_token", "refresh_token", "id_token"];

    /// <summary>Every call answered in the process: a call to a host the recording does not know fails by name.</summary>
    public bool Hermetic { get; set; }

    public HttpReplayPace Pace { get; set; } = HttpReplayPace.None;

    /// <summary>Headers recorded as <c>***</c>, beside <see cref="DefaultRedactedHeaders"/> (<c>| HttpRedact | x-key |</c>).</summary>
    public ISet<string> RedactedHeaders { get; } = new HashSet<string>(DefaultRedactedHeaders, StringComparer.OrdinalIgnoreCase);

    /// <summary>Query parameters and body fields recorded as <c>***</c>, beside <see cref="DefaultRedactedFields"/>.</summary>
    public ISet<string> RedactedFields { get; } = new HashSet<string>(DefaultRedactedFields, StringComparer.OrdinalIgnoreCase);

    /// <summary>Request fields left out of matching by their last name: a preset's (<see cref="HttpReplayPresets"/>), or a suite's own.</summary>
    public ISet<string> IgnoredFields { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Adds a preset's fields to <see cref="IgnoredFields"/>.</summary>
    public HttpReplayOptions WithPreset(IEnumerable<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        IgnoredFields.UnionWith(fields);
        return this;
    }

    /// <summary>Adds headers and fields to redact, as a suite names them (comma-separated): a name is both.</summary>
    public HttpReplayOptions Redacting(string? names)
    {
        foreach (var name in (names ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            RedactedHeaders.Add(name);
            RedactedFields.Add(name);
        }

        return this;
    }
}
