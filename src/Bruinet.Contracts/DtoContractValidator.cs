using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bruinet.Contracts;

/// <summary>
/// Checks a JSON response against the DTO type the app deserializes it into, using the app's
/// own serializer options.
/// </summary>
/// <remarks>
/// Failures: the body does not deserialize, a field has the wrong JSON kind, a required field is
/// missing or <c>null</c>, or a coded value is unknown. Dates and decimals are strict: a date-time
/// needs a time zone offset, a date-only value must be <c>yyyy-MM-dd</c>, and a decimal must be a
/// JSON number that keeps its precision. Warnings: a DTO field is absent, a field is not in the
/// DTO, a non-nullable DTO property receives <c>null</c>, or a whole number arrives as a string.
/// </remarks>
public static class DtoContractValidator
{
    public static ContractResult Validate(string? json, DtoContract contract, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(options);

        var result = new ContractResult(contract.Endpoint);
        if (string.IsNullOrWhiteSpace(json))
        {
            result.Add(ContractSeverity.Failure, "$", "Response body is empty.");
            return result;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            result.Add(ContractSeverity.Failure, "$", $"Body is not valid JSON: {ex.Message}");
            return result;
        }

        if (!options.IsReadOnly)
        {
            options = new JsonSerializerOptions(options);
            options.MakeReadOnly(populateMissingResolver: true);
        }

        using (document)
        {
            try
            {
                JsonSerializer.Deserialize(json, contract.DtoType, options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
            {
                var path = ex is JsonException { Path: { Length: > 0 } jsonPath } ? jsonPath : "$";
                result.Add(ContractSeverity.Failure, path, $"The app's DTO cannot read the body: {ex.GetType().Name}: {FirstLine(ex.Message)}");
            }

            var walker = new Walker(contract, options, result);
            var root = document.RootElement;
            result.ItemCount = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : 1;
            walker.Walk(root, contract.DtoType, "$");

            foreach (var required in contract.Required.Where(path => !walker.VisitedPatterns.Contains(path)))
                result.Add(ContractSeverity.Warning, required, "Required path was never reached, so it was not checked.");
        }

        return result;
    }

    /// <summary>True when <paramref name="text"/> is exactly a calendar date as <c>yyyy-MM-dd</c>.</summary>
    public static bool IsDateOnly(string? text)
        => text is { Length: 10 }
            && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>True when an ISO 8601 date-time ends with <c>Z</c> or a <c>+hh:mm</c> offset.</summary>
    public static bool HasOffset(string text)
        => System.Text.RegularExpressions.Regex.IsMatch(text, @"T.*(?:Z|[+-]\d{2}(?::?\d{2})?)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string FirstLine(string message)
    {
        var index = message.IndexOfAny(['\r', '\n']);
        return index < 0 ? message : message[..index];
    }

    private sealed class Walker(DtoContract contract, JsonSerializerOptions options, ContractResult result)
    {
        private JsonNumberHandling? _declaredNumberHandling;

        public HashSet<string> VisitedPatterns { get; } = new(StringComparer.Ordinal);

        public void Walk(JsonElement element, Type type, string location)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(JsonElement) || type == typeof(object) || element.ValueKind == JsonValueKind.Null)
                return;

            var typeInfo = options.GetTypeInfo(type);
            switch (typeInfo.Kind)
            {
                case JsonTypeInfoKind.Enumerable:
                    WalkArray(element, typeInfo, location);
                    break;
                case JsonTypeInfoKind.Dictionary:
                    if (element.ValueKind != JsonValueKind.Object)
                        Fail(location, $"Expected an object, got {Describe(element)}.");
                    break;
                case JsonTypeInfoKind.Object:
                    WalkObject(element, typeInfo, location);
                    break;
                default:
                    CheckPrimitive(element, type, location);
                    break;
            }
        }

        private void WalkArray(JsonElement element, JsonTypeInfo typeInfo, string location)
        {
            if (element.ValueKind != JsonValueKind.Array)
            {
                Fail(location, $"Expected an array, got {Describe(element)}.");
                return;
            }

            var elementType = typeInfo.ElementType ?? typeof(object);
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                var itemLocation = $"{location}[{index++}]";
                if (item.ValueKind == JsonValueKind.Null)
                    result.Add(ContractSeverity.Warning, itemLocation, "Array item is null.");
                else
                    Walk(item, elementType, itemLocation);
            }
        }

        private void WalkObject(JsonElement element, JsonTypeInfo typeInfo, string location)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                Fail(location, $"Expected an object, got {Describe(element)}.");
                return;
            }

