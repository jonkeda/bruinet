using System.Text.Json;

namespace Bruinet.Contracts;

/// <summary>
/// Compares the values the app sent in a request with the values the service returns for the
/// same entity. The service is the truth, so every difference is a failure the app must explain.
/// </summary>
public static class RoundTripCheck
{
    /// <summary>
    /// Compares <paramref name="fields"/> of the sent JSON object with the read-back JSON object.
    /// A field the app did not send is skipped. An empty sent string matches a <c>null</c> read back.
    /// </summary>
    public static ContractResult Compare(string name, string? sentJson, string? readJson, IEnumerable<string> fields)
        => Compare(name, sentJson, readJson, fields.Select(field => new RoundTripField(field, field)));

    /// <summary>
    /// Compares sent and read-back values whose paths differ, such as <c>contact.id</c> sent to
    /// Heimdall and <c>contact_id</c> read back from Bouw. Paths are dot-separated property names.
    /// </summary>
    public static ContractResult Compare(string name, string? sentJson, string? readJson, IEnumerable<RoundTripField> fields)
    {
        var result = new ContractResult(name);
        if (string.IsNullOrWhiteSpace(sentJson))
        {
            result.Add(ContractSeverity.Blocked, "$", "The request body was not recorded, so nothing can be compared.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(readJson))
        {
            result.Add(ContractSeverity.Blocked, "$", "The read-back body was not recorded, so nothing can be compared.");
            return result;
        }

        using var sent = JsonDocument.Parse(sentJson);
        using var read = JsonDocument.Parse(readJson);
        foreach (var field in fields)
        {
            if (!TryGetPath(sent.RootElement, field.SentPath, out var sentValue))
                continue;

            result.ItemCount++;
            var location = $"$.{field.ReadPath}";
            if (!TryGetPath(read.RootElement, field.ReadPath, out var readValue))
            {
                if (!IsEmpty(sentValue))
                    result.Add(ContractSeverity.Failure, location, $"Sent {sentValue.GetRawText()}, but the response has no such field.");
                continue;
            }

            switch (field.Kind)
            {
                case RoundTripKind.CalendarDay:
                    CompareCalendarDay(result, location, sentValue, readValue);
                    break;
                case RoundTripKind.SameMoment:
                    CompareMoment(result, location, sentValue, readValue);
                    break;
                case RoundTripKind.Decimal:
                    if (!(IsEmpty(sentValue) && IsEmpty(readValue))
                        && !(TryGetNumber(sentValue, out var sentNumber) && TryGetNumber(readValue, out var readNumber) && sentNumber == readNumber))
                        result.Add(ContractSeverity.Failure, location, $"Sent {sentValue.GetRawText()}, read back {readValue.GetRawText()}: not the same number.");
                    break;
                case RoundTripKind.IdList:
                    var sentIds = Ids(sentValue);
                    var readIds = Ids(readValue);
                    if (!sentIds.SetEquals(readIds))
                        result.Add(ContractSeverity.Failure, location,
                            $"Sent ids [{string.Join(", ", sentIds.Order())}], read back [{string.Join(", ", readIds.Order())}].");
                    break;
                case RoundTripKind.IdsIncluded:
                    var sentIncluded = Ids(sentValue);
                    var readIncluded = Ids(readValue);
                    if (!sentIncluded.IsSubsetOf(readIncluded))
                        result.Add(ContractSeverity.Failure, location,
                            $"Sent ids [{string.Join(", ", sentIncluded.Order())}], read back [{string.Join(", ", readIncluded.Order())}].");
                    else if (readIncluded.Count > sentIncluded.Count)
                        result.Add(ContractSeverity.Warning, location,
                            $"The service added ids [{string.Join(", ", readIncluded.Except(sentIncluded).Order())}] to the sent [{string.Join(", ", sentIncluded.Order())}].");
                    break;
                default:
                    switch (Match(sentValue, readValue))
                    {
                        case ValueMatch.Different:
                            result.Add(ContractSeverity.Failure, location, $"Sent {sentValue.GetRawText()}, read back {readValue.GetRawText()}.");
                            break;
                        case ValueMatch.SameValueOtherKind:
                            result.Add(ContractSeverity.Warning, location,
                                $"Sent {sentValue.GetRawText()}, read back {readValue.GetRawText()}: same value, different JSON kind.");
                            break;
                    }
                    break;
            }
        }

        return result;
    }

    // The day the user picked is the date part of what the app sent; the service must keep that day.
    private static void CompareCalendarDay(ContractResult result, string location, JsonElement sent, JsonElement read)
    {
        if (IsEmpty(sent) && IsEmpty(read))
            return;

        var sentDay = CalendarDay(sent);
        var readDay = CalendarDay(read);
        if (sentDay is null || readDay is null)
            result.Add(ContractSeverity.Failure, location, $"Sent {sent.GetRawText()}, read back {read.GetRawText()}: not both dates.");
        else if (sentDay != readDay)
            result.Add(ContractSeverity.Failure, location, $"Sent the day {sentDay:yyyy-MM-dd} as {sent.GetRawText()}, read back {readDay:yyyy-MM-dd} as {read.GetRawText()}.");
    }

    private static void CompareMoment(ContractResult result, string location, JsonElement sent, JsonElement read)
    {
        if (IsEmpty(sent) && IsEmpty(read))
            return;

        var sentMoment = Moment(sent);
        var readMoment = Moment(read);
        if (sentMoment is null || readMoment is null)
            result.Add(ContractSeverity.Failure, location, $"Sent {sent.GetRawText()}, read back {read.GetRawText()}: not both moments.");
        else if (sentMoment != readMoment)
            result.Add(ContractSeverity.Failure, location, $"Sent {sentMoment:u} as {sent.GetRawText()}, read back {readMoment:u} as {read.GetRawText()}.");
    }

    // Unix seconds as a number or numeric string, or an ISO 8601 text; a text without a zone is UTC.
    private static DateTimeOffset? Moment(JsonElement value)
    {
        if (TryGetNumber(value, out var seconds) && seconds == decimal.Truncate(seconds))
            return DateTimeOffset.FromUnixTimeSeconds((long)seconds);

        return value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var moment)
                ? moment.ToUniversalTime()
                : null;
    }

    private static DateOnly? CalendarDay(JsonElement value)
        => value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: >= 10 } text
            && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var day)
                ? day
                : null;

    private static HashSet<long> Ids(JsonElement value)
    {
        var ids = new HashSet<long>();
        var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
        foreach (var item in items)
        {
            var id = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var nested) ? nested : item;
            if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number))
                ids.Add(number);
        }

        return ids;
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                return false;
        }

        return true;
    }
    public enum ValueMatch
    {
        Equal,
        SameValueOtherKind,
        Different
    }

    public static ValueMatch Match(JsonElement sent, JsonElement read)
    {
        if (IsEmpty(sent) && IsEmpty(read))
            return ValueMatch.Equal;

        if (sent.ValueKind == JsonValueKind.Number && read.ValueKind == JsonValueKind.Number)
            return sent.TryGetDecimal(out var a) && read.TryGetDecimal(out var b) && a == b
                ? ValueMatch.Equal
                : ValueMatch.Different;

        if (sent.ValueKind == JsonValueKind.String && read.ValueKind == JsonValueKind.String)
            return string.Equals(sent.GetString(), read.GetString(), StringComparison.Ordinal)
                ? ValueMatch.Equal
                : ValueMatch.Different;

        if (TryGetNumber(sent, out var sentNumber) && TryGetNumber(read, out var readNumber))
            return sentNumber == readNumber ? ValueMatch.SameValueOtherKind : ValueMatch.Different;

        return JsonElement.DeepEquals(sent, read) ? ValueMatch.Equal : ValueMatch.Different;
    }

    private static bool IsEmpty(JsonElement value)
        => value.ValueKind == JsonValueKind.Null
            || (value.ValueKind == JsonValueKind.String && value.GetString()!.Length == 0);

    private static bool TryGetNumber(JsonElement value, out decimal number)
    {
        number = 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDecimal(out number),
            JsonValueKind.String => decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out number),
            _ => false
        };
    }
}

