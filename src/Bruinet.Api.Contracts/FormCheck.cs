using System.Globalization;
using System.Text.Json;

namespace Bruinet.Api.Contracts;

/// <summary>Where a form's field goes in the body its save sends.</summary>
/// <param name="Field">The field, as the page object names it.</param>
/// <param name="Typed">What a test typed into it.</param>
/// <param name="Path">The place in the sent JSON that held the value (<c>contact.name</c>, <c>lines.0.amount</c>); null when no place did.</param>
public sealed record FormFieldPlace(string Field, string Typed, string? Path);

/// <summary>
/// A saved form checked against what the service stored. A test fills each field with a value of its own; each value
/// is found in the body the save sent, which gives a map from the page object's field to a place in the JSON with no
/// model and no hand work. Then three values per field are compared: what was typed, what was sent, and what the
/// service gives back when the record is read. A field with no place in the body is a finding: it was shown and never
/// sent, or sent in a form the map cannot recognise.
/// </summary>
public static class FormCheck
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The place of each typed value in the body that was sent.</summary>
    /// <param name="typed">What was typed, by field. Values that are each other's part are found less surely: give each field its own.</param>
    /// <param name="sentJson">The body the save sent.</param>
    public static IReadOnlyList<FormFieldPlace> Map(IReadOnlyDictionary<string, string> typed, string? sentJson)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var leaves = Leaves(sentJson);
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var places = new List<FormFieldPlace>();
        foreach (var (field, value) in typed)
        {
            // The same value in every way first; then as a number or a day; then as the start of a longer text.
            var place = leaves.FirstOrDefault(leaf => !taken.Contains(leaf.Path) && Same(value, leaf.Value))
                ?? leaves.FirstOrDefault(leaf => !taken.Contains(leaf.Path) && (SameNumber(value, leaf.Value) || SameDay(value, leaf.Value)))
                ?? leaves.FirstOrDefault(leaf => !taken.Contains(leaf.Path) && value.Trim().Length >= 4 && leaf.Value.ValueKind == JsonValueKind.String && leaf.Value.GetString()!.Contains(value.Trim(), StringComparison.Ordinal));
            if (place is not null)
            {
                taken.Add(place.Path);
            }

            places.Add(new FormFieldPlace(field, value, place?.Path));
        }

        return places;
    }

    /// <summary>
    /// Compares, for each field with a place, what was typed, what was sent and what the service stored. A stored
    /// value that differs is a failure; a field with no place in the sent body is a warning (a finding to read).
    /// </summary>
    /// <param name="name">What the check is of, such as the form's name.</param>
    /// <param name="places">The fields and their places (<see cref="Map"/>, or a map kept beside the page object).</param>
    /// <param name="sentJson">The body the save sent.</param>
    /// <param name="storedJson">The record as the service gives it back.</param>
    public static ContractResult Compare(string name, IEnumerable<FormFieldPlace> places, string? sentJson, string? storedJson)
    {
        ArgumentNullException.ThrowIfNull(places);
        var result = new ContractResult(name);
        if (string.IsNullOrWhiteSpace(storedJson))
        {
            result.Add(ContractSeverity.Blocked, "$", "The record was not read back, so nothing can be compared.");
            return result;
        }

        var sent = Leaves(sentJson).ToDictionary(leaf => leaf.Path, leaf => leaf.Value, StringComparer.Ordinal);
        var stored = Leaves(storedJson).ToDictionary(leaf => leaf.Path, leaf => leaf.Value, StringComparer.Ordinal);
        foreach (var place in places)
        {
            if (place.Path is null)
            {
                result.Add(ContractSeverity.Warning, place.Field, $"Typed '{place.Typed}', but no place in the sent body holds it: the field was not sent, or sent as another value.");
                continue;
            }

            result.ItemCount++;
            var location = $"$.{place.Path}";
            var sentText = sent.TryGetValue(place.Path, out var sentValue) ? sentValue.GetRawText() : "nothing";
            if (!stored.TryGetValue(place.Path, out var storedValue))
            {
                result.Add(ContractSeverity.Failure, location, $"{place.Field}: typed '{place.Typed}', sent {sentText}, but the stored record has no such field.");
            }
            else if (!(Same(place.Typed, storedValue) || SameNumber(place.Typed, storedValue) || SameDay(place.Typed, storedValue)
                || (sent.ContainsKey(place.Path) && sentValue.GetRawText() == storedValue.GetRawText())))
            {
                result.Add(ContractSeverity.Failure, location, $"{place.Field}: typed '{place.Typed}', sent {sentText}, stored {storedValue.GetRawText()}.");
            }
        }

        return result;
    }

    /// <summary>A map as the text kept beside a page object.</summary>
    /// <param name="places">The fields and their places.</param>
    public static string ToJson(IEnumerable<FormFieldPlace> places)
        => JsonSerializer.Serialize(places.Select(place => new { field = place.Field, path = place.Path }), Json);

    /// <summary>A kept map with the values a test typed now: the fields the map has no row for get no place.</summary>
    /// <param name="mapJson">The kept map (<see cref="ToJson"/>).</param>
    /// <param name="typed">What was typed, by field.</param>
    public static IReadOnlyList<FormFieldPlace> FromJson(string mapJson, IReadOnlyDictionary<string, string> typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        using var document = JsonDocument.Parse(mapJson);
        var paths = document.RootElement.EnumerateArray()
            .Where(row => row.TryGetProperty("field", out _))
            .ToDictionary(row => row.GetProperty("field").GetString()!, row => row.TryGetProperty("path", out var path) ? path.GetString() : null, StringComparer.Ordinal);
        return [.. typed.Select(each => new FormFieldPlace(each.Key, each.Value, paths.GetValueOrDefault(each.Key)))];
    }

    private static List<Leaf> Leaves(string? json)
    {
        var leaves = new List<Leaf>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return leaves;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            Walk(document.RootElement.Clone(), string.Empty, leaves);
        }
        catch (JsonException)
        {
            // A body that is not JSON has no places.
        }

        return leaves;
    }

    private static void Walk(JsonElement element, string path, List<Leaf> leaves)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}", leaves);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, path.Length == 0 ? index.ToString(CultureInfo.InvariantCulture) : $"{path}.{index}", leaves);
                    index++;
                }

                break;
            default:
                leaves.Add(new Leaf(path, element));
                break;
        }
    }

    private sealed record Leaf(string Path, JsonElement Value);

    private static bool Same(string typed, JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(value.GetString()!.Trim(), typed.Trim(), StringComparison.Ordinal),
            JsonValueKind.Null => typed.Trim().Length == 0,
            JsonValueKind.True => string.Equals(typed.Trim(), "true", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.False => string.Equals(typed.Trim(), "false", StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(value.GetRawText(), typed.Trim(), StringComparison.Ordinal),
        };

    // A number typed with a comma or a point, against a JSON number or a numeric text.
    private static bool SameNumber(string typed, JsonElement value)
    {
        if (!Number(typed, out var typedNumber))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out var number) && number == typedNumber,
            JsonValueKind.String => Number(value.GetString()!, out var number) && number == typedNumber,
            _ => false,
        };
    }

    private static bool Number(string text, out decimal number)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || !(char.IsDigit(trimmed[0]) || trimmed[0] == '-'))
        {
            number = 0;
            return false;
        }

        // One comma and no point is a decimal comma (1,5); otherwise the point is.
        var plain = trimmed.Contains(',') && !trimmed.Contains('.') ? trimmed.Replace(',', '.') : trimmed.Replace(",", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(plain, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
    }

    // A day typed as dd-MM-yyyy, d-M-yyyy, dd/MM/yyyy or yyyy-MM-dd, against the date part of a stored text.
    private static bool SameDay(string typed, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: >= 10 } stored
            || !DateOnly.TryParseExact(stored[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var storedDay))
        {
            return false;
        }

        return DateOnly.TryParseExact(typed.Trim(), ["dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var typedDay)
            && typedDay == storedDay;
    }
}