            var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var properties = typeInfo.Properties
                .Where(property => property.Set is not null && !property.IsExtensionData)
                .ToList();
            var hasExtensionData = typeInfo.Properties.Any(property => property.IsExtensionData);
            var dtoNames = new HashSet<string>(properties.Select(property => property.Name), comparer);

            foreach (var property in properties)
            {
                var childLocation = $"{location}.{property.Name}";
                var pattern = ContractResult.CollapseIndexes(childLocation);
                VisitedPatterns.Add(pattern);
                var isRequired = contract.Required.Contains(pattern);

                if (!TryGetProperty(element, property.Name, comparer, out var value))
                {
                    result.Add(isRequired ? ContractSeverity.Failure : ContractSeverity.Warning, childLocation,
                        isRequired ? "Required field is missing." : "DTO field is not in the response.");
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Null)
                {
                    if (isRequired)
                        Fail(childLocation, "Required field is null.");
                    else if (!IsNullable(property))
                        result.Add(ContractSeverity.Warning, childLocation, "Field is null, but the DTO property is non-nullable.");
                    continue;
                }

                if (contract.CodedValues.TryGetValue(pattern, out var codedType))
                    CheckCodedValue(value, codedType, childLocation);

                if (property.CustomConverter is not null)
                    continue;

                // A property-level [JsonNumberHandling] records that the service sends numbers as strings.
                var outerHandling = _declaredNumberHandling;
                _declaredNumberHandling = property.NumberHandling;
                Walk(value, property.PropertyType, childLocation);
                _declaredNumberHandling = outerHandling;
            }

