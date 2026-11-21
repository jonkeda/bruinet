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
        var fields = place.Fields.Select(PlaceField.Parse).Where(field => !field.InTable).ToList();
        if (fields.Count == 0)
            return List(place);
        var table = new StringBuilder("## The fields\n\nIn the order on the screen. Min and Max are characters for a text and a value for a number. A dash means the\nlimit does not apply; \"open\" means nobody knows yet, and the question is under the tables.\n\n| Field | Kind | Required | Min | Max | Message |\n| --- | --- | :---: | --- | --- | --- |\n");
        var (more, open) = (new List<string>(), new List<string>());
        foreach (var field in fields)
        {
            var label = field.Label.Replace("|", "/", StringComparison.Ordinal);
            var named = FieldFact.Named(facts, field.Label);
            var kind = Kind(field.Control, field.Kind);
            var (min, max) = Limits(field, kind, named, label, open);
            var empty = named.FirstOrDefault(fact => fact.Name == FieldFact.MessageWhenEmpty);
            if (field.Required && empty is null)
                open.Add($"{label}: what the form says when it is left empty. No test saved the form without it.");
            table.Append(CultureInfo.InvariantCulture, $"| {label} | {kind} | {(field.Required ? "☑" : "☐")} | {min} | {max} | {empty?.Value ?? (field.Required ? "open" : "")} |\n");
            more.AddRange(More(label, named));
        }

        if (more.Count > 0)
            table.Append("\n## More about a field\n\nWhat the page states or the app did beyond a limit, each with where it comes from.\n\n| Field | Rule |\n| --- | --- |\n").AppendJoin('\n', more).Append('\n');
        var examples = Examples(fields, facts);
        if (examples.Count > 0)
            table.Append("\n## Examples\n\nWhat a test did on the app and what the form answered: each row was seen, none is a guess.\n\n| # | Given | Outcome | Seen |\n| --- | --- | --- | --- |\n").AppendJoin('\n', examples.Select((row, at) => $"| {at + 1} | {row}")).Append('\n');
        if (open.Count > 0)
            table.Append("\n## Open questions\n\n").AppendJoin('\n', open.Select(each => "- " + each)).Append('\n');
        return table.Append(List(place)).ToString();
    }

    // A list's columns in their order. What a column shows, sorts by and filters on is not in a capture: open.
    private static string List(Place place)
    {
        if (place.Columns.Count == 0)
            return string.Empty;
        // A field right under a column's header filters it. Where a capture shows such fields, a column without one
        // has none; where it shows none at all, nobody looked.
        var filters = place.FieldFacts.Select(FieldFact.Parse).OfType<FieldFact>().Where(fact => fact.Name == FieldFact.Filter).ToList();
        var own = place.Fields.Select(PlaceField.Parse).Where(field => field.InTable).Select(field => field.Label).ToList();
        var text = new StringBuilder("\n## The list\n\nThe columns in their order, as the page shows them. Filter is the kind of the field under the column's header.\n\n| Column | Shows | Sorts | Filter |\n| --- | --- | --- | --- |\n");
        foreach (var column in place.Columns)
        {
            var filter = filters.FirstOrDefault(fact => fact.Field == column)?.Value ?? (filters.Count > 0 ? "none" : "open");
            text.Append(CultureInfo.InvariantCulture, $"| {column.Replace("|", "/", StringComparison.Ordinal)} | open | open | {filter} |\n");
        }

        text.Append("\n## Open questions of the list\n\n- What each column shows and whether it sorts: a capture holds the headers only.\n");
        if (filters.Count == 0 && own.Count > 0)
            text.Append(CultureInfo.InvariantCulture, $"- The table has fields of its own ({string.Join(", ", own)}): which column each filters is not known.\n");
        return text.ToString();
    }

    // What was seen, as rows a reader can try again: the empty save, a value that does not fit, a save that is long.
    private static List<string> Examples(List<PlaceField> fields, List<FieldFact> facts)
    {
        var rows = new List<string>();
        var empty = facts.Where(fact => fact.Name == FieldFact.MessageWhenEmpty).ToList();
        if (empty.Count > 0)
            rows.Add($"the form as it opens, saved | the form stays, with {string.Join(" and ", empty.Select(fact => $"\"{fact.Value}\"").Distinct(StringComparer.Ordinal))} | {empty[0].Source} |");
        foreach (var field in fields)
        {
            foreach (var fact in FieldFact.Named(facts, field.Label))
            {
                var row = fact.Name switch
                {
                    FieldFact.MessageWhenWrong => $"{field.Label} holds a single letter | the field says \"{fact.Value}\" | {fact.Source} |",
                    FieldFact.MessageWhenTooLong => $"{field.Label} holds {Count(fact.Source)} characters, saved | the form stays, with \"{fact.Value}\" | {fact.Source} |",
                    FieldFact.Takes => $"{field.Label} holds {fact.Value} characters, saved | the save is taken | {fact.Source} |",
                    _ => null,
                };
                if (row is not null)
                    rows.Add(row);
            }
        }

        return rows;
    }

    // The number a source names ("typed 300 characters"); "more" when it names none.
    private static string Count(string source) => new string([.. source.SkipWhile(each => !char.IsDigit(each)).TakeWhile(char.IsDigit)]) is { Length: > 0 } digits ? digits : "more";

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
    internal static string Kind(string? named, string own)
    {
        var control = named ?? string.Empty;
        foreach (var (word, kind) in new[] { ("Select", "select"), ("Autocomplete", "select"), ("Combobox", "select"), ("Radio", "choice"), ("Date", "date"), ("Time", "time"), ("Checkbox", "checkbox"), ("Switch", "checkbox") })
        {
            if (control.Contains(word, StringComparison.Ordinal))
                return kind;
        }

        return own.Length == 0 ? "text" : own;
    }
}
