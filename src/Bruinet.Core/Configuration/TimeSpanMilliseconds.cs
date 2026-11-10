using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Core.Configuration;

/// <summary>
/// The bridge between the two time rules (public API review, proposal 9): options and settings are
/// <see cref="TimeSpan"/>s, a control verb's budget is <c>int? timeoutMs</c>.
/// </summary>
public static class TimeSpanMilliseconds
{
    /// <summary>Whole milliseconds, rounded, for a verb's <c>timeoutMs</c>.</summary>
    /// <exception cref="OverflowException">The span is longer than <see cref="int.MaxValue"/> milliseconds.</exception>
    public static int ToMs(this TimeSpan value) => checked((int)Math.Round(value.TotalMilliseconds));
}

/// <summary>
/// Reads a <see cref="TimeSpan"/> setting from a number of milliseconds (<c>5000</c>) or a
/// <see cref="TimeSpan"/> string (<c>"00:00:05"</c>), and writes milliseconds, so settings files
/// written when these settings were <c>int</c> milliseconds still load.
/// </summary>
public sealed class MillisecondsTimeSpanConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc />
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return TimeSpan.FromMilliseconds(reader.GetDouble());
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
            {
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var span))
            {
                return span;
            }
        }

        throw new JsonException($"A time setting is a number of milliseconds or a TimeSpan such as \"00:00:05\"; this is {reader.TokenType}.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.TotalMilliseconds);
    }
}
