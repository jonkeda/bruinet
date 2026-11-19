using System.Text.Json.Serialization;

namespace Bruinet.Scraper.Places;

/// <summary>How a place is reached: what a page object's opener, or a test's first step, has to do.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpenerKind>))]
public enum OpenerKind
{
    /// <summary>Not known: the place was found before the walk kept this, or without a press.</summary>
    Unknown,

    /// <summary>Its own address.</summary>
    Address,

    /// <summary>A link on another page.</summary>
    Link,

    /// <summary>A tab of its page.</summary>
    Tab,

    /// <summary>A button of the page that adds a record (<c>+ Contact</c>).</summary>
    AddButton,

    /// <summary>A button of a titled part of the page, named after that part (<c>Avatar: Edit</c>).</summary>
    CardButton,

    /// <summary>A button in a row of a list: it needs a row to press.</summary>
    RowButton,

    /// <summary>A row of a list, pressed: it needs a row, and leads to a record's own page.</summary>
    Row,

    /// <summary>Another button of the page.</summary>
    Button,
}

/// <summary>How a place is reached.</summary>
/// <param name="Kind">By what.</param>
/// <param name="Label">What was pressed, as the page says it; empty for an address.</param>
/// <param name="On">The place it was pressed on; empty for an address.</param>
public sealed record PlaceOpener(OpenerKind Kind, string Label, string On)
{
    /// <summary>What the walk calls the first row of a table it pressed.</summary>
    public const string FirstRow = "the first row";

    /// <summary>
    /// How a place is reached: what the walk kept when it found the place (<see cref="Place.Opener"/>), else what its
    /// key and <see cref="Place.Via"/> say. A place found before the walk kept openers cannot say that its button was in
    /// a row: it reads as <see cref="OpenerKind.Button"/> until its page is walked again.
    /// </summary>
    public static PlaceOpener Of(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var (label, on) = Pressed(place);
        if (place.Opener != OpenerKind.Unknown)
            return new PlaceOpener(place.Opener, label, on);
        if (place.Kind == PlaceKind.Tab)
            return new PlaceOpener(OpenerKind.Tab, label, on);
        if (place.Kind == PlaceKind.Page && label.Length == 0)
        {
            return place.Via is { } via && via.StartsWith("a link on ", StringComparison.Ordinal)
                ? new PlaceOpener(place.Address is null ? OpenerKind.Link : OpenerKind.Address, "", via["a link on ".Length..])
                : new PlaceOpener(place.Address is null ? OpenerKind.Unknown : OpenerKind.Address, "", "");
        }

        return new PlaceOpener(KindOf(label, isTab: false, inRow: false), label, on);
    }

    /// <summary>The kind of a press, from what the walk knows of the element it pressed.</summary>
    /// <param name="label">What the element says.</param>
    /// <param name="isTab">It is a tab.</param>
    /// <param name="inRow">It is in a row of a table.</param>
    public static OpenerKind KindOf(string label, bool isTab, bool inRow)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (isTab)
            return OpenerKind.Tab;
        if (label.StartsWith(FirstRow, StringComparison.Ordinal))
            return OpenerKind.Row;
        if (inRow)
            return OpenerKind.RowButton;
        if (label.StartsWith('+'))
            return OpenerKind.AddButton;
        // The walk names a titled part's own button after the part: "Avatar: Edit".
        return label.Contains(": ", StringComparison.Ordinal) ? OpenerKind.CardButton : OpenerKind.Button;
    }

    // What was pressed and where: from "'label' on place" as the walk writes it, else from the key's last part.
    private static (string Label, string On) Pressed(Place place)
    {
        if (place.Via is { } via && via.StartsWith('\'') && via.LastIndexOf("' on ", StringComparison.Ordinal) is var at and > 0)
            return (via[1..at], via[(at + "' on ".Length)..]);
        var part = place.Key.LastIndexOf(" › ", StringComparison.Ordinal);
        return part < 0 ? ("", "") : (place.Key[(part + 3)..], place.Key[..part]);
    }
}
