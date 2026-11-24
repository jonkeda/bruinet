using System.Text.Json;
using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Html;

/// <summary>What <c>capture.js</c> gives for one document, as a snapshot: for a source that reads a page by script alone.</summary>
public static class CapturedDocument
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The snapshot of a document's capture; its nodes are numbered as the script numbered them.</summary>
    /// <param name="json">What the capture script returned.</param>
    /// <param name="at">When it was captured.</param>
    /// <exception cref="FormatException">The text is no capture.</exception>
    public static ScrapeSnapshot Read(string json, DateTimeOffset at)
    {
        Document document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, Json) ?? throw new FormatException("The capture is empty.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"The capture cannot be read: {e.Message}", e);
        }

        return new ScrapeSnapshot
        {
            Platform = "html",
            Location = document.Location ?? "",
            Title = document.Title ?? "",
            CapturedAt = at,
            Nodes = [.. (document.Nodes ?? []).Select(node => new ScrapeNode
            {
                Number = node.N,
                Parent = node.P,
                Kind = node.Kind == "shadow-root" ? ScrapeNodeKind.ShadowRoot : ScrapeNodeKind.Element,
                Type = node.Type ?? "",
                Role = node.Role,
                Name = node.Name,
                Text = node.Text,
                Frame = node.Frame,
                FrameTitle = node.FrameTitle,
                Bounds = node.Box is { Length: 4 } b ? new ScrapeBounds(b[0], b[1], b[2], b[3]) : null,
                Attributes = node.Attr ?? new Dictionary<string, string>(),
            })],
        };
    }

    private sealed record Document(string? Location, string? Title, Node[]? Nodes);

    private sealed record Node(int N, int P, string? Kind, string? Type, string? Role, string? Name, string? Text, string? Frame, string? FrameTitle, int[]? Box, Dictionary<string, string>? Attr);
}
