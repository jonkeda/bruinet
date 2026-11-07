using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bruinet.Contracts;

/// <summary>How a write sends its body.</summary>
public enum RequestBodyKind
{
    Json,
    Form,
    Multipart
}

/// <summary>What a request field must hold. Values are checked by format, so ids, times and tokens need no normalising.</summary>
public enum ExpectedValue
{
    /// <summary>Any string.</summary>
    Text,
    Integer,
    /// <summary>An invariant number with a dot, as a number or a string.</summary>
    Decimal,
    /// <summary><c>yyyy-MM-dd</c>.</summary>
    Day,
    /// <summary>A date with a time, and optionally seconds and a zone.</summary>
    Moment,
    /// <summary><c>true</c>/<c>false</c>, or <c>0</c>/<c>1</c> in a form.</summary>
    Bool,
    /// <summary>A JSON <c>null</c>.</summary>
    Null,
    /// <summary>An empty string.</summary>
    Empty,
    /// <summary>Any value, including the whole subtree below the path.</summary>
    Any,
    /// <summary>A multipart file part.</summary>
    File,
    /// <summary>Android leaves the field out, so the app must too.</summary>
    Omitted,
    /// <summary>Android and the app differ on purpose; the field is not checked. Name the decision.</summary>
    ApprovedDifference
}

/// <summary>
/// One field of the request Android sends. Paths are dot-separated property names, with <c>[*]</c> for
/// every array element, for example <c>employees[*].id</c>. A form key <c>a[0][b]</c> reads as <c>a[*].b</c>.
/// </summary>
/// <param name="Path">The field's path in the body.</param>
/// <param name="Value">What the field must hold.</param>
/// <param name="Optional">The field may be left out, for example when the user leaves it empty; when sent it must match.</param>
/// <param name="Note">Why: the decision behind an approved difference, or the Android rule.</param>
public sealed record ExpectedField(string Path, ExpectedValue Value, bool Optional = false, string? Note = null);

/// <summary>
/// The request Android sends for one write. The live run and the hermetic mock run both compare the
/// app's request with it: a field the app adds, leaves out, or formats differently fails.
/// </summary>
/// <param name="Body">How the write sends its body.</param>
/// <param name="Source">The Android code or the parity document the expectation comes from.</param>
/// <param name="Fields">Every field the reference client sends, and the ones it leaves out on purpose.</param>
public sealed record ExpectedRequest(RequestBodyKind Body, string Source, IReadOnlyList<ExpectedField> Fields);

/// <summary>Compares a recorded request with the <see cref="ExpectedRequest"/> of its contract.</summary>
public static partial class ExpectedRequestCheck
{
    public static ContractResult Compare(DtoContract contract, string? contentType, string? body)
    {
        var expected = contract.Request
            ?? throw new ArgumentException($"{contract.Endpoint} has no expected request.", nameof(contract));
        var result = new ContractResult($"Expected request: {contract.Endpoint}");
        if (!MatchesKind(expected.Body, contentType))
        {
            result.Add(ContractSeverity.Failure, "$", $"Sent {contentType ?? "no body"}, but Android sends {expected.Body} ({expected.Source}).");
            return result;
        }

        if (body is null)
        {
            result.Add(ContractSeverity.Blocked, "$", "The request body was not recorded, so it cannot be compared.");
            return result;
        }

        IReadOnlyList<(string Path, Sent Value)> sent;
        try
        {
            sent = expected.Body == RequestBodyKind.Json ? ReadJson(body) : ReadForm(body);
        }
        catch (JsonException exception)
        {
            result.Add(ContractSeverity.Failure, "$", $"The body is not JSON: {exception.Message}");
            return result;
        }

        foreach (var field in expected.Fields)
        {
            result.ItemCount++;
            var values = sent.Where(item => Covers(field, item.Path)).Select(item => item.Value).ToList();
            var location = "$." + field.Path;
            switch (field.Value)
            {
                case ExpectedValue.ApprovedDifference:
                    continue;
                case ExpectedValue.Omitted:
                    if (values.Count > 0)
                        result.Add(ContractSeverity.Failure, location, $"Sent {values[0].Text}, but Android leaves this field out.");
                    continue;
            }

            if (values.Count == 0)
            {
                if (!field.Optional)
                    result.Add(ContractSeverity.Failure, location, $"Android sends this field as {field.Value}, but the request leaves it out.");
                continue;
            }

            foreach (var value in values.Where(value => !Matches(field.Value, value)))
                result.Add(ContractSeverity.Failure, location, $"Sent {value.Text}, but Android sends {field.Value}.");
        }

        foreach (var path in sent.Select(item => item.Path).Distinct(StringComparer.Ordinal)
            .Where(path => !expected.Fields.Any(field => Covers(field, path))))
            result.Add(ContractSeverity.Failure, "$." + path, "Sent a field Android does not send.");

        return result;
    }

