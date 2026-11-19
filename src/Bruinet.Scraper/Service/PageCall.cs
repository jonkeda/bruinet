namespace Bruinet.Scraper.Service;

/// <summary>
/// One call a page's script made (fetch or XHR): how a page reads and writes its data, for someone who must make or
/// delete the same record beside the screen. It holds no header, so no token.
/// </summary>
/// <param name="Method">The method, such as <c>GET</c> or <c>DELETE</c>.</param>
/// <param name="Url">The address, cut when it is long.</param>
/// <param name="Status">The answer's status.</param>
/// <param name="Body">What a writing call sent, cut when it is long; null for a call that only reads.</param>
public sealed partial record PageCall(string Method, string Url, int Status, string? Body)
{
    /// <summary>How much of an address or a body is kept.</summary>
    public const int Kept = 400;

    /// <summary>A call, with its address and body cut to <see cref="Kept"/> characters.</summary>
    public static PageCall Of(string method, string url, int status, string? body)
        => new(method, Cut(url), status, string.IsNullOrEmpty(body) ? null : Cut(body));

    /// <summary>The calls without repeats: a page that asks the same thing twice says it once.</summary>
    public static IReadOnlyList<PageCall> Distinct(IEnumerable<PageCall> calls) => [.. calls.Distinct()];

    /// <summary>The call on one line: <c>DELETE https://… -> 204</c>, and what it sent.</summary>
    public string ToText() => $"{Method} {Url} -> {Status}{(Body is null ? string.Empty : "  sent: " + Body)}";

    /// <summary>
    /// The names of the fields a body sends (a JSON object's, a form's), in their order, without any value: what a
    /// form's save asks for, as a list of places may hold it. At most twenty.
    /// </summary>
    public static IReadOnlyList<string> FieldsOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];
        var text = body.TrimStart();
        var names = text.StartsWith('{') || text.StartsWith('[')
            ? JsonName().Matches(text).Select(match => match.Groups[1].Value)
            : text.Split('&').Select(pair => Uri.UnescapeDataString(pair.Split('=')[0])).Where(name => name.Length is > 0 and <= 60 && name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or '[' or ']'));
        return [.. names.Distinct(StringComparer.Ordinal).Take(20)];
    }

    [System.Text.RegularExpressions.GeneratedRegex("\"([A-Za-z_][A-Za-z0-9_.-]{0,59})\"\\s*:")]
    private static partial System.Text.RegularExpressions.Regex JsonName();

    private static string Cut(string text) => text.Length <= Kept ? text : text[..Kept] + "…";
}
