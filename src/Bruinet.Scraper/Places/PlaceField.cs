namespace Bruinet.Scraper.Places;

/// <summary>
/// One field of a place, as the list of places writes it in a line (<c>Name (text, VTextField, required, in a Card)</c>)
/// and as code reads it: the one reading of that line.
/// </summary>
/// <param name="Label">What the page calls the field.</param>
/// <param name="Kind">What the element is by itself: <c>text</c>, <c>number</c>, <c>combobox</c>, <c>checkbox</c>; empty when the line names none.</param>
public sealed record PlaceField(string Label, string Kind)
{
    private const string TableHolder = "table";

    /// <summary>The control a sign named for it; null when no sign fits.</summary>
    public string? Control { get; init; }

    /// <summary>False when the control a sign named is still to make.</summary>
    public bool ControlMade { get; init; } = true;

    /// <summary>The page marks it as required.</summary>
    public bool Required { get; init; }

    /// <summary>It cannot be filled in as the page shows it.</summary>
    public bool Disabled { get; init; }

    /// <summary>It is a table's own: a filter or a cell of a list, not a field of a form.</summary>
    public bool InTable { get; init; }

    /// <summary>What holds it with its label (<c>Card</c>, <c>FormRow</c>); null when the list names none.</summary>
    public string? Holder { get; init; }

    /// <summary>A control that exists stands for it: a sign named one and it is made.</summary>
    public bool HasControl => InTable || (Control is not null && ControlMade);

    /// <summary>The field as the list writes it.</summary>
    public string ToText() =>
        Kind.Length == 0
            ? Label
            : $"{Label} ({Kind}{(Control is null ? "" : ", " + Control + (ControlMade ? "" : "?"))}{(Required ? ", required" : "")}{(Disabled ? ", disabled" : "")}{(InTable ? ", in a table" : Holder is null ? "" : ", in a " + Holder)})";

    /// <summary>Reads a field's line of the list. A line without brackets is a label and nothing more.</summary>
    public static PlaceField Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var open = text.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0 || !text.EndsWith(')'))
            return new PlaceField(text, "");
        var parts = text[(open + 2)..^1].Split(", ");
        var field = new PlaceField(text[..open], parts[0]);
        foreach (var part in parts.Skip(1))
        {
            field = part switch
            {
                "required" => field with { Required = true },
                "disabled" => field with { Disabled = true },
                "in a " + TableHolder => field with { InTable = true },
                _ when part.StartsWith("in a ", StringComparison.Ordinal) => field with { Holder = part["in a ".Length..] },
                _ when field.Control is null => field with { Control = part.TrimEnd('?'), ControlMade = !part.EndsWith('?') },
                _ => field,
            };
        }

        return field;
    }
}
