using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Places;

/// <summary>
/// One thing known of the values a field takes, with how sure it is: for a spec's field table, where a cell is a fact
/// and never a guess. In the list of places it is a line, <c>Name: max length 100 (stated, maxlength)</c>.
/// </summary>
/// <param name="Field">The field's label.</param>
/// <param name="Name">What it is about: <c>max length</c>, <c>min length</c>, <c>min</c>, <c>max</c>, <c>step</c>, <c>pattern</c>, <c>input mode</c>, <c>autocomplete</c>.</param>
/// <param name="Value">Its value, as the source gave it.</param>
/// <param name="Level">How sure: <see cref="StatedLevel"/> (the page says so itself), <see cref="SeenLevel"/> (the app did it when asked), <see cref="ReadLevel"/> (a script says so; nobody tried).</param>
/// <param name="Source">Where it came from: an attribute's name, what was typed, a script.</param>
public sealed partial record FieldFact(string Field, string Name, string Value, string Level, string Source)
{
    /// <summary>The page says so itself: an attribute of the field.</summary>
    public const string StatedLevel = "stated";

    /// <summary>The app did it when asked: a value was typed or saved, and the form answered.</summary>
    public const string SeenLevel = "seen";

    /// <summary>A script says so and nobody tried: a lead, an open question in a spec.</summary>
    public const string ReadLevel = "read";

    // The attributes that state a fact, each with what the fact is called.
    private static readonly (string Attribute, string Name)[] Attributes =
    [
        ("maxlength", "max length"), ("minlength", "min length"), ("min", "min"), ("max", "max"), ("step", "step"),
        ("pattern", "pattern"), ("inputmode", "input mode"), ("autocomplete", "autocomplete"),
    ];

    /// <summary>What a fact is called when it is what the form said of a field that was left empty.</summary>
    public const string MessageWhenEmpty = "message when empty";

    /// <summary>
    /// What a form said when a test saved it empty, as facts that were seen: each of <paramref name="said"/> is a
    /// field and its message (<c>Name: Name is required</c>).
    /// </summary>
    public static IReadOnlyList<FieldFact> SavedEmpty(IEnumerable<string> said)
    {
        ArgumentNullException.ThrowIfNull(said);
        return [.. said.Select(each => (Text: each, At: each.IndexOf(": ", StringComparison.Ordinal))).Where(each => each.At > 0 && each.At + 2 < each.Text.Length)
            .Select(each => new FieldFact(each.Text[..each.At], MessageWhenEmpty, each.Text[(each.At + 2)..], SeenLevel, "saved empty"))];
    }

    /// <summary>The fact as a line of the list.</summary>
    public string ToText() => $"{Field}: {Name} {Value} ({Level}, {Source})";

    /// <summary>A line of the list as a fact; null when it is none.</summary>
    public static FieldFact? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = Line().Match(line);
        if (!match.Success)
            return null;
        // A label may hold a colon itself (Start date: Time): the field ends where the first fact's name starts.
        var body = match.Groups["body"].Value;
        var found = Attributes.Select(each => each.Name).Append(MessageWhenEmpty).Select(name => (Name: name, At: body.IndexOf($": {name} ", StringComparison.Ordinal)))
            .Where(each => each.At > 0).OrderBy(each => each.At).ThenByDescending(each => each.Name.Length).FirstOrDefault();
        return found.Name is null ? null : new FieldFact(body[..found.At], found.Name, body[(found.At + found.Name.Length + 3)..], match.Groups["level"].Value, match.Groups["source"].Value);
    }

    /// <summary>What a field's own attributes state of the values it takes.</summary>
    /// <param name="field">The field's label.</param>
    /// <param name="attributes">The field's attributes, as a capture kept them.</param>
    public static IReadOnlyList<FieldFact> Stated(string field, IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(attributes);
        // "off" and "on" say nothing of the value; a counted id in a pattern is not expected.
        return field.Length == 0 ? [] :
        [
            .. Attributes.Where(each => attributes.TryGetValue(each.Attribute, out var value) && value.Trim().Length > 0 && !(each.Attribute == "autocomplete" && value.Trim() is "off" or "on" or "nope" or "new-password"))
                .Select(each => new FieldFact(field, each.Name, attributes[each.Attribute].Trim(), StatedLevel, each.Attribute)),
        ];
    }

    /// <summary>
    /// A place's fields as a spec's field table: Field, Kind, Required, Min, Max and where each limit came from. Min and
    /// Max are characters for a text and a value for a number. A cell is a fact that was stated or seen; a fact that
    /// was only read is given as a question; "?" is what nobody knows yet.
    /// </summary>
    /// <param name="place">The place.</param>
    public static string Table(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var facts = place.FieldFacts.Select(Parse).OfType<FieldFact>().ToList();
        var text = new System.Text.StringBuilder("| Field | Kind | Required | Min | Max | Message | From |\n| --- | --- | :---: | --- | --- | --- | --- |\n");
        foreach (var field in place.Fields.Select(PlaceField.Parse).Where(field => !field.InTable))
        {
            var number = field.Kind is "number" or "range";
            var own = facts.Where(fact => fact.Field == field.Label).ToList();
            var (min, max) = (own.FirstOrDefault(fact => fact.Name == (number ? "min" : "min length")), own.FirstOrDefault(fact => fact.Name == (number ? "max" : "max length")));
            // A test names a field as its member is named ("Name field" for a member NameField): the label's words decide.
            var message = facts.FirstOrDefault(fact => fact.Name == MessageWhenEmpty && (fact.Field == field.Label || string.Equals(Words(fact.Field), Words(field.Label), StringComparison.OrdinalIgnoreCase)));
            var from = string.Join("; ", new[] { min, max, message }.OfType<FieldFact>().Select(fact => $"{fact.Name}: {fact.Level}, {fact.Source}").Distinct(StringComparer.Ordinal));
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"| {field.Label.Replace("|", "/", StringComparison.Ordinal)} | {field.Kind} | {(field.Required ? "☑" : "☐")} | {Cell(min)} | {Cell(max)} | {message?.Value ?? ""} | {from} |\n");
        }

        return text.ToString();
    }

    private static string Words(string label) => new string([.. label.Where(char.IsLetterOrDigit)]).Replace("field", "", StringComparison.OrdinalIgnoreCase);

    // A fact someone saw or the page states is a cell; a fact that was only read is a question; nothing known is "?".
    private static string Cell(FieldFact? fact) => fact is null ? "?" : fact.Level == ReadLevel ? $"{fact.Value}?" : fact.Value;

    [GeneratedRegex(@"^(?<body>.+) \((?<level>stated|seen|read), (?<source>[^()]+)\)$")]
    private static partial Regex Line();
}
