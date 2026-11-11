using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bruinet.Mocking.Data.Replay;

/// <summary>A recording held a value that looks like a key, so it was not saved: record against test data only.</summary>
public sealed class SqlRecordingSecretException(string message) : Exception(message);

/// <summary>
/// What a SQL recording never keeps (review 09, D4). Recordings go into git, so before one is saved: a parameter
/// named like a secret is written <c>***</c> and is volatile, so replay binds the live value instead of matching it;
/// a result column the suite names in <see cref="ColumnsVariable"/> is written <c>***</c>; and a value that still
/// looks like a key stops the save, naming where it is.
/// </summary>
/// <remarks>
/// Result columns are not redacted by default: replay gives the recorded value back to the app, so a redacted
/// <c>PasswordHash</c> would make every replayed sign-in fail. A suite that records such columns names them.
/// </remarks>
public static class SqlRedaction
{
    /// <summary>The value written in place of a redacted one.</summary>
    public const string Redacted = "***";

    /// <summary>Result columns to write as <c>***</c>, comma-separated (<c>Pin,Iban</c>), in the app's environment.</summary>
    public const string ColumnsVariable = "BRUINET_SQL_REDACT";

    private static readonly Regex SecretName = new(
        "password|passwd|secret|token|apikey|api_key|securitystamp|authenticatorkey",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // From gitleaks' rules (AD-033), as HTTP replay's scan: what a database should never hand a test.
    private static readonly (string Name, Regex Pattern)[] Keys =
    [
        ("a JSON web token", new Regex(@"\beyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}", RegexOptions.Compiled)),
        ("a private key", new Regex("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled)),
        ("an AWS access key", new Regex(@"\b(AKIA|ASIA)[0-9A-Z]{16}\b", RegexOptions.Compiled)),
        ("an OpenAI key", new Regex(@"\bsk-[A-Za-z0-9_-]{20,}", RegexOptions.Compiled)),
        ("a GitHub token", new Regex(@"\bgh[pousr]_[A-Za-z0-9]{36,}", RegexOptions.Compiled)),
    ];

    /// <summary>Whether a parameter or column of this name holds a secret by its name.</summary>
    /// <param name="name">The parameter's or column's name, with or without its <c>@</c>.</param>
    public static bool IsSecretName(string name) => SecretName.IsMatch(name);

    /// <summary>A value as a message may show it: <c>***</c> for a secret's name.</summary>
    internal static JsonNode? Shown(string name, JsonNode? value) => IsSecretName(name) && value is not null ? Values.ToJson(Redacted) : value;

    /// <summary>
    /// Redacts <paramref name="recording"/> in place before it is saved; throws when a value still looks like a key.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="path">Its file, for the message.</param>
    /// <exception cref="SqlRecordingSecretException">A value looks like a key.</exception>
    internal static void Apply(Recording recording, string path)
    {
        var columns = new HashSet<string>(
            (Environment.GetEnvironmentVariable(ColumnsVariable) ?? string.Empty)
                .Split(',')
                .Select(name => name.Trim())
                .Where(name => name.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        foreach (var exchange in recording.Exchanges)
        {
            foreach (var name in exchange.Parameters.Keys.Where(IsSecretName).ToList())
            {
                // One placeholder per exchange and name: replay binds a volatile value by its recorded one, so two
                // passwords sent in one test must not share it.
                exchange.Parameters[name] = Values.ToJson($"{Redacted} {name} #{exchange.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                if (!exchange.Volatile.Contains(name))
                {
                    exchange.Volatile = [.. exchange.Volatile.Append(name).OrderBy(each => each, StringComparer.Ordinal)];
                }
            }

            if (columns.Count > 0 && exchange.Results is { } results)
            {
                exchange.Results = [.. results.Select(result => RedactColumns(result, columns))];
            }

            Scan(exchange, path);
        }
    }

    private static RecordedResult RedactColumns(RecordedResult result, HashSet<string> columns)
    {
        var redacted = result.Columns.Select(column => columns.Contains(column.Name)).ToArray();
        if (!redacted.Any(each => each))
        {
            return result;
        }

        return result with
        {
            Rows = [.. result.Rows.Select(row => (IReadOnlyList<JsonNode?>)[.. row.Select((value, i) => redacted[i] && value is not null ? Values.ToJson(Redacted) : value)])],
        };
    }

    private static void Scan(Exchange exchange, string path)
    {
        foreach (var parameter in exchange.Parameters)
        {
            Check(parameter.Value, path, $"exchange #{exchange.Index}, parameter {parameter.Key}");
        }

        foreach (var (result, r) in (exchange.Results ?? []).Select((result, r) => (result, r)))
        {
            foreach (var row in result.Rows)
            {
                for (var i = 0; i < row.Count && i < result.Columns.Count; i++)
                {
                    Check(row[i], path, $"exchange #{exchange.Index}, result {r + 1}, column {result.Columns[i].Name}");
                }
            }
        }
    }

    private static void Check(JsonNode? value, string path, string where)
    {
        if (value?["v"] is not JsonValue leaf || !leaf.TryGetValue<string>(out var text) || text.Length < 20)
        {
            return;
        }

        foreach (var (name, pattern) in Keys)
        {
            if (pattern.IsMatch(text))
            {
                throw new SqlRecordingSecretException(
                    $"{Path.GetFileName(path)} was not saved: {where} holds what looks like {name}. Record against seeded "
                    + $"test data only; a column that must hold one is named in {ColumnsVariable} and written as {Redacted}.");
            }
        }
    }
}
