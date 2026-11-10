using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Core.Configuration;

/// <summary>
/// Reads a window handle from a settings file: a number, or a string in decimal or <c>0x</c> hex
/// (<c>"0x1A2B"</c>), as the desktop and MAUI fixtures' files always wrote it (public API review, proposal 11).
/// </summary>
public sealed class WindowHandleJsonConverter : JsonConverter<IntPtr?>
{
    /// <inheritdoc />
    public override IntPtr? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return new IntPtr(reader.GetInt64());
            case JsonTokenType.String:
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                {
                    return new IntPtr(hex);
                }

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    return new IntPtr(number);
                }

                break;
        }

        throw new JsonException($"A window handle is a number or a string such as \"0x1A2B\"; this is {reader.TokenType}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IntPtr? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } handle)
        {
            writer.WriteNumberValue(handle.ToInt64());
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
