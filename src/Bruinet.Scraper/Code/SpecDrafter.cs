using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Code;

/// <summary>What a drafted spec says of one page: how it is reached and what it offers.</summary>
/// <param name="Place">The page's key in the list of places.</param>
/// <param name="Tab">The tab that shows it.</param>
/// <param name="PageName">The page's name in a step (<c>Project View Complaints</c>).</param>
/// <param name="Buttons">The buttons it shows, by their labels.</param>
public sealed record SpecPage(string Place, string Tab, string PageName, IReadOnlyList<string> Buttons);

/// <summary>A drafted spec.</summary>
/// <param name="Text">The spec file.</param>
/// <param name="Rules">Its rules, each as its id and its sentence.</param>
/// <param name="LeftOut">What got no rule, each with why: nothing new to say, or its example failed on the app.</param>
public sealed record SpecDraft(string Text, IReadOnlyList<(string Id, string Rule)> Rules, IReadOnlyList<string> LeftOut);

/// <summary>
/// Drafts a spec in Bruinet's spec format from what the drafted pages of one address show: a rule and an example per
/// page, in the suite's own phrases (the <c>Spec …</c> blocks of its draft templates). A step an existing spec has
/// already is left out, and so is a rule whose example failed on the app. The draft is a start: a person writes the
/// intent and says what each rule is for.
/// </summary>
public static partial class SpecDrafter
{
    /// <summary>The template of a spec's head: its table of fields and its intent, with <c>{Id}</c> and <c>{Title}</c>.</summary>
    public const string Head = "Spec head";

    /// <summary>The start of the heading of the template that opens a chapter for an address (<c>## Spec opening /project/view</c>): a title line, then prose, then the steps that reach the first page.</summary>
    public const string Opening = "Spec opening ";

    /// <summary>The template of the steps that reach a page from the chapter's start, with <c>{Tab}</c> and <c>{PageName}</c>.</summary>
    public const string PageSteps = "Spec page steps";

    /// <summary>The template of the step that checks a button, with <c>{Label}</c>.</summary>
    public const string ButtonStep = "Spec button step";

    /// <summary>The template of a page's rule, with <c>{Tab}</c> and <c>{Buttons}</c>.</summary>
    public const string PageRule = "Spec page rule";

    /// <summary>True when the templates say how a spec is written for an address.</summary>
    public static bool CanDraft(DraftTemplates templates, string address)
    {
        ArgumentNullException.ThrowIfNull(templates);
        return new[] { Head, Opening + address, PageSteps, ButtonStep, PageRule }.All(templates.Blocks.ContainsKey);
    }

    /// <summary>Drafts the spec.</summary>
    /// <param name="id">The spec's id (<c>B7.PRJ.04</c>).</param>
    /// <param name="title">Its title.</param>
    /// <param name="address">The address its pages share.</param>
    /// <param name="pages">The pages, in the order the rules get.</param>
    /// <param name="templates">The suite's templates.</param>
    /// <param name="existing">The steps other specs have already, without their first word: such a step is left out.</param>
    /// <param name="failed">The places whose example failed on the app, each with what the test said: they get no rule.</param>
    public static SpecDraft Draft(string id, string title, string address, IReadOnlyList<SpecPage> pages, DraftTemplates templates, IReadOnlySet<string> existing, IReadOnlyDictionary<string, string>? failed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(existing);
        var rules = new List<(string Id, string Rule)>();
        var leftOut = new List<string>();
        var examples = new StringBuilder();
        foreach (var page in pages)
        {
            if (failed is not null && failed.TryGetValue(page.Place, out var said))
            {
                leftOut.Add($"{page.Place}: its example failed on the app: {said}");
                continue;
            }

            var checks = page.Buttons.Select(button => (Label: button, Step: Fill(templates.Block(ButtonStep), page).Replace("{Label}", button, StringComparison.Ordinal).Trim()))
                .Where(check => !existing.Contains(WithoutKeyword(check.Step))).ToList();
            if (checks.Count == 0)
            {
                leftOut.Add($"{page.Place}: nothing to say that another spec does not say already");
                continue;
            }

            var number = rules.Count + 1;
            var ruleId = $"{id}.R{number.ToString(CultureInfo.InvariantCulture)}";
            var rule = Fill(templates.Block(PageRule), page).Replace("{Buttons}", Sentence(checks.Select(check => check.Label)), StringComparison.Ordinal).Trim();
            rules.Add((ruleId, rule));
            examples.Append(CultureInfo.InvariantCulture, $"#### R{number} {page.Tab}\n\n{rule}\n\n```gherkin\n{Fill(templates.Block(PageSteps), page).Trim()}\n{string.Join('\n', checks.Select(check => check.Step))}\n```\n\n");
        }

        var opening = templates.Block(Opening + address).Trim().Split('\n');
        var steps = opening.Skip(1).Where(IsStep).ToList();
        var prose = opening.Skip(1).Where(line => !IsStep(line) && line.Trim().Length > 0).ToList();
        var text = new StringBuilder();
        text.Append(templates.Block(Head).Replace("{Id}", id, StringComparison.Ordinal).Replace("{Title}", title, StringComparison.Ordinal).TrimEnd()).Append("\n\n");
        text.Append("| Id | Rule |\n| --- | --- |\n");
        foreach (var (ruleId, rule) in rules)
            text.Append(CultureInfo.InvariantCulture, $"| {ruleId} | {rule.Replace("|", "/", StringComparison.Ordinal)} |\n");
        text.Append(CultureInfo.InvariantCulture, $"\n## Examples\n\n### Chapter: {opening[0].Trim()}\n\n{string.Join('\n', prose)}\n\n```gherkin\n{string.Join('\n', steps.Select(step => step.Trim()))}\n```\n\n");
        text.Append(examples);
        return new SpecDraft(text.ToString().TrimEnd() + "\n", rules, leftOut);
    }

    /// <summary>The steps a spec file has, each without its first word (Given, When, Then, And, But): what another spec need not say again.</summary>
    public static IEnumerable<string> StepsOf(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(IsStep).Select(WithoutKeyword);
    }

    /// <summary>A class's name as a page is named in a step: <c>ProjectViewComplaintsPage</c> is <c>Project View Complaints</c>.</summary>
    public static string PageName(string className)
    {
        ArgumentNullException.ThrowIfNull(className);
        var bare = className.EndsWith("Page", StringComparison.Ordinal) && className.Length > 4 ? className[..^4] : className;
        return Words().Replace(bare, "$1 $2");
    }

    private static string Fill(string template, SpecPage page) => template
        .Replace("{Tab}", page.Tab, StringComparison.Ordinal)
        .Replace("{PageName}", page.PageName, StringComparison.Ordinal);

    private static bool IsStep(string line) => StepStart().IsMatch(line);

    private static string WithoutKeyword(string step) => StepStart().Replace(step.Trim(), "").Trim();

    // "A", "A and B", "A, B and C", each in quotes.
    private static string Sentence(IEnumerable<string> labels)
    {
        var quoted = labels.Select(label => $"\"{label}\"").ToList();
        return quoted.Count <= 1 ? string.Concat(quoted) : string.Join(", ", quoted.Take(quoted.Count - 1)) + " and " + quoted[^1];
    }

    [GeneratedRegex(@"^\s*(Given|When|Then|And|But)\s+")]
    private static partial Regex StepStart();

    [GeneratedRegex(@"([a-z0-9])([A-Z])")]
    private static partial Regex Words();
}