/// <summary>How a round-trip field is compared.</summary>
public enum RoundTripKind
{
    /// <summary>The same JSON value.</summary>
    Value,

    /// <summary>The same calendar day, whatever time or zone either side adds.</summary>
    CalendarDay,

    /// <summary>The same set of ids, sent as numbers or as objects with an <c>id</c>.</summary>
    IdList,

    /// <summary>Every sent id comes back; ids the service adds are reported as a warning.</summary>
    IdsIncluded,

    /// <summary>The same moment to the second, whether either side writes Unix seconds or ISO 8601 text.</summary>
    SameMoment,

    /// <summary>The same number, whether either side writes it as a JSON number or as a string.</summary>
    Decimal
}

/// <summary>
/// One value the app sends and the path where the service returns it. Use the named factories,
/// so a module states each field rule where it lists the field.
/// </summary>
public sealed record RoundTripField(string SentPath, string ReadPath, RoundTripKind Kind = RoundTripKind.Value)
{
    /// <summary>The same JSON value; the same number in another JSON kind is a warning.</summary>
    public static RoundTripField Same(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath);

    /// <inheritdoc cref="RoundTripKind.CalendarDay"/>
    public static RoundTripField Day(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath, RoundTripKind.CalendarDay);

    /// <inheritdoc cref="RoundTripKind.SameMoment"/>
    public static RoundTripField Moment(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath, RoundTripKind.SameMoment);

    /// <inheritdoc cref="RoundTripKind.Decimal"/>
    public static RoundTripField Number(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath, RoundTripKind.Decimal);

    /// <inheritdoc cref="RoundTripKind.IdList"/>
    public static RoundTripField Ids(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath, RoundTripKind.IdList);

    /// <inheritdoc cref="RoundTripKind.IdsIncluded"/>
    public static RoundTripField IdsKept(string sentPath, string? readPath = null)
        => new(sentPath, readPath ?? sentPath, RoundTripKind.IdsIncluded);
}