    private static bool MatchesKind(RequestBodyKind kind, string? contentType) => kind switch
    {
        RequestBodyKind.Json => contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true,
        RequestBodyKind.Form => string.Equals(contentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase),
        _ => string.Equals(contentType, "multipart/form-data", StringComparison.OrdinalIgnoreCase)
    };

    private static bool Covers(ExpectedField field, string path)
        => path == field.Path
           || (field.Value is ExpectedValue.Any or ExpectedValue.ApprovedDifference or ExpectedValue.Omitted
               && (path.StartsWith(field.Path + ".", StringComparison.Ordinal) || path.StartsWith(field.Path + "[*]", StringComparison.Ordinal)));

    private static bool Matches(ExpectedValue expected, Sent value) => expected switch
    {
        ExpectedValue.Any => true,
        ExpectedValue.Null => value.Kind == JsonValueKind.Null,
        ExpectedValue.Empty => value.Kind == JsonValueKind.String && value.Text.Length == 0,
        ExpectedValue.Text => value.Kind == JsonValueKind.String && !IsFile(value.Text),
        ExpectedValue.Integer => (value.Kind == JsonValueKind.Number || value.IsForm) && IntegerPattern().IsMatch(value.Text),
        ExpectedValue.Decimal => value.Kind is JsonValueKind.Number or JsonValueKind.String && DecimalPattern().IsMatch(value.Text),
        ExpectedValue.Day => value.Kind == JsonValueKind.String && DayPattern().IsMatch(value.Text),
        ExpectedValue.Moment => value.Kind == JsonValueKind.String && MomentPattern().IsMatch(value.Text),
        ExpectedValue.Bool => value.Kind is JsonValueKind.True or JsonValueKind.False
            || (value.IsForm && value.Text is "0" or "1" or "true" or "false"),
        ExpectedValue.File => value.IsForm && IsFile(value.Text),
        _ => false
    };

    private static bool IsFile(string text) => text.StartsWith("[file ", StringComparison.Ordinal);

    private static List<(string, Sent)> ReadJson(string body)
    {
        using var document = JsonDocument.Parse(body);
        var leaves = new List<(string, Sent)>();
        Walk(document.RootElement, string.Empty, leaves);
        return leaves;
    }

