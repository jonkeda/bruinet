using System.Text.Json.Serialization;
using Bruinet.Scraper.Places;

namespace Bruinet.Scraper.Factory;

/// <summary>A field of a kept place that reads differently now than the list holds it.</summary>
/// <param name="Place">The place.</param>
/// <param name="Label">The field's label.</param>
/// <param name="Before">The field as the list holds it; null when the list has no such field.</param>
/// <param name="After">The field as it reads now; null when it is not read any more.</param>
public sealed record FieldChange(string Place, string Label, string? Before, string? After)
{
    /// <summary>A made control stands for it now, and none did.</summary>
    public bool GainedControl => After is not null && PlaceField.Parse(After).HasControl && !(Before is not null && PlaceField.Parse(Before).HasControl);

    /// <summary>A made control stood for it, and none does now.</summary>
    public bool LostControl => Before is not null && PlaceField.Parse(Before).HasControl && !(After is not null && PlaceField.Parse(After).HasControl);
}

/// <summary>A class drafted from a place that would be drafted differently now.</summary>
/// <param name="Class">The class.</param>
/// <param name="Place">The place it was drafted from.</param>
/// <param name="Added">Members a draft has now that the class does not.</param>
/// <param name="Removed">Members the class has that a draft no longer gives.</param>
/// <param name="Note">What else is the matter: written differently though the members are the same, or why it cannot be drafted; empty when the two lists say it.</param>
public sealed record ClassChange(string Class, string Place, IReadOnlyList<string> Added, IReadOnlyList<string> Removed, string Note);

/// <summary>A place that owes a class and cannot be drafted, with why.</summary>
public sealed record NotDrafted(string Place, string Reason);

/// <summary>
/// What reading and drafting every kept place again gives, held against the list and the suite as they are: the
/// measure of a change to a sign, a holder, a template or the reader, without opening the app.
/// </summary>
/// <param name="Places">How many kept places were read again.</param>
/// <param name="Classes">How many drafted classes were drafted again and compared.</param>
/// <param name="Fields">The fields that read differently.</param>
/// <param name="Changed">The classes that would be drafted differently.</param>
/// <param name="WouldDraft">How many places that owe a class can be drafted now.</param>
/// <param name="CannotDraft">The places that owe a class and cannot be drafted, each with why.</param>
/// <param name="Seconds">How long it took.</param>
public sealed record ReplayReport(int Places, int Classes, IReadOnlyList<FieldChange> Fields, IReadOnlyList<ClassChange> Changed, int WouldDraft, IReadOnlyList<NotDrafted> CannotDraft, double Seconds)
{
    /// <summary>Fields a made control stands for now, and none did.</summary>
    public int ControlsGained => Fields.Count(each => each.GainedControl);

    /// <summary>Fields a made control stood for, and none does now.</summary>
    public int ControlsLost => Fields.Count(each => each.LostControl);

    /// <summary>True when nothing reads or drafts differently.</summary>
    public bool Same => Fields.Count == 0 && Changed.Count == 0;
}

/// <summary>Who is right where an example class and its place differ.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BenchVerdict>))]
public enum BenchVerdict
{
    /// <summary>The place shows it and the example does not name it: a draft has more than the example.</summary>
    DraftIsRight,

    /// <summary>The example names it, the capture shows its text, and the reading does not give it: the Scraper missed it.</summary>
    ExampleIsRight,

    /// <summary>The example names it and the capture does not show its text: the example is old, or names what only shows later.</summary>
    NotOnThePage,
}

/// <summary>One difference between an example class and what its place reads as.</summary>
public sealed record BenchDifference(string Class, string Place, string Label, BenchVerdict Verdict);

/// <summary>The examples (<c>## Examples</c> of a project's config) held against their places.</summary>
/// <param name="Examples">How many examples were compared.</param>
/// <param name="Differences">Where an example and its place differ.</param>
/// <param name="Problems">Examples that could not be compared: a place or a class that is not there.</param>
public sealed record BenchReport(int Examples, IReadOnlyList<BenchDifference> Differences, IReadOnlyList<string> Problems)
{
    /// <summary>What counts against the Scraper: what an example names, the page shows, and the reading misses.</summary>
    public int Missed => Differences.Count(difference => difference.Verdict == BenchVerdict.ExampleIsRight);
}
