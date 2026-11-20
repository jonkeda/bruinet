using System.Globalization;
using System.Text;

namespace Bruinet.Scraper.Places;

/// <summary>
/// A place's fields as the sections of a screen's spec: the field table, what more there is to say of a field, and
/// what is still open. Every cell is a fact that was stated or seen, a dash where a limit does not apply, or the word
/// "open" with a question under the tables: no cell is a guess and none is a "?".
/// </summary>
public static class FieldSpec
{
    private static readonly string[] Texts = ["text", "textarea", "email", "tel", "url", "password", "search"];

    /// <summary>The sections, as Markdown.</summary>
    /// <param name="place">The place.</param>
    public static string Of(Place place)
    {
        ArgumentNullException.ThrowIfNull(place);
        var facts = place.FieldFacts.Select(FieldFact.Parse).OfType<FieldFact>().ToList();
        var table = new StringBuilder("## The fields\n\nIn the order on the screen. Min and Max are characters for a text and a value for a number. A dash means the\nlimit does not apply; \"open\" means nobody knows yet, and the question is under the tables.\n\n| Field | Kind | Required | Min | Max | Message |\n| --- | --- | :---: | --- | --- | --- |\n");
        var (more, open) = (new List<string>(), new List<string>());
        foreach (var field in place.Fields.Select(PlaceField.Parse).Where(field => !field.InTable))
        {
            var label = field.Label.Replace("|", "/", StringComparison.Ordinal);
            var named = FieldFact.Named(facts, field.Label);
            var kind = Kind(field);
            var (min, max) = Limits(field, kind, named, label, open);
            var empty = named.FirstOrDefault(fact => fact.Name == FieldFact.MessageWhenEmpty);
            if (field.Required && empty is null)
                open.Add($"{label}: what the form says when it is left empty. No test saved the form without it.");
            table.Append(CultureInfo.InvariantCulture, $"| {label} | {kind} | {(field.Required ? "☑" : "☐")} | {min} | {max} | {empty?.Value ?? (field.Required ? "open" : "")} |\n");
            more.AddRange(More(label, named));
        }

        if (more.Count > 0)
            table.Append("\n## More about a field\n\nWhat the page states or the app did beyond a limit, each with where it comes from.\n\n| Field | Rule |\n| --- | --- |\n").AppendJoin('\n', more).Append('\n');
        if (open.Count > 0)
            table.Append("\n## Open questions\n\n").AppendJoin('\n', open.Select(each => "- " + each)).Append('\n');
        return table.ToString();
    }

    // The least and the most a field takes, as cells; what is not known is "open" and a question.
    private static (string Min, string Max) Limits(PlaceField field, string kind, List<FieldFact> named, string label, List<string> open)
    {
        var number = kind is "number" or "range";
        if (!number && !Texts.Contains(kind, StringComparer.Ordinal))
            return ("–", "–");
        var (least, most) = (FieldFact.Surest(named, number ? "min" : "min length"), FieldFact.Surest(named, number ? "max" : "max length"));
        var takes = named.FirstOrDefault(fact => fact.Name == FieldFact.Takes);
        // A text that must be given has one character at least; one that may be left empty has no least.
        var min = least is not null ? Cell(least, label, "least", open) : number ? Open(label, "least", open) : field.Required ? "1" : "–";
        var max = most is not null ? Cell(most, label, number ? "most" : "longest", open)
            : takes is not null ? $"{takes.Value} or more"
            : Open(label, number ? "most" : "longest", open);
        if (most is null && takes is not null)
            open.Add($"{label}: its longest. A save with {takes.Value} characters was taken ({takes.Source}); where it ends was not tried.");
        return (min, max);
    }

    // A fact that was stated or seen is the cell; one that was only read is a question with its value as the proposal.
    private static string Cell(FieldFact fact, string label, string what, List<string> open)
    {
        if (fact.Level != FieldFact.ReadLevel)
            return fact.Value;
        open.Add($"{label}: its {what}. {fact.Source} says {fact.Value}; nobody tried it on the app.");
        return "open";
    }

    private static string Open(string label, string what, List<string> open)
    {
        open.Add($"{label}: its {what}. No attribute states one and no test found one.");
        return "open";
    }

    // What there is to say of a field beyond its row: a pattern it states, and what it said of a value that did not fit.
    private static IEnumerable<string> More(string label, List<FieldFact> named)
    {
        foreach (var fact in named)
        {
            var rule = fact.Name switch
            {
                "pattern" => $"A value shall match `{fact.Value}`",
                "step" => $"A value goes by steps of {fact.Value}",
                FieldFact.MessageWhenWrong => $"A value that does not fit is answered with \"{fact.Value}\"",
                FieldFact.MessageWhenTooLong => $"A value that is too long is answered with \"{fact.Value}\"",
                _ => null,
            };
            if (rule is not null)
                yield return $"| {label} | {rule} ({fact.Level}, {fact.Source}). |";
        }
    }

    // What a reader calls the field: the element's own kind, or what its control is by its name.
    private static string Kind(PlaceField field)
    {
        var control = field.Control ?? string.Empty;
        foreach (var (word, kind) in new[] { ("Select", "select"), ("Autocomplete", "select"), ("Combobox", "select"), ("Radio", "choice"), ("Date", "date"), ("Time", "time"), ("Checkbox", "checkbox"), ("Switch", "checkbox") })
        {
            if (control.Contains(word, StringComparison.Ordinal))
                return kind;
        }

        return field.Kind.Length == 0 ? "text" : field.Kind;
    }
}
