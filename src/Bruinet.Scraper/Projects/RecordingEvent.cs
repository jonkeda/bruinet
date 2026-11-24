using System.Globalization;
using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Projects;

/// <summary>
/// One thing that happened in a recording session: a page capture, or (in a session the Recorder made) a press of
/// a person, which says how the page capture after it was reached. Never a key or a typed text.
/// </summary>
public sealed record RecordingEvent
{
    /// <summary>The kind of a page capture.</summary>
    public const string PageKind = "page";

    /// <summary>Its number in the session, from 1.</summary>
    public int Number { get; init; }

    /// <summary>The kind of a person's press.</summary>
    public const string PressKind = "press";

    /// <summary>What it is: <see cref="PageKind"/> or <see cref="PressKind"/>.</summary>
    public string Kind { get; init; } = PageKind;

    /// <summary>When it happened.</summary>
    public DateTimeOffset At { get; init; }

    /// <summary>Where the app was.</summary>
    public string Location { get; init; } = "";

    /// <summary>The page's key (<see cref="PageKey"/>).</summary>
    public string Page { get; init; } = "";

    /// <summary>The page's title.</summary>
    public string Title { get; init; } = "";

    /// <summary>What the user called the page, if anything; for a press, what was pressed, as the page says it.</summary>
    public string? Name { get; init; }

    /// <summary>The snapshot file, in the session folder.</summary>
    public string? Snapshot { get; init; }

    /// <summary>The screenshot file, in the session folder.</summary>
    public string? Screenshot { get; init; }

    /// <summary>For a press, what was pressed: <c>button</c>, <c>link</c>, <c>tab</c>, <c>row</c>, <c>clickable</c>.</summary>
    public string? Role { get; init; }

    /// <summary>For a press, a CSS locator of what was pressed, when it has something stable to be found by.</summary>
    public string? Target { get; init; }

    /// <summary>For a press, the pressed node's number in the page capture before it; 0 when it is in none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Node { get; init; }

    /// <summary>How many nodes the snapshot has.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Nodes { get; init; }

    /// <summary>The name to show: the user's, else the title, else the page key.</summary>
    [JsonIgnore]
    public string DisplayName => Name ?? (Title.Length > 0 ? Title : Page);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Number} {Kind} {(Kind == PressKind ? Name : Page)}");
}
