using System.Text;
using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>
/// Recovers the complete leading items of a JSON array whose recorded body was cut off, so a
/// large list response can still be checked item by item.
/// </summary>
public static class TruncatedJson
{
    /// <summary>
    /// Returns a valid JSON array of the items that were recorded in full, or <c>null</c> when the
    /// body is not an array or holds no complete item.
    /// </summary>
    public static (string Json, int Count)? CompleteArrayItems(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var bytes = Encoding.UTF8.GetBytes(body);
        var reader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                return null;

            var items = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var start = (int)reader.TokenStartIndex;
                if (!reader.TrySkip())
                    break;

                var end = (int)reader.BytesConsumed;
                items.Add(Encoding.UTF8.GetString(bytes, start, end - start));
            }

            return items.Count == 0 ? null : ("[" + string.Join(",", items) + "]", items.Count);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
