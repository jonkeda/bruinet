using System.Text.Encodings.Web;
using System.Text.Json;

namespace Bruinet.Core.Api;

/// <summary>
/// Exchanges kept in a file, one JSON object per line: what a run's sessions and pages called, for a description of
/// the API merged from it and for a contract check. The bodies in it are masked.
/// </summary>
public static class ApiExchangeLog
{
    /// <summary>The environment variable that names the file every session appends to.</summary>
    public const string Variable = "BRUINET_API_EXCHANGES";

    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The file a run appends to, when the environment names one.</summary>
    public static string? Current => Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } path ? path : null;

    /// <summary>Appends an exchange to a file, making the file and its folder when they are not there.</summary>
    /// <param name="path">The file.</param>
    /// <param name="exchange">The exchange.</param>
    public static void Append(string path, ApiExchange exchange)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(exchange);
        var line = JsonSerializer.Serialize(exchange, Json);
        lock (Gate)
        {
            if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.AppendAllText(path, line + "\n");
        }
    }

    /// <summary>The exchanges of a file, oldest first. A line that is not an exchange is skipped.</summary>
    /// <param name="path">The file.</param>
    public static IReadOnlyList<ApiExchange> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var found = new List<ApiExchange>();
        foreach (var line in File.ReadLines(path))
        {
            if (Parse(line) is { } exchange)
            {
                found.Add(exchange);
            }
        }

        return found;
    }

    /// <summary>One line of a file as an exchange; null when it is not one.</summary>
    /// <param name="line">The line.</param>
    public static ApiExchange? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ApiExchange>(line, Json) is { Method.Length: > 0, Url.Length: > 0 } exchange ? exchange : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
