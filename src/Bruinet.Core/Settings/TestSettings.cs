using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Bruinet.Core.Settings;

public sealed class TestSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new CaseInsensitiveEnumConverter() }
    };

    private readonly JsonObject _values;

    public TestSettings(JsonObject? values = null, IReadOnlyList<string>? sources = null)
    {
        _values = values ?? [];
        Sources = sources ?? [];
    }

    public static TestSettings Empty { get; } = new();

    public IReadOnlyList<string> Sources { get; }

    public bool TryGetValue<T>(string path, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!TryGetNode(path, out var node) || node is null)
        {
            value = default;
            return false;
        }

        value = node.Deserialize<T>(SerializerOptions);
        return true;
    }

    public T GetRequired<T>(string path)
    {
        if (!TryGetValue<T>(path, out var value) || value is null)
        {
            throw new InvalidOperationException($"Required test setting '{path}' was not found.");
        }

        return value;
    }

    public bool Contains(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return TryGetNode(path, out _);
    }

    public TestSettings GetSection(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!TryGetNode(path, out var node) || node is not JsonObject section)
        {
            return new TestSettings(sources: Sources);
        }

        return new TestSettings((JsonObject)section.DeepClone(), Sources);
    }

    public T Bind<T>()
        where T : class
    {
        return (T)Bind(typeof(T));
    }

    public object Bind(Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(settingsType);

        if (settingsType == typeof(TestSettings))
        {
            return this;
        }

        var section = settingsType.GetCustomAttribute<TestSettingsSectionAttribute>();
        var settings = section is null
            ? this
            : GetSection(section.Path);

        var result = JsonSerializer.Deserialize(
            settings._values.ToJsonString(),
            settingsType,
            SerializerOptions);

        return result ?? throw new InvalidOperationException(
            $"Test settings could not be bound to '{settingsType.FullName}'.");
    }

    public string ToJsonString(bool indented = false)
    {
        return _values.ToJsonString(new JsonSerializerOptions { WriteIndented = indented });
    }

    private bool TryGetNode(string path, out JsonNode? node)
    {
        node = _values;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (node is not JsonObject current ||
                !TryGetProperty(current, segment, out node))
            {
                node = null;
                return false;
            }
        }

        return true;
    }

    private static bool TryGetProperty(JsonObject obj, string propertyName, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(propertyName, out value))
        {
            return true;
        }

        foreach (var candidate in obj)
        {
            if (candidate.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}

/// <summary>
/// Custom JSON converter for enums that handles case-insensitive deserialization.
/// This converter attempts to match enum values regardless of case, falling back to
/// the exact case-sensitive match if no case-insensitive match is found.
/// </summary>
internal sealed class CaseInsensitiveEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(CaseInsensitiveEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

/// <summary>
/// Generic enum converter implementation that performs case-insensitive matching.
/// </summary>
/// <typeparam name="TEnum">The enum type to convert.</typeparam>
internal sealed class CaseInsensitiveEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var stringValue = reader.GetString();
                if (stringValue is null)
                {
                    throw new JsonException($"Null value cannot be converted to enum type {typeof(TEnum).Name}.");
                }

                var values = Enum.GetValues<TEnum>();
                foreach (var value in values)
                {
                    if (value.ToString().Equals(stringValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }

                // If no case-insensitive match found, try exact match (for backwards compatibility)
                try
                {
                    return Enum.Parse<TEnum>(stringValue, ignoreCase: false);
                }
                catch (ArgumentException ex)
                {
                    throw new JsonException(
                        $"Unable to convert \"{stringValue}\" to enum \"{typeof(TEnum).Name}\". Valid values are: {string.Join(", ", values)}.",
                        ex);
                }

            case JsonTokenType.Number:
                if (reader.TryGetInt32(out int intValue))
                {
                    return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                }
                if (reader.TryGetInt64(out long longValue))
                {
                    return (TEnum)Enum.ToObject(typeof(TEnum), longValue);
                }
                throw new JsonException($"Unable to convert number to enum type {typeof(TEnum).Name}.");

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} when parsing enum {typeof(TEnum).Name}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
