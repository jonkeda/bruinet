using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Testing;

/// <summary>
/// What a drafted test was made from (R8): <c>// &lt;recorded session="…" hash="…" /&gt;</c> at the top of an
/// xUnit draft, and a <c>| Recorded | &lt;session&gt; &lt;hash&gt; |</c> row in a scenario's <c>## Metadata</c>. The
/// hash is the recording's (<see cref="Projects.RecordingSession.Hash"/>), so a view can tell when it changed since.
/// </summary>
public sealed partial record RecordedHeader(string Session, string Hash)
{
    /// <summary>The metadata field a scenario draft names its recording in.</summary>
    public const string MetadataField = "Recorded";

    /// <summary>The header as a C# comment line.</summary>
    public override string ToString() => $"// <recorded session=\"{Session}\" hash=\"{Hash}\" />";

    /// <summary>The value of the scenario's <c>Recorded</c> metadata row.</summary>
    public string MetadataValue => $"{Session} {Hash}";

    /// <summary>The header of a drafted C# file or scenario, or null: it is looked for in the first lines.</summary>
    public static RecordedHeader? Find(string text)
    {
        using var reader = new StringReader(text);
        for (var i = 0; i < 20 && reader.ReadLine() is { } line; i++)
        {
            if (Code().Match(line) is { Success: true } code)
                return new(code.Groups[1].Value, code.Groups[2].Value);
            if (Metadata().Match(line) is { Success: true } row)
                return new(row.Groups[1].Value, row.Groups[2].Value);
        }
        return null;
    }

    [GeneratedRegex(@"^\s*//\s*<recorded\s+session=""([^""]*)""\s+hash=""([^""]*)""\s*/>\s*$")]
    private static partial Regex Code();

    [GeneratedRegex(@"^\|\s*Recorded\s*\|\s*(\S+)\s+(\S+)\s*\|\s*$")]
    private static partial Regex Metadata();
}
