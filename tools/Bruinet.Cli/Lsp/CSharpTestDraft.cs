using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>A phrase a suite has, as the draft reads it: its keyword, its words and the namespace a file must use for it.</summary>
/// <param name="Keyword">Given, When or Then.</param>
/// <param name="Phrase">The phrase, with {placeholders}.</param>
/// <param name="Namespace">The step namespace; empty for a phrase every file sees.</param>
public sealed record DraftPhrase(string Keyword, string Phrase, string Namespace);

/// <summary>A statement of the C# test no phrase stands for: a step the suite lacks.</summary>
/// <param name="Line">The draft's line that marks it.</param>
/// <param name="Keyword">The keyword the step would take.</param>
/// <param name="Statement">The C# statement, on one line.</param>
public sealed record DraftGap(int Line, string Keyword, string Statement);

/// <summary>A drafted feature file: its text, the namespaces it uses, and what it lacks.</summary>
/// <param name="Text">The feature file.</param>
/// <param name="Module">The test's module; null when it names none.</param>
/// <param name="Name">A file name for it, without the extension.</param>
/// <param name="Using">The step namespaces the steps come from.</param>
/// <param name="Gaps">The statements no phrase stands for, each a commented line in the draft.</param>
/// <param name="Skipped">Cleanup in finally blocks, which becomes the undo of the Given that changed it.</param>
public sealed record FeatureDraft(string Text, string? Module, string Name, IReadOnlyList<string> Using, IReadOnlyList<DraftGap> Gaps, IReadOnlyList<string> Skipped);