            foreach (var jsonProperty in element.EnumerateObject())
            {
                if (dtoNames.Contains(jsonProperty.Name))
                    continue;

                var childLocation = $"{location}.{jsonProperty.Name}";
                if (contract.KnownUnmapped.Contains(ContractResult.CollapseIndexes(childLocation)))
                    continue;

                result.Add(ContractSeverity.Warning, childLocation, hasExtensionData
                    ? "Field is not in the DTO; it lands in ExtensionData."
                    : "Field is not in the DTO and is ignored.");
            }
        }

        private void CheckPrimitive(JsonElement value, Type type, string location)
        {

            if (type == typeof(string))
            {
                if (value.ValueKind != JsonValueKind.String)
                    Fail(location, $"Expected a string, got {Describe(value)}.");
                return;
            }

            if (type == typeof(bool))
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    Fail(location, $"Expected a boolean, got {Describe(value)}.");
                return;
            }

            if (type.IsEnum)
            {
                CheckCodedValue(value, type, location);
                return;
            }

            if (IsNumeric(type))
            {
                CheckNumber(value, type, location);
                return;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                CheckDateTime(value, location);
                return;
            }

            if (type == typeof(DateOnly))
            {
                if (value.ValueKind != JsonValueKind.String || !IsDateOnly(value.GetString()))
                    Fail(location, $"Expected a date as yyyy-MM-dd, got {DescribeValue(value)}.");
                return;
            }

            if (type == typeof(Guid))
            {
                if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out _))
                    Fail(location, $"Expected a GUID string, got {Describe(value)}.");
            }
        }

        private void CheckNumber(JsonElement value, Type type, string location)
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                if (IsFractional(type))
                {
                    if (type == typeof(decimal) && !value.TryGetDecimal(out _))
                        Fail(location, $"Number {value.GetRawText()} does not fit a decimal.");
                    else if (type != typeof(decimal) && LosesPrecision(value, type))
                        Fail(location, $"Number {value.GetRawText()} loses precision as {type.Name}.");
                }
                else if (!value.TryGetInt64(out _) && !value.TryGetUInt64(out _))
                {
                    Fail(location, $"Expected a whole number, got {value.GetRawText()}.");
                }

                return;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact))
                {
                    if (IsFractional(type) && _declaredNumberHandling?.HasFlag(JsonNumberHandling.AllowReadingFromString) == true)
                    {
                        if (StringLosesPrecision(text!, exact, type))
                            Fail(location, $"Decimal string \"{text}\" loses precision as {type.Name}.");
                        else
                            result.Add(ContractSeverity.Warning, location,
                                $"Decimal arrives as the string \"{text}\"; the DTO property declares string reading, so the app accepts it.");
                    }
                    else if (IsFractional(type))
                        Fail(location, $"Decimal arrives as the string \"{text}\"; it must be a JSON number.");
                    else if (options.NumberHandling.HasFlag(JsonNumberHandling.AllowReadingFromString))
                        result.Add(ContractSeverity.Warning, location, "Number arrives as a string.");
                    else
                        Fail(location, $"Number arrives as the string \"{text}\", which the app cannot read.");
                    return;
                }

                if (text is not null && text.Contains(',')
                    && decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("nl-NL"), out _))
                {
                    Fail(location, $"Number \"{text}\" uses a comma as the decimal separator.");
                    return;
                }
            }

            Fail(location, $"Expected a number, got {DescribeValue(value)}.");
        }

        private void CheckDateTime(JsonElement value, string location)
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                Fail(location, $"Expected an ISO 8601 date-time string, got {Describe(value)}.");
                return;
            }

            var text = value.GetString();
            if (IsDateOnly(text))
                Fail(location, $"Expected a date-time, got the date-only value \"{text}\".");
            else if (!value.TryGetDateTimeOffset(out _))
                Fail(location, $"Expected an ISO 8601 date-time string, got \"{text}\".");
            else if (!HasOffset(text!))
                Fail(location, $"Date-time \"{text}\" has no time zone offset, so the moment is ambiguous.");
        }

        private static bool StringLosesPrecision(string text, decimal exact, Type type)
        {
            if (type == typeof(decimal))
                return false;
            var approximate = type == typeof(float)
                ? float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)
                : double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            try
            {
                return (decimal)approximate != exact;
            }
            catch (OverflowException)
            {
                return true;
            }
        }

        private static bool LosesPrecision(JsonElement value, Type type)
        {
            if (!value.TryGetDecimal(out var exact))
                return false;
            var approximate = type == typeof(float) ? (double)value.GetSingle() : value.GetDouble();
            try
            {
                return (decimal)approximate != exact;
            }
            catch (OverflowException)
            {
                return true;
            }
        }

        private void CheckCodedValue(JsonElement value, Type enumType, string location)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                if (!Enum.IsDefined(enumType, Enum.ToObject(enumType, number)))
                    Fail(location, $"Unknown {enumType.Name} value {number}.");
                return;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    if (!Enum.IsDefined(enumType, Enum.ToObject(enumType, number)))
                        Fail(location, $"Unknown {enumType.Name} value {number}.");
                    else
                        result.Add(ContractSeverity.Warning, location, "Coded value arrives as a string.");
                    return;
                }

                if (!Enum.TryParse(enumType, text, ignoreCase: true, out _))
                    Fail(location, $"Unknown {enumType.Name} value '{text}'.");
                return;
            }

            Fail(location, $"Expected a {enumType.Name} code, got {Describe(value)}.");
        }

        private void Fail(string location, string message) => result.Add(ContractSeverity.Failure, location, message);

        private static bool TryGetProperty(JsonElement element, string name, StringComparer comparer, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (comparer.Equals(property.Name, name))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool IsNullable(JsonPropertyInfo property)
            => property.PropertyType.IsValueType
                ? Nullable.GetUnderlyingType(property.PropertyType) is not null
                : property.IsSetNullable;

        private static bool IsNumeric(Type type) => Type.GetTypeCode(type) is
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
            or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

        private static bool IsFractional(Type type) => Type.GetTypeCode(type) is
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

        private static string DescribeValue(JsonElement value)
            => value.ValueKind == JsonValueKind.String ? $"\"{value.GetString()}\"" : Describe(value);

        private static string Describe(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            JsonValueKind.Array => "an array",
            JsonValueKind.Object => "an object",
            _ => value.ValueKind.ToString()
        };
    }
}
