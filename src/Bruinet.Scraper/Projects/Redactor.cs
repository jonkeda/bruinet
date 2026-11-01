using Bruinet.Core.Scraping;

namespace Bruinet.Scraper.Projects;

/// <summary>Applies a config's <see cref="TextMask"/>s to every text a snapshot or an action holds, before it is written or shown.</summary>
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

    /// <summary>
    /// The action with its value, location and path masked. A secret's value is dropped whatever the
    /// source sent: only its placeholder is kept.
    /// </summary>
    public static RecordingEvent Apply(RecordingEvent action, IReadOnlyList<TextMask> masks)
    {
        if (action.Placeholder is not null)
            action = action with { Value = null };
        if (masks.Count == 0)
            return action;
        var location = Mask(action.Location, masks)!;
        return action with
        {
            Location = location,
            Page = location.Length > 0 ? PageKey.Of(location) : action.Page,
            Value = Mask(action.Value, masks),
            Path = action.Path?.Select(s => s with { TestId = Mask(s.TestId, masks), Name = Mask(s.Name, masks), Classes = Mask(s.Classes, masks) }).ToArray(),
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
