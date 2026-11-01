using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Code;

/// <summary>
/// The line a generated file starts with, saying what it was made from:
/// <c>// &lt;scraped page="/invoices/{id}" from="2026-09-30-invoices/002-edit.nodes.jsonl" hash="1a2b3c4d" /&gt;</c> for a page object,
/// <c>// &lt;scraped control="div|dialog|modal|h2,div+" from="…" /&gt;</c> for a control. The code is the
/// registry: nothing else records what was generated.
/// </summary>
public sealed partial record ScrapedHeader
{
    /// <summary>For a page object: the page key it was made for.</summary>
    public string? Page { get; init; }

    /// <summary>For a control: the node signature it stands for.</summary>
    public string? Control { get; init; }

    /// <summary>The capture it was made from: session folder and snapshot file.</summary>
    public string? From { get; init; }

    /// <summary>For a control made from one variant of a pattern: the variant (<see cref="ControlKind.Variant"/>), so only its occurrences count.</summary>
    public string? Variant { get; init; }

    /// <summary>For a control made from a lifted pattern: its root is the pattern's node lifted (<see cref="PatternDetector.RootOf"/>).</summary>
    public bool Lifted { get; init; }

    /// <summary>For a page object: the content hash of that capture, to tell when the page has changed since.</summary>
    public string? Hash { get; init; }

    /// <summary>The header as a comment line.</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Page is not null)
            parts.Add($"page=\"{Escape(Page)}\"");
        if (Control is not null)
            parts.Add($"control=\"{Escape(Control)}\"");
        if (Variant is not null)
            parts.Add($"variant=\"{Escape(Variant)}\"");
        if (Lifted)
            parts.Add("root=\"lifted\"");
        if (From is not null)
            parts.Add($"from=\"{Escape(From)}\"");
        if (Hash is not null)
            parts.Add($"hash=\"{Escape(Hash)}\"");
        return $"// <scraped {string.Join(' ', parts)} />";
    }

    /// <summary>The header in a file's text, or null: it is looked for in the first lines only.</summary>
    public static ScrapedHeader? Find(string text)
    {
        using var reader = new StringReader(text);
        for (var i = 0; i < 10 && reader.ReadLine() is { } line; i++)
        {
            var match = Line().Match(line);
            if (!match.Success)
                continue;
            var attributes = Attribute().Matches(match.Groups[1].Value).ToDictionary(m => m.Groups[1].Value, m => Unescape(m.Groups[2].Value), StringComparer.Ordinal);
            return new ScrapedHeader
            {
                Page = attributes.GetValueOrDefault("page"),
                Control = attributes.GetValueOrDefault("control"),
                Variant = attributes.GetValueOrDefault("variant"),
                Lifted = attributes.GetValueOrDefault("root") == "lifted",
                From = attributes.GetValueOrDefault("from"),
                Hash = attributes.GetValueOrDefault("hash"),
            };
        }
        return null;
    }

    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string Unescape(string value) => value.Replace("&quot;", "\"", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);

    [GeneratedRegex(@"^\s*//\s*<scraped\s+(.*?)\s*/>\s*$")]
    private static partial Regex Line();

    [GeneratedRegex(@"(\w+)=""([^""]*)""")]
    private static partial Regex Attribute();
}