/// <summary>
/// Drafts a feature file from a C# UI test: each statement of the test becomes the suite phrase whose words
/// match it best, or a commented gap when none does. A draft only starts a conversion; it never invents a
/// phrase, so it binds as it is, and its gaps are the steps to write.
/// </summary>
public static partial class CSharpTestDraft
{
    // Words that say nothing about what a step does.
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "i", "is", "it", "its", "to", "of", "and", "or", "in", "on", "at", "for", "with", "by", "be",
        "var", "new", "true", "false", "null", "this", "await", "async", "task", "assert", "equal", "contains",
        "fixture", "context", "timeout", "ms", "wait", "waits", "ready", "loaded", "exists", "visible", "get", "set",
        "item", "items", "page", "control", "controls", "label", "button", "text", "value", "values", "mock", "backend",
        "host", "manager", "scenario", "system", "string", "is", "not",
    };

    /// <summary>Drafts <paramref name="method"/> (the first test method when null) of the C# file <paramref name="source"/>.</summary>
    /// <param name="source">The C# file's text.</param>
    /// <param name="method">The test method; the first one with a Fact or Theory attribute when null.</param>
    /// <param name="phrases">The suite's phrases without placeholders.</param>
    public static FeatureDraft Draft(string source, string? method, IReadOnlyList<DraftPhrase> phrases)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(phrases);
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var tests = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(IsTest).ToList();
        var test = method is null
            ? tests.FirstOrDefault()
            : root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(candidate => candidate.Identifier.Text == method);
        if (test is null)
        {
            throw new ArgumentException(method is null
                ? "The file has no test method."
                : $"The file has no method '{method}'. Its tests are {string.Join(", ", tests.Select(candidate => candidate.Identifier.Text))}.");
        }

        var owner = test.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var attributes = test.AttributeLists.Concat(owner?.AttributeLists ?? default).SelectMany(list => list.Attributes).ToList();
        var module = Traits(attributes, "Module").FirstOrDefault();
        var journeys = Traits(attributes, "Journey").SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToList();

        var candidates = Candidates(phrases, module);
        var steps = new List<(string Keyword, string Line, DraftPhrase? Phrase, string? Gap)>();
        var skipped = new List<string>();

        // The module's ready step starts the scenario, as the ready app the test's setup reaches.
        var ready = module is null ? null : candidates.FirstOrDefault(phrase => phrase.Keyword == "Given"
            && phrase.Phrase.EndsWith($"ready for {module}", StringComparison.OrdinalIgnoreCase));
        if (ready is not null)
        {
            steps.Add(("Given", ready.Phrase, ready, null));
        }

        foreach (var statement in Statements(test))
        {
            var flat = OneLine(statement);
            if (statement.Ancestors().Any(node => node is FinallyClauseSyntax))
            {
                skipped.Add(flat);
                continue;
            }

            var keyword = flat.StartsWith("Assert", StringComparison.Ordinal) || flat.Contains(".Assert", StringComparison.Ordinal) ? "Then" : "When";
            var words = Words(statement);
            if (ready is not null && OpensModule(words, module!))
            {
                continue;
            }

            var best = Best(words, candidates.Where(phrase => phrase != ready));
            if (best is not null)
            {
                if (steps.Count == 0 || steps[^1].Phrase != best)
                {
                    steps.Add((best.Keyword, best.Phrase, best, null));
                }
            }
            else
            {
                steps.Add((keyword, flat, null, flat));
            }
        }

        var used = steps.Where(step => step.Phrase is { Namespace.Length: > 0 }).Select(step => step.Phrase!.Namespace).Distinct().ToList();
        var (text, gaps) = Render(module, journeys, used, $"{owner?.Identifier.Text}.{test.Identifier.Text}", Words(test.Identifier.Text), steps);
        return new FeatureDraft(text, module, Kebab(test.Identifier.Text), used, gaps, skipped);
    }

    private static (string Text, List<DraftGap> Gaps) Render(
        string? module, List<string> journeys, List<string> used, string source, string title, List<(string Keyword, string Line, DraftPhrase? Phrase, string? Gap)> steps)
    {
        var journey = journeys.Count > 0 ? journeys[0] : null;
        List<string> lines = [$"# Feature: {(module is null ? string.Empty : module + " ")}{title}", string.Empty, "## Metadata", string.Empty, "| Field | Value |", "| --- | --- |"];
        if (module is not null)
        {
            lines.Add($"| Area | {module} |");
        }

        if (journey is not null)
        {
            lines.Add($"| Journey | {string.Join(", ", journeys)} |");
        }

        lines.AddRange([$"| Using | {string.Join(", ", used)} |", "| Mode | Automated |", $"| Evidence | Drafted from {source}; not run yet |", string.Empty]);
        if (journey is not null)
        {
            lines.Add($"@{journey}");
        }

        lines.AddRange([$"## Scenario: {(journey is null ? string.Empty : journey + " ")}{title}", string.Empty, "```gherkin"]);
        var gaps = new List<DraftGap>();
        string? previous = null;
        foreach (var step in steps)
        {
            if (step.Gap is not null)
            {
                gaps.Add(new DraftGap(lines.Count + 1, step.Keyword, step.Gap));
                lines.Add($"# Gap ({step.Keyword}): {step.Gap}");
            }
            else
            {
                lines.Add($"{(step.Keyword == previous ? "And" : step.Keyword)} {step.Line}");
                previous = step.Keyword;
            }
        }

        lines.AddRange(["```", string.Empty]);
        return (string.Join('\n', lines), gaps);
    }

    /// <summary>
    /// The phrases that differ only in one value, such as a name or a number, each group with the phrase
    /// that could stand for all of them: one step with a placeholder rather than one step per value.
    /// </summary>
    /// <param name="phrases">The suite's phrases.</param>
    public static IReadOnlyList<SimilarSteps> Similar(IEnumerable<DraftPhrase> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        var groups = new Dictionary<string, List<DraftPhrase>>(StringComparer.Ordinal);
        foreach (var phrase in phrases.Where(phrase => !phrase.Phrase.Contains('{', StringComparison.Ordinal)).DistinctBy(phrase => (phrase.Keyword, phrase.Phrase)))
        {
            var words = phrase.Phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 1; index < words.Length; index++)
            {
                if (!IsValue(words[index]))
                {
                    continue;
                }

                var key = $"{phrase.Keyword}|{string.Join(' ', words.Take(index))} {{value}} {string.Join(' ', words.Skip(index + 1))}".TrimEnd();
                if (!groups.TryGetValue(key, out var members))
                {
                    groups[key] = members = [];
                }

                members.Add(phrase);
            }
        }

        return [.. groups
            .Where(group => group.Value.Count > 1)
            .Select(group => new SimilarSteps(group.Key[(group.Key.IndexOf('|', StringComparison.Ordinal) + 1)..], [.. group.Value.Select(phrase => phrase.Phrase)]))
            .OrderByDescending(similar => similar.Phrases.Count)
            .ThenBy(similar => similar.Placeholder, StringComparer.Ordinal)];
    }

    private static bool IsTest(MethodDeclarationSyntax method)
        => method.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => Name(attribute) is var name && (name.EndsWith("Fact", StringComparison.Ordinal) || name.EndsWith("Theory", StringComparison.Ordinal)));

    private static string Name(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        name = name[(name.LastIndexOf('.') + 1)..];
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^"Attribute".Length] : name;
    }

    // [Trait("Module", "X")] or [Module("X")].
    private static IEnumerable<string> Traits(IEnumerable<AttributeSyntax> attributes, string key)
    {
        foreach (var attribute in attributes)
        {
            var arguments = attribute.ArgumentList?.Arguments.Select(argument => argument.Expression).OfType<LiteralExpressionSyntax>().Select(literal => literal.Token.ValueText).ToList() ?? [];
            var name = Name(attribute);
            if (name == "Trait" && arguments.Count == 2 && arguments[0] == key)
            {
                yield return arguments[1];
            }
            else if (name == key && arguments.Count == 1)
            {
                yield return arguments[0];
            }
        }
    }

    // The module's own namespaces when it has some, otherwise every namespaced phrase.
    private static List<DraftPhrase> Candidates(IReadOnlyList<DraftPhrase> phrases, string? module)
    {
        var exact = phrases.Where(phrase => !phrase.Phrase.Contains('{', StringComparison.Ordinal) && phrase.Namespace.Length > 0).ToList();
        var own = module is null ? [] : exact.Where(phrase => phrase.Namespace.Contains(module, StringComparison.OrdinalIgnoreCase)).ToList();
        return own.Count > 0 ? own : exact;
    }

    // The test's leaf statements in order, with the body of an expression lambda (setup: () => Open()) as one.
    private static IEnumerable<SyntaxNode> Statements(MethodDeclarationSyntax test)
    {
        var body = (SyntaxNode?)test.Body ?? test.ExpressionBody;
        if (body is null)
        {
            return [];
        }

        var statements = body.DescendantNodes()
            .Where(node => node is ExpressionStatementSyntax or LocalDeclarationStatementSyntax)
            .Where(node => !node.DescendantNodes().Any(inner => inner is ExpressionStatementSyntax or LocalDeclarationStatementSyntax));
        var lambdas = body.DescendantNodes().OfType<LambdaExpressionSyntax>()
            .Where(lambda => lambda.ExpressionBody is not null)
            .Select(lambda => (SyntaxNode)lambda.ExpressionBody!);
        return statements.Concat(lambdas).OrderBy(node => node.SpanStart);
    }

    // OpenMaterials(scenario): the ready step already opens the module.
    private static bool OpensModule(HashSet<string> words, string module)
        => words.Contains("open") && words.Contains(Stem(module.ToLowerInvariant())) && words.Count <= 3;

    private static DraftPhrase? Best(HashSet<string> words, IEnumerable<DraftPhrase> candidates)
    {
        DraftPhrase? best = null;
        var bestScore = 0d;
        foreach (var phrase in candidates)
        {
            var own = Words(phrase.Phrase).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => !Noise.Contains(word)).Select(Stem).Distinct().ToList();
            if (own.Count == 0)
            {
                continue;
            }

            var shared = own.Count(words.Contains);
            var score = (double)shared / own.Count;
            if (shared >= 2 && score >= 0.5 && score > bestScore)
            {
                best = phrase;
                bestScore = score;
            }
        }

        return best;
    }

    // The words a statement says: its names split at capitals, its strings and the comments before it.
    private static HashSet<string> Words(SyntaxNode statement)
    {
        var parts = new List<string>();
        foreach (var token in statement.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken) && !token.Text.EndsWith("Ms", StringComparison.Ordinal))
            {
                parts.Add(Words(token.Text));
            }
            else if (token.IsKind(SyntaxKind.StringLiteralToken))
            {
                parts.Add(token.ValueText);
            }
        }

        parts.AddRange(statement.GetLeadingTrivia().Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)).Select(trivia => trivia.ToString().TrimStart('/')));
        return [.. string.Join(' ', parts).Split([' ', '.', ',', '(', ')', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.ToLowerInvariant())
            .Where(word => !Noise.Contains(word))
            .Select(Stem)];
    }

    // "OpenMaterialsQuickAction" -> "open materials quick action"; "Refresh_Requests" -> "refresh requests".
    private static string Words(string identifier)
        => Whitespace().Replace(CapitalStart().Replace(identifier.Replace('_', ' '), " $1"), " ").Trim().ToLowerInvariant();

    private static string Kebab(string identifier) => Words(identifier).Replace(' ', '-');

    private static string Stem(string word) => word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;

    private static string OneLine(SyntaxNode statement) => Whitespace().Replace(statement.ToString(), " ").Trim();

    // A value a phrase holds: a number, a quoted text, or a capitalized word inside the phrase.
    private static bool IsValue(string word)
        => word.Length > 0 && (char.IsDigit(word[0]) || word[0] is '\'' or '"' || char.IsUpper(word[0]));

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])")]
    private static partial Regex CapitalStart();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>Phrases that differ only in one value.</summary>
/// <param name="Placeholder">The phrase that could stand for them, with {value} where they differ.</param>
/// <param name="Phrases">The phrases.</param>
public sealed record SimilarSteps(string Placeholder, IReadOnlyList<string> Phrases);
