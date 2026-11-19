using Bruinet.Scraper.Snapshots;

namespace Bruinet.Scraper.Projects;

/// <summary>Applies a config's <see cref="TextMask"/>s to every text a snapshot holds, before it is written or shown.</summary>
public static class Redactor
{
    /// <summary>The snapshot with its names, texts, frame addresses, location, title and attribute values masked.</summary>
    public static ScrapeSnapshot Apply(ScrapeSnapshot snapshot, IReadOnlyList<TextMask> masks)
    {
        if (masks.Count == 0)
            return snapshot;
        return snapshot with
        {
            Location = Mask(snapshot.Location, masks)!,
            Title = Mask(snapshot.Title, masks)!,
            Nodes = snapshot.Nodes.Select(n => n with
            {
                Name = Mask(n.Name, masks),
                Text = Mask(n.Text, masks),
                Frame = Mask(n.Frame, masks),
                Attributes = n.Attributes.ToDictionary(a => a.Key, a => Mask(a.Value, masks)!, StringComparer.Ordinal),
            }).ToArray(),
        };
    }

    /// <summary>A text with every mask applied.</summary>
    public static string? Mask(string? text, IReadOnlyList<TextMask> masks)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        foreach (var mask in masks)
            text = mask.Pattern.Replace(text, mask.Replacement);
        return text;
    }
}
