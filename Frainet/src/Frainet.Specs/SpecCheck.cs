using System.Globalization;
using System.Text.RegularExpressions;

namespace Frainet.Specs;

/// <summary>
/// Checks a spec against the format in <c>docs/spec-format.md</c>, without a model: what is there,
/// in which order, and what a spec never holds.
/// </summary>
public static partial class SpecCheck
{
    /// <summary>The sections a spec may have, in their order.</summary>
    public static readonly string[] Sections =
        ["Intent", "Rules", "Background", "Examples", "Choices", "Not in this journey", "Open questions"];

    /// <summary>The sections every spec has.</summary>
    public static readonly string[] RequiredSections = ["Intent", "Rules", "Examples"];

    /// <summary>The header table's fields.</summary>
    public static readonly string[] HeaderFields = ["Product", "Journey", "Where", "Status"];

    /// <summary>Words that cannot be false, so a spec never uses them.</summary>
    public static readonly string[] VagueWords =
        ["fast", "should", "appropriate", "user-friendly", "etc.", "and so on", "as needed"];

    /// <summary>Checks the spec in <paramref name="path"/>.</summary>
    public static IReadOnlyList<SpecFinding> CheckFile(string path) => Check(File.ReadAllText(path));

    /// <summary>Checks a spec's text; an empty list when it follows the format.</summary>
    public static IReadOnlyList<SpecFinding> Check(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var findings = new List<SpecFinding>();
        var spec = Read(lines);

        CheckTitle(spec, findings);
        CheckHeader(spec, findings);
        CheckSections(spec, findings);
        var rules = CheckRules(spec, findings);
        CheckExamples(spec, rules, findings);
        CheckOpenQuestions(spec, findings);
        CheckWords(lines, findings);

        return findings.OrderBy(finding => finding.Line).ToList();
    }

    /// <summary>The journey id from a spec's title, such as <c>TOD.02</c>; <c>null</c> without one.</summary>
    public static string? JourneyId(string text) => Read(text.ReplaceLineEndings("\n").Split('\n')).JourneyId;

    /// <summary>The ids in a spec's rules table, in their order, such as <c>TOD.02.R1</c>.</summary>
    public static IReadOnlyList<string> RuleIds(string text)
    {
        var spec = Read(text.ReplaceLineEndings("\n").Split('\n'));
        return spec.Section("Rules") is { } rules
            ? TableRows(rules.Body).Skip(1).Select(row => row.Cells[0]).Where(id => id.Length > 0).ToList()
            : [];
    }

    private static void CheckTitle(Spec spec, List<SpecFinding> findings)
    {
        if (spec.JourneyId is null)
        {
            findings.Add(new(1, "title", "The first line is \"# Spec: <journey id> <journey name>\", such as \"# Spec: TOD.02 Create a todo\"."));
        }
    }

    private static void CheckHeader(Spec spec, List<SpecFinding> findings)
    {
        var fields = TableRows(spec.Header)
            .Where(row => row.Cells.Count >= 2)
            .GroupBy(row => row.Cells[0])
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var field in HeaderFields.Where(field => !fields.ContainsKey(field)))
        {
            findings.Add(new(2, "header-field", $"The table under the title has no \"{field}\"."));
        }

        if (fields.TryGetValue("Journey", out var journey) && spec.JourneyId is { } id
            && !journey.Cells[1].StartsWith(id + " ", StringComparison.Ordinal))
        {
            findings.Add(new(journey.Line, "header-journey", $"The Journey field starts with the title's journey id, {id}."));
        }