    private static void Walk(JsonElement element, string path, List<(string, Sent)> leaves)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, path.Length == 0 ? property.Name : path + "." + property.Name, leaves);
                break;
            case JsonValueKind.Array when element.GetArrayLength() > 0:
                foreach (var item in element.EnumerateArray())
                    Walk(item, path + "[*]", leaves);
                break;
            case JsonValueKind.String:
                leaves.Add((path, new Sent(element.ValueKind, element.GetString()!, IsForm: false)));
                break;
            default:
                leaves.Add((path, new Sent(element.ValueKind, element.GetRawText(), IsForm: false)));
                break;
        }
    }

    private static List<(string, Sent)> ReadForm(string body)
        => body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(pair => (FormPath(Decode(pair[0])), new Sent(JsonValueKind.String, pair.Length > 1 ? Decode(pair[1]) : string.Empty, IsForm: true)))
            .ToList();

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    /// <summary>
    /// Describes a raw multipart body as the app's exchange log does: a text part keeps its value,
    /// and a file part becomes <c>[file name; type; N bytes]</c>.
    /// </summary>
    public static string DescribeMultipart(string contentType, byte[] body)
    {
        var boundary = contentType.Split(';').Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?["boundary=".Length..].Trim('"');
        if (string.IsNullOrEmpty(boundary))
            return string.Empty;

        // Latin-1 keeps one character per byte, so file sizes stay exact.
        var text = System.Text.Encoding.Latin1.GetString(body);
        var parts = new List<string>();
        foreach (var section in text.Split("--" + boundary).Skip(1))
        {
            var split = section.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (split < 0)
                continue;

            var headers = section[..split];
            var content = section[(split + 4)..];
            if (content.EndsWith("\r\n", StringComparison.Ordinal))
                content = content[..^2];
            var name = HeaderValue(headers, "name") ?? string.Empty;
            var fileName = DecodeFileNameStar(HeaderValue(headers, "filename*")) ?? HeaderValue(headers, "filename");
            var type = MultipartType().Match(headers) is { Success: true } match ? match.Groups[1].Value.Trim() : null;
            var value = fileName is null
                ? System.Text.Encoding.UTF8.GetString(System.Text.Encoding.Latin1.GetBytes(content))
                : $"[file {fileName}; {type}; {content.Length} bytes]";
            parts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return string.Join('&', parts);
    }

    /// <summary>RFC 5987 <c>utf-8''photo.jpg</c> becomes <c>photo.jpg</c>, as <c>FileNameStar</c> reads it.</summary>
    private static string? DecodeFileNameStar(string? value)
    {
        if (value is null)
            return null;
        var start = value.IndexOf("''", StringComparison.Ordinal);
        return Uri.UnescapeDataString(start < 0 ? value : value[(start + 2)..]);
    }

    private static string? HeaderValue(string headers, string parameter)
    {
        var match = Regex.Match(headers, $@";\s*{Regex.Escape(parameter)}=(""(?<v>[^""]*)""|(?<v>[^;\r\n]*))", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value : null;
    }

    /// <summary><c>a[0][b]</c> becomes <c>a[*].b</c>, and <c>a[]</c> becomes <c>a[*]</c>.</summary>
    internal static string FormPath(string key)
        => FormSegment().Replace(key, match => match.Groups[1].Value.Length == 0 || match.Groups[1].Value.All(char.IsAsciiDigit)
            ? "[*]"
            : "." + match.Groups[1].Value);

    private sealed record Sent(JsonValueKind Kind, string Text, bool IsForm);

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex FormSegment();

    [GeneratedRegex(@"^-?\d+$")]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex DecimalPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DayPattern();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?$")]
    private static partial Regex MomentPattern();

    [GeneratedRegex(@"Content-Type:\s*([^\r\n;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MultipartType();
}

/// <summary>One write as a mock backend logged it: the method, the path, and the body as the check reads it.</summary>
public sealed record RecordedWrite(string Method, string Path, string? ContentType, string? Body);

/// <summary>
/// The hermetic side of the expected request: every write a test sent to its mock backend is compared with
/// the expected request of the contract that covers it, so CI guards the request without the real service.
/// </summary>
public static class ExpectedRequestMockCheck
{
    /// <summary>The failed comparisons; a write that no contract with an expected request covers is not checked.</summary>
    public static IReadOnlyList<ContractResult> Failures(IEnumerable<DtoContract> contracts, IEnumerable<RecordedWrite> writes)
    {
        var expectations = contracts.Where(contract => contract.Request is not null).Distinct().ToList();
        var failures = new List<ContractResult>();
        foreach (var write in writes)
        {
            var contract = expectations.FirstOrDefault(candidate =>
                candidate.Method.Equals(write.Method, StringComparison.OrdinalIgnoreCase) && candidate.MatchesPath(write.Path));
            if (contract is null)
                continue;

            var result = ExpectedRequestCheck.Compare(contract, write.ContentType, write.Body);
            if (!result.Passed)
                failures.Add(result);
        }

        return failures;
    }
}
