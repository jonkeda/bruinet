using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Bruinet.Core.Api;

/// <summary>How an <see cref="ApiSession"/> signs its calls as an account: a token, a key, whatever the service asks.</summary>
public interface IApiSignIn
{
    /// <summary>Puts the sign-in on a call about to be sent.</summary>
    /// <param name="request">The call.</param>
    void Apply(HttpRequestMessage request);

    /// <summary>Gets a new sign-in after the service refused the old one (a 401).</summary>
    /// <returns>Whether there is a new one to try the call again with.</returns>
    bool Renew();
}

/// <summary>
/// A bearer token from wherever a suite gets it: fetched when first needed, again when it ran out (a JSON web token's
/// own <c>exp</c>, read a minute early) and again when the service refuses it.
/// </summary>
public sealed class BearerSignIn : IApiSignIn
{
    private static readonly TimeSpan Early = TimeSpan.FromMinutes(1);

    private readonly Func<string> _fetch;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();
    private string? _token;
    private DateTimeOffset? _until;

    /// <summary>A sign-in with the way to fetch its token.</summary>
    /// <param name="fetch">Fetches a token; throws when there is none to be had.</param>
    /// <param name="now">The clock, for tests; the system's when null.</param>
    public BearerSignIn(Func<string> fetch, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        _fetch = fetch;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
    }

    /// <inheritdoc />
    public bool Renew()
    {
        lock (_gate)
        {
            var old = _token;
            Fetch();
            return !string.Equals(old, _token, StringComparison.Ordinal);
        }
    }

    /// <summary>When a JSON web token runs out, by its <c>exp</c>; null when the token is not one or has none.</summary>
    /// <param name="token">The token.</param>
    public static DateTimeOffset? Expiry(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            using var payload = JsonDocument.Parse(Convert.FromBase64String(padded));
            return payload.RootElement.ValueKind == JsonValueKind.Object
                && payload.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string Token()
    {
        lock (_gate)
        {
            if (_token is null || (_until is { } until && _now() >= until - Early))
            {
                Fetch();
            }

            return _token!;
        }
    }

    private void Fetch()
    {
        _token = _fetch();
        _until = Expiry(_token);
    }
}

/// <summary>A key the service wants in a header of every call.</summary>
/// <param name="header">The header's name, such as <c>X-Api-Key</c>.</param>
/// <param name="key">The key.</param>
public sealed class ApiKeySignIn(string header, string key) : IApiSignIn
{
    /// <inheritdoc />
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Remove(header);
        request.Headers.TryAddWithoutValidation(header, key);
    }

    /// <inheritdoc />
    public bool Renew() => false;
}

/// <summary>The sign-ins a suite gets a token with.</summary>
public static class ApiSignIns
{
    /// <summary>
    /// A token from a web session: the session's cookies are sent to the address where the web app itself gets its
    /// token, and the answer (the token as text, or as a JSON string) signs the API's calls.
    /// </summary>
    /// <param name="cookies">The web session's cookies, such as those of a saved sign-in.</param>
    /// <param name="tokenAddress">Where the web app gets its token.</param>
    /// <param name="who">The account's name, for the message when the session ran out.</param>
    /// <param name="handler">The handler the token is fetched through, for tests; a new one with the cookies when null.</param>
    public static BearerSignIn FromCookies(CookieContainer cookies, Uri tokenAddress, string who, Func<HttpMessageHandler>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(tokenAddress);
        return new BearerSignIn(() =>
        {
            using var http = new HttpClient(handler?.Invoke() ?? new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false });
            using var request = new HttpRequestMessage(HttpMethod.Get, tokenAddress);
            using var response = http.SendAsync(request).GetAwaiter().GetResult();
            var token = ApiSession.ReadText(response).Trim().Trim('"');
            return response.IsSuccessStatusCode && token.Split('.').Length == 3
                ? token
                : throw new ApiCallException($"{who}'s saved sign-in gave no token ({(int)response.StatusCode} from {tokenAddress.AbsolutePath}): it ran out. Run a test that signs in as {who}.");
        });
    }

    /// <summary>A token by OAuth's client-credentials flow: the client's id and secret, posted to the token address.</summary>
    /// <param name="tokenAddress">The token address.</param>
    /// <param name="clientId">The client's id.</param>
    /// <param name="clientSecret">The client's secret.</param>
    /// <param name="scope">The scope asked for, when the service wants one.</param>
    /// <param name="handler">The handler the token is fetched through, for tests.</param>
    public static BearerSignIn ClientCredentials(Uri tokenAddress, string clientId, string clientSecret, string? scope = null, Func<HttpMessageHandler>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(tokenAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        return new BearerSignIn(() =>
        {
            using var http = handler is null ? new HttpClient() : new HttpClient(handler());
            var form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" };
            if (scope is { Length: > 0 })
            {
                form["scope"] = scope;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenAddress) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
            using var response = http.SendAsync(request).GetAwaiter().GetResult();
            var text = ApiSession.ReadText(response);
            var token = response.IsSuccessStatusCode && ApiMasks.Parse(text) is { } answer ? (string?)answer["access_token"] : null;
            return token is { Length: > 0 }
                ? token
                : throw new ApiCallException($"The client {clientId} got no token ({(int)response.StatusCode} from {tokenAddress.AbsolutePath}).");
        });
    }
}
