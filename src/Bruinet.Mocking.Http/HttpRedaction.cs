using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bruinet.Mocking.Http;

/// <summary>A recording held a value that looks like a secret, so it was not saved.</summary>
public sealed class HttpRecordingSecretException(string message) : Exception(message);

/// <summary>
/// What is never written: the named headers, query parameters and JSON fields as <c>***</c>; then a scan for anything
/// that still looks like a key, which stops the save naming where it is.
/// </summary>
internal static partial class HttpRedaction
{
    public const string Redacted = "***";

    /// <summary>A redacted value as a JSON leaf reads in <see cref="HttpMatching.RequestValues"/>.</summary>
    public const string RedactedJson = "\"***\"";

    // Patterns from gitleaks' rules (AD-033), for what a call carries: model and cloud keys, tokens, private keys.
    private static readonly (string Name, Regex Pattern)[] Secrets =
    [
        ("an OpenAI key", OpenAiKey()),
        ("an AWS access key", AwsKey()),
        ("a GitHub token", GitHubToken()),
        ("a Slack token", SlackToken()),
        ("a JSON web token", JsonWebToken()),
        ("a private key", PrivateKey()),
        ("an Azure storage key", AzureStorageKey()),
    ];

    /// <summary>Replaces the named values of <paramref name="exchange"/> with <c>***</c>.</summary>
    public static void Redact(HttpExchange exchange, HttpReplayOptions options)
    {
        foreach (var name in exchange.RequestHeaders.Keys.Where(options.RedactedHeaders.Contains).ToList())
        {
            exchange.RequestHeaders[name] = Redacted;
        }

        foreach (var name in exchange.ResponseHeaders.Keys.Where(options.RedactedHeaders.Contains).ToList())
        {
            exchange.ResponseHeaders[name] = Redacted;
        }

        exchange.Url = RedactQuery(exchange.Url, options.RedactedFields);
        if (exchange.RequestBodyEncoding is null)
        {
            exchange.RequestBody = RedactBody(exchange.RequestBody, exchange.RequestHeaders, options.RedactedFields);
        }

        if (exchange.ResponseBodyEncoding is null)
        {
            exchange.ResponseBody = RedactBody(exchange.ResponseBody, exchange.ResponseHeaders, options.RedactedFields);
        }
    }

    /// <summary>Stops a save: the first value in <paramref name="recording"/> that looks like a secret, named by where it is.</summary>
    /// <exception cref="HttpRecordingSecretException">One looks like a secret.</exception>
    public static void Scan(HttpRecording recording, string path)
    {
        foreach (var exchange in recording.Exchanges)
        {
            var at = $"exchange {exchange.Index.ToString(CultureInfo.InvariantCulture)} ({exchange.Method} {exchange.PathAndQuery})";
            Check(exchange.Url, at + " URL", path);
            foreach (var (name, value) in exchange.RequestHeaders)
            {
                Check(value, $"{at} request header {name}", path);
            }

            foreach (var (name, value) in exchange.ResponseHeaders)
            {
                Check(value, $"{at} response header {name}", path);
            }

            Check(exchange.RequestBody, at + " request body", path);
            Check(exchange.ResponseBody, at + " response body", path);
            foreach (var chunk in exchange.Chunks ?? [])
            {
                Check(chunk.Data, at + " streamed answer", path);
            }
        }
    }

    private static void Check(string? value, string where, string path)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var (name, pattern) in Secrets)
        {
            if (pattern.IsMatch(value))
            {
                throw new HttpRecordingSecretException(
                    $"Not saved: {where} holds what looks like {name}. Name it to redact (HttpRedact), or keep it out of the call; recording {path}.");
            }
        }
    }

    public static string RedactQuery(string url, ISet<string> fields)
    {
        var question = url.IndexOf('?', StringComparison.Ordinal);
        if (question < 0)
        {
            return url;
        }

        var pairs = url[(question + 1)..].Split('&').Select(pair =>
        {
            var parts = pair.Split('=', 2);
            return parts.Length == 2 && fields.Contains(Uri.UnescapeDataString(parts[0])) ? parts[0] + "=" + Redacted : pair;
        });
        return url[..(question + 1)] + string.Join("&", pairs);
    }

    private static string? RedactBody(string? body, Dictionary<string, string> headers, ISet<string> fields)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        if (headers.TryGetValue("Content-Type", out var type) && type.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return RedactQuery("?" + body, fields)[1..];
        }

        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return body;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return body;
        }

        return RedactNode(node, fields) ? node!.ToJsonString() : body;
    }

    private static bool RedactNode(JsonNode? node, ISet<string> fields)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject item:
                foreach (var (name, value) in item.ToList())
                {
                    if (fields.Contains(name) && value is JsonValue)
                    {
                        item[name] = Redacted;
                        changed = true;
                    }
                    else
                    {
                        changed |= RedactNode(value, fields);
                    }
                }

                break;
            case JsonArray list:
                foreach (var value in list)
                {
                    changed |= RedactNode(value, fields);
                }

                break;
        }

        return changed;
    }

    [GeneratedRegex(@"\bsk-(?:proj-|ant-)?[A-Za-z0-9_\-]{20,}")]
    private static partial Regex OpenAiKey();

    [GeneratedRegex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b")]
    private static partial Regex AwsKey();

    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{36}|github_pat_[A-Za-z0-9_]{60,})\b")]
    private static partial Regex GitHubToken();

    [GeneratedRegex(@"\bxox[baprs]-[A-Za-z0-9-]{10,}")]
    private static partial Regex SlackToken();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}")]
    private static partial Regex JsonWebToken();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"AccountKey=[A-Za-z0-9+/=]{40,}")]
    private static partial Regex AzureStorageKey();
}