        if (fields.TryGetValue("Status", out var status) && !IsStatus(status.Cells[1]))
        {
            findings.Add(new(status.Line, "header-status", "The Status is \"draft\" or \"accepted <date>\", such as \"accepted 2026-10-26\"."));
        }
    }

    private static bool IsStatus(string value)
    {
        if (value == "draft")
        {
            return true;
        }

        return value.StartsWith("accepted ", StringComparison.Ordinal)
            && DateOnly.TryParseExact(value["accepted ".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static void CheckSections(Spec spec, List<SpecFinding> findings)
    {
        var last = -1;
        foreach (var section in spec.Sections)
        {
            var index = Array.IndexOf(Sections, section.Name);
            if (index < 0)
            {
                findings.Add(new(section.Line, "section-unknown", $"\"{section.Name}\" is not a section of a spec; they are {string.Join(", ", Sections)}."));
                continue;
            }

            if (index <= last)
            {
                findings.Add(new(section.Line, "section-order", $"\"{section.Name}\" comes before \"{Sections[last]}\", or appears twice."));
            }

            last = Math.Max(last, index);

            if (section.Body.All(line => string.IsNullOrWhiteSpace(line.Text)))
            {
                findings.Add(new(section.Line, "section-empty", $"\"{section.Name}\" has nothing in it; leave the section out instead."));
            }
        }

        foreach (var name in RequiredSections.Where(name => spec.Sections.All(section => section.Name != name)))
        {
            findings.Add(new(0, "section-missing", $"Every spec has a section \"{name}\"."));
        }
    }

    private static Dictionary<int, int> CheckRules(Spec spec, List<SpecFinding> findings)
    {
        var rules = new Dictionary<int, int>();
        if (spec.Section("Rules") is not { } section)
        {
            return rules;
        }

        var rows = TableRows(section.Body).ToList();
        if (rows.Count == 0 || rows[0].Cells is not ["Id", "Rule"])
        {
            findings.Add(new(section.Line, "rules-table", "The rules are a table with the columns Id and Rule."));
            return rules;
        }

        var prefix = (spec.JourneyId ?? "<journey id>") + ".R";
        foreach (var row in rows.Skip(1))
        {
            var id = row.Cells.ElementAtOrDefault(0) ?? string.Empty;
            var rule = row.Cells.ElementAtOrDefault(1) ?? string.Empty;

            if (!id.StartsWith(prefix, StringComparison.Ordinal) || !int.TryParse(id[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                findings.Add(new(row.Line, "rule-id", $"\"{id}\" is not a rule id; they are {prefix}<number>, such as {prefix}1."));
                continue;
            }

            if (!rules.TryAdd(number, row.Line))
            {
                findings.Add(new(row.Line, "rule-id-twice", $"{id} is used twice."));
            }

            if (string.IsNullOrWhiteSpace(rule))
            {
                findings.Add(new(row.Line, "rule-empty", $"{id} says nothing."));
            }
            else if (!rule.EndsWith('.') || SentenceBreak().IsMatch(Quoted().Replace(rule, "x")))
            {
                findings.Add(new(row.Line, "rule-sentence", $"{id} is one sentence, ending in a full stop."));
            }
        }

        return rules;
    }

    private static void CheckExamples(Spec spec, Dictionary<int, int> rules, List<SpecFinding> findings)
    {
        if (spec.Section("Examples") is not { } section)
        {
            return;
        }

        // The Background's steps go before every example, a chapter's before each of its own (runnable specs).
        var background = spec.Section("Background") is { } given ? GherkinSteps(given.Body) : [];
        List<Section> examples = [];
        foreach (var sub in Subsections(section, "### "))
        {
            if (ChapterHeading().IsMatch(sub.Name))
            {
                var chapter = Subsections(sub, "#### ").ToList();
                var preamble = sub.Body.TakeWhile(line => !line.Text.StartsWith("#### ", StringComparison.Ordinal)).ToList();
                var steps = GherkinSteps(preamble);
                if (steps.Count == 0 || steps.Any(step => step.Text.TrimStart().StartsWith("Then ", StringComparison.Ordinal)))
                {
                    findings.Add(new(sub.Line, "chapter-steps", $"\"### {sub.Name}\" has a ```gherkin block of Given and When steps, no Then."));
                }

                foreach (var example in chapter)
                {
                    CheckOne(example, "####", [.. background, .. steps]);
                }

                examples.AddRange(chapter);
                continue;
            }

            CheckOne(sub, "###", background);
            examples.Add(sub);
        }

        void CheckOne(Section example, string level, List<SpecLine> prefix)
        {
            var match = ExampleHeading().Match(example.Name);
            if (!match.Success)
            {
                findings.Add(new(example.Line, "example-heading", $"\"{level} {example.Name}\" is not an example's heading; they are \"### R<number> <short name>\", or \"#### R<number> <short name>\" in a \"### Chapter: <name>\"."));
                return;
            }

            var number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!rules.ContainsKey(number))
            {
                findings.Add(new(example.Line, "example-unknown", $"R{number} is not one of the rules."));
            }

            CheckExample(example, number, findings, prefix);
        }

        var shown = examples
            .Select(example => ExampleHeading().Match(example.Name))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToHashSet();

        foreach (var (number, line) in rules.Where(rule => !shown.Contains(rule.Key)))
        {
            findings.Add(new(line, "example-missing", $"R{number} has no example under Examples."));
        }
    }

    private static void CheckExample(Section example, int number, List<SpecFinding> findings, List<SpecLine> prefix)
    {
        var fence = example.Body.FindIndex(line => line.Text.StartsWith("```", StringComparison.Ordinal));
        var why = example.Body.Take(fence < 0 ? example.Body.Count : fence).Any(line => !string.IsNullOrWhiteSpace(line.Text));

        if (!why)
        {
            findings.Add(new(example.Line, "example-why", $"R{number}'s example starts with a short paragraph on why the rule is there."));
        }

        if (fence < 0 || example.Body[fence].Text.Trim() != "```gherkin")
        {
            findings.Add(new(example.Line, "example-gherkin", $"R{number}'s example has a ```gherkin block."));
            return;
        }

        var steps = example.Body.Skip(fence + 1).TakeWhile(line => !line.Text.StartsWith("```", StringComparison.Ordinal)).ToList();
        foreach (var scenario in SplitOnBlankLines(steps))
        {
            CheckScenario([.. prefix, .. scenario], number, findings);
        }
    }

    private static void CheckScenario(List<SpecLine> steps, int number, List<SpecFinding> findings)
    {
        string[] order = ["Given", "When", "Then"];
        var at = -1;
        var seen = new HashSet<string>();

        foreach (var step in steps)
        {
            var keyword = step.Text.Trim().Split(' ', 2)[0];
            if (keyword is "And" or "But")
            {
                if (at < 0)
                {
                    findings.Add(new(step.Number, "gherkin-step", $"R{number}'s example starts with Given or When, not {keyword}."));
                }

                continue;
            }

            var index = Array.IndexOf(order, keyword);
            if (index < 0)
            {
                findings.Add(new(step.Number, "gherkin-step", $"Each line of R{number}'s example starts with Given, When, Then, And or But."));
                continue;
            }

            if (index < at)
            {
                findings.Add(new(step.Number, "gherkin-order", $"In R{number}'s example, {keyword} comes after {order[at]}; Given, When and Then go in that order."));
            }

            at = Math.Max(at, index);
            seen.Add(keyword);
        }

        foreach (var keyword in new[] { "When", "Then" }.Where(keyword => !seen.Contains(keyword)))
        {
            findings.Add(new(steps[0].Number, "gherkin-incomplete", $"R{number}'s example has no {keyword}."));
        }
    }

    private static void CheckOpenQuestions(Spec spec, List<SpecFinding> findings)
    {
        if (spec.Section("Open questions") is not { } section)
        {
            return;
        }

        foreach (var question in Bullets(section.Body).Where(bullet => !bullet.Text.Contains("Proposal:", StringComparison.Ordinal)))
        {
            findings.Add(new(question.Number, "question-proposal", "Each open question has \"Proposal:\" with an answer and why."));
        }

        var status = TableRows(spec.Header).FirstOrDefault(row => row.Cells is ["Status", ..]);
        if (status is not null && status.Cells[1].StartsWith("accepted", StringComparison.Ordinal) && Bullets(section.Body).Any())
        {
            findings.Add(new(status.Line, "accepted-open", "A spec is accepted only with no open questions left."));
        }
    }

    private static void CheckWords(string[] lines, List<SpecFinding> findings)
    {
        var inFence = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var text = lines[i];
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            foreach (var word in VagueWords.Where(word => VagueWord(word).IsMatch(text)))
            {
                findings.Add(new(i + 1, "vague-word", $"\"{word}\" cannot be false; say exactly what holds."));
            }

            if (!inFence && CodeSpan().IsMatch(text))
            {
                findings.Add(new(i + 1, "code-word", "A spec has no code in `backticks`; say what a person sees."));
            }

            foreach (Match match in CodeName().Matches(text))
            {
                findings.Add(new(i + 1, "code-word", $"\"{match.Value}\" looks like a name from the code; say what a person sees."));
            }
        }
    }

    private static Regex VagueWord(string word) =>
        new($@"(?<![\w-]){Regex.Escape(word)}(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Spec Read(string[] lines)
    {
        var title = TitleLine().Match(lines.FirstOrDefault() ?? string.Empty);
        var header = new List<SpecLine>();
        var sections = new List<Section>();
        var inFence = false;

        for (var i = 1; i < lines.Length; i++)
        {
            var line = new SpecLine(i + 1, lines[i]);
            if (line.Text.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }

            if (!inFence && line.Text.StartsWith("## ", StringComparison.Ordinal))
            {
                sections.Add(new(line.Number, line.Text[3..].Trim(), []));
            }
            else if (sections.Count == 0)
            {
                header.Add(line);
            }
            else
            {
                sections[^1].Body.Add(line);
            }
        }

        return new(title.Success ? title.Groups[1].Value : null, header, sections);
    }

    // The steps of a body's first ```gherkin block.
    private static List<SpecLine> GherkinSteps(List<SpecLine> body)
    {
        var fence = body.FindIndex(line => line.Text.Trim() == "```gherkin");
        return fence < 0 ? [] : [.. body.Skip(fence + 1).TakeWhile(line => !line.Text.StartsWith("```", StringComparison.Ordinal)).Where(line => !string.IsNullOrWhiteSpace(line.Text))];
    }

    private static IEnumerable<Section> Subsections(Section section, string level)
    {
        Section? current = null;
        foreach (var line in section.Body)
        {
            if (line.Text.StartsWith(level, StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    yield return current;
                }

                current = new(line.Number, line.Text[level.Length..].Trim(), []);
            }
            else
            {
                current?.Body.Add(line);
            }
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    private static IEnumerable<Row> TableRows(IEnumerable<SpecLine> lines) => lines
        .Where(line => line.Text.TrimStart().StartsWith('|'))
        .Select(line => new Row(line.Number, line.Text.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToList()))
        .Where(row => !row.Cells.All(cell => cell.Length > 0 && cell.All(c => c is '-' or ':')));

    private static IEnumerable<SpecLine> Bullets(IEnumerable<SpecLine> lines) =>
        lines.Where(line => line.Text.StartsWith("- ", StringComparison.Ordinal));

    private static IEnumerable<List<SpecLine>> SplitOnBlankLines(List<SpecLine> lines)
    {
        var current = new List<SpecLine>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                if (current.Count > 0)
                {
                    yield return current;
                    current = [];
                }
            }
            else
            {
                current.Add(line);
            }
        }

        if (current.Count > 0)
        {
            yield return current;
        }
    }

    // A journey id may carry a product before it (B7.HRS.01): upper-case parts, then the journey's number.
    [GeneratedRegex(@"^# Spec: ((?:[A-Z][A-Z0-9]*\.)+\d+) \S")]
    private static partial Regex TitleLine();

    [GeneratedRegex(@"^R(\d+) \S")]
    private static partial Regex ExampleHeading();

    [GeneratedRegex(@"^Chapter:\s*\S")]
    private static partial Regex ChapterHeading();

    // What the screen says ("Amount (excl. VAT)", "Save and create another. Now") is not the rule's own sentence.
    [GeneratedRegex(@"\([^()]*\)|""[^""]*""")]
    private static partial Regex Quoted();

    // A full stop after a number is a numbering ("00. Intern", a status), not a sentence's end.
    [GeneratedRegex(@"(?<![0-9])[.!?] +[A-Z]")]
    private static partial Regex SentenceBreak();

    [GeneratedRegex(@"`[^`]+`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"\b[A-Z][a-z]+[A-Z][A-Za-z]*\b|\b\w+\(\)|\b\w+_\w+\b|\b[\w-]+\.(cs|json|xaml|razor|sql|csproj)\b")]
    private static partial Regex CodeName();

    private sealed record SpecLine(int Number, string Text);

    private sealed record Row(int Line, List<string> Cells);

    private sealed record Section(int Line, string Name, List<SpecLine> Body);

    private sealed record Spec(string? JourneyId, List<SpecLine> Header, List<Section> Sections)
    {
        public Section? Section(string name) => Sections.FirstOrDefault(section => section.Name == name);
    }
}
