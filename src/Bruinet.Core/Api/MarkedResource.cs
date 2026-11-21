using System.Globalization;
using System.Text.Json.Nodes;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Api;

/// <summary>
/// A kind of record a test makes on a shared environment, declared once: where its list is, which field carries the
/// run's marker (<see cref="RunMarker"/>), how one is read and how one is deleted. An <see cref="ApiSession"/> sweeps
/// and deletes by it, and only ever a record whose text, as the service gives it, carries a marker.
/// </summary>
public sealed class MarkedResource
{
    /// <summary>A kind of record.</summary>
    /// <param name="name">What a message calls one, such as <c>contact</c>.</param>
    /// <param name="list">Reads the records that may carry a marker; it may read more, the marker is checked on each.</param>
    /// <param name="read">Reads one record by its id, as the service has it now; null when there is none.</param>
    /// <param name="delete">Deletes one record, given as the service has it; null when the service has no delete for it.</param>
    public MarkedResource(string name, Func<ApiSession, IReadOnlyList<JsonObject>> list, Func<ApiSession, string, JsonObject?> read, Action<ApiSession, JsonObject>? delete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(read);
        Name = name;
        List = list;
        Read = read;
        Delete = delete;
    }

    /// <summary>What a message calls one record.</summary>
    public string Name { get; }

    /// <summary>Reads the records that may carry a marker.</summary>
    public Func<ApiSession, IReadOnlyList<JsonObject>> List { get; }

    /// <summary>Reads one record by its id; null when there is none.</summary>
    public Func<ApiSession, string, JsonObject?> Read { get; }

    /// <summary>Deletes one record; null when the service has no delete for it.</summary>
    public Action<ApiSession, JsonObject>? Delete { get; }

    /// <summary>The field that carries the marker; <c>*</c> for any text field of the record's own.</summary>
    public string MarkerField { get; init; } = "name";

    /// <summary>The field that holds the record's id.</summary>
    public string IdField { get; init; } = "id";

    /// <summary>Why a marked record must stay (something refers to it); null when it may go.</summary>
    public Func<ApiSession, JsonObject, string?>? Keep { get; init; }

    /// <summary>The record's text that carries a run's marker; null when none does.</summary>
    /// <param name="record">The record.</param>
    public string? MarkedText(JsonObject record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (MarkerField != "*")
        {
            return Text(record[MarkerField]) is { } text && RunMarker.IsMarked(text) ? text : null;
        }

        return record.Select(field => Text(field.Value)).FirstOrDefault(RunMarker.IsMarked);
    }

    /// <summary>The record's id as text; null when it has none.</summary>
    /// <param name="record">The record.</param>
    public string? Id(JsonObject record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record[IdField] is JsonValue value
            ? value.TryGetValue<long>(out var number) ? number.ToString(CultureInfo.InvariantCulture)
                : value.TryGetValue<string>(out var text) ? text
                : value.ToJsonString()
            : null;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>A record tests rely on, with a fixed name: made once, never deleted.</summary>
/// <param name="Name">The record's name, which says what it is.</param>
/// <param name="IsThere">Whether the environment has it already.</param>
/// <param name="Make">Makes it.</param>
public sealed record SeedRecord(string Name, Func<ApiSession, bool> IsThere, Action<ApiSession> Make);

/// <summary>The seed of an environment: make what is not there yet, leave what is.</summary>
public static class ApiSeed
{
    /// <summary>Makes the records that are not there yet, in the order given (a later one may need an earlier one).</summary>
    /// <param name="session">The session, as an account that writes.</param>
    /// <param name="records">The records.</param>
    /// <returns>The names of what was made now; empty when everything was there.</returns>
    public static IReadOnlyList<string> Ensure(ApiSession session, IEnumerable<SeedRecord> records)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(records);
        var made = new List<string>();
        foreach (var record in records)
        {
            if (!record.IsThere(session))
            {
                record.Make(session);
                made.Add(record.Name);
            }
        }

        return made;
    }
}
