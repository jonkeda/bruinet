using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Factory;

/// <summary>
/// The factory's review as a document (<c>review.md</c>), written from its state: for every place that still owes
/// something, the last reason it was not kept for, gathered by reason and then by what the evidence has in common.
/// It is the brief a person or an agent starts a fix from: one reason, how often, where the fix belongs, and the
/// places to run again when it is fixed. Counted, not judged: the Scraper calls no model.
/// </summary>
public static partial class FactoryReview
{
    /// <summary>The file's name.</summary>
    public const string FileName = "review.md";

    private static readonly Dictionary<string, string> Means = new(StringComparer.Ordinal)
    {
        [ReasonCodes.DoesNotBuild] = "The draft does not fit the suite: a member the base has already, or something the test presses that its page's class does not have. Fix the template, or name the base's member under `## Reserved names`; a page a person wrote may need the member the test presses.",
        [ReasonCodes.NoPageClass] = "What opens on a page cannot be drafted before the page has a class. Draft or write the page first; a page that shows nothing to check needs a class a person writes.",
        [ReasonCodes.NeedsRecord] = "The page is of one record, or the list has no row to press, and there is no record to open it on. Give the suite a record of that kind, then run the place again.",
        [ReasonCodes.LabelNotFound] = "The form does not show a label its class declares: the reading took something for a field's label that is not one. Fix a sign or a holder, or the reader.",
        [ReasonCodes.DidNotOpen] = "The test did not find what the class declares: the page or the part did not open the way the test opens it. A draft kind may be missing.",
        [ReasonCodes.Timeout] = "The environment was slow. Run the place again.",
        [ReasonCodes.Refused] = "The app refused the page to the account. A person decides which account the place is for.",
        [ReasonCodes.PassedSecondTime] = "A wait is missing: find what the test did not wait for, in the template's test or in the control.",
        [ReasonCodes.PassedThenFailed] = "The test passed and then failed on the same app: it reads or presses something before it is there. Find the step that does not wait, in the template's test or in the control.",
        [ReasonCodes.NoTemplate] = "The suite's templates lack a section the draft needs.",
        [ReasonCodes.NothingToCheck] = "The place shows nothing a test could check. Nothing to fix: a person writes a class when a test must name something there.",
        [ReasonCodes.ClassExists] = "A class of the draft's name is in the suite already. Nothing to fix unless the two are different things: then one of them needs another name.",
        [ReasonCodes.BrokeAPassingTest] = "What the loop wrote made a test fail that passed before it. The draft collides with something of the suite.",
        [ReasonCodes.Unknown] = "No rule fits. Read the evidence; when it is a kind of its own, it gets a rule in the classifier.",
    };

    /// <summary>The review.</summary>
    /// <param name="state">The factory's state.</param>
    /// <param name="owed">The keys of the places that still owe a class; a reason about another place is history, and left out.</param>
    public static string ToMarkdown(FactoryState state, IReadOnlyCollection<string> owed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(owed);
        var last = new Dictionary<string, (Reason Reason, int Loop)>(StringComparer.Ordinal);
        foreach (var loop in state.Loops)
        {
            foreach (var reason in loop.Reasons.Where(reason => reason.Place.Length > 0))
                last[reason.Place] = (reason, loop.Number);
        }

        var standing = last.Values.Where(each => owed.Contains(each.Reason.Place)).ToList();
        var never = owed.Where(key => !last.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        text.Append("# The factory's review\n\nWhy each place that still owes a class was not kept, by reason: the last reason per place. Written from\n`state.json` by `bruinet scrape scraper review` and after every batch; not edited by hand.\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{owed.Count} places owe a class: {standing.Count} with a reason, {never.Count} never tried.\n\n");
        text.Append("| Reason | Places | Fix at |\n| --- | --- | --- |\n");
        var groups = standing.GroupBy(each => each.Reason.Code, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).ToList();
        foreach (var group in groups)
            text.Append(CultureInfo.InvariantCulture, $"| [{group.Key}](#{group.Key}) | {group.Count()} | {group.First().Reason.FixAt} |\n");
        foreach (var group in groups)
        {
            text.Append(CultureInfo.InvariantCulture, $"\n## {group.Key}\n\n{Means.GetValueOrDefault(group.Key, "No description.")}\n");
            foreach (var shape in group.GroupBy(each => Shape(each.Reason.Evidence), StringComparer.Ordinal).OrderByDescending(shape => shape.Count()))
            {
                text.Append(CultureInfo.InvariantCulture, $"\n{shape.Count()} of this shape: {Cell(shape.Key)}\n\n| Place | Class | Loop | Evidence |\n| --- | --- | --- | --- |\n");
                foreach (var (reason, loop) in shape.OrderBy(each => each.Reason.Place, StringComparer.Ordinal))
                    text.Append(CultureInfo.InvariantCulture, $"| `{Cell(reason.Place)}` | {(reason.Class.Length > 0 ? $"`{reason.Class}`" : "")} | {loop} | {Cell(reason.Evidence)} |\n");
            }
        }

        if (never.Count > 0)
            text.Append("\n## Never tried\n\n").AppendJoin('\n', never.Select(key => $"- `{key}`")).Append('\n');
        var person = standing.Where(each => each.Reason.FixAt == FixPlaces.Person && each.Reason.Code != ReasonCodes.ClassExists).OrderBy(each => each.Reason.Place, StringComparer.Ordinal).ToList();
        if (person.Count > 0)
        {
            text.Append("\n## Waits for a person\n\nNothing the factory can change by itself gets these further.\n\n");
            text.AppendJoin('\n', person.Select(each => $"- `{each.Reason.Place}` ({each.Reason.Code}): {Cell(each.Reason.Evidence)}")).Append('\n');
        }

        return text.ToString();
    }

    // What two messages have in common: the message without the names it quotes and the numbers it gives.
    private static string Shape(string evidence)
    {
        var shape = Names().Replace(evidence, "…");
        return shape.Length > 140 ? shape[..140] + "…" : shape;
    }

    private static string Cell(string text) => text.Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    // A name in quotes, an address, a number: what differs between two messages of one kind.
    [GeneratedRegex(@"'[^']*'|""[^""]*""|/[\w#/{}?=.-]+|\b\w+(Page|Dialog|Tests|Tab)\b|\d+")]
    private static partial Regex Names();
}
