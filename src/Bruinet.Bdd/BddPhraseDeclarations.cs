using System.Reflection;
using System.Text.RegularExpressions;
using Bruinet.Core.Settings;

namespace Bruinet.Bdd;

/// <summary>Where a phrase parameter's value comes from in the scenario.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public enum BddParameterSource
{
    /// <summary>A placeholder on the step line.</summary>
    Phrase,

    /// <summary>The table under the step (or a JSON doc string for one record).</summary>
    Table,

    /// <summary>The <c>"""</c> doc string under the step.</summary>
    DocString,
}

/// <summary>A value a step supplies to its phrase's method, with the type it is read as.</summary>
/// <param name="Name">The parameter's name; for <see cref="BddParameterSource.Phrase"/> also the placeholder's.</param>
/// <param name="Type">The parameter type.</param>
/// <param name="Source">Where the value comes from.</param>
/// <param name="Optional">Whether the step may leave it out.</param>
/// <param name="AllowsNull">Whether <c>null</c> reads as null for a reference type.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddPhraseParameter(string Name, Type Type, BddParameterSource Source, bool Optional, bool AllowsNull)
{
    /// <summary>The placeholder as the phrase list shows it: <c>{due: date}</c>.</summary>
    public override string ToString() => $"{{{Name}: {BddValues.Describe(Type)}}}";
}

/// <summary>Why a declared phrase cannot run. It is reported when a step binds to it (AD-016).</summary>
/// <param name="Code">The diagnostic code, <c>UATA010</c>-<c>UATA013</c>.</param>
/// <param name="Message">What is wrong with the declaration.</param>
/// <param name="Declaration">The method that declares it: <c>Type.Method</c>.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddPhraseProblem(string Code, string Message, string Declaration);

/// <summary>A declaration with no phrase text to match: only the validator's phrase list shows it.</summary>
/// <param name="Keyword">The attribute's keyword.</param>
/// <param name="Problem">Why it has no phrase.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddUnmatchableDeclaration(BddEffectiveStepKeyword Keyword, BddPhraseProblem Problem);

internal sealed record BddDeclaredPhrase(
    BddEffectiveStepKeyword Keyword,
    string Phrase,
    IReadOnlyList<BddPhraseParameter> Parameters,
    BddPhraseProblem? Problem)
{
    /// <summary>Whether it is a declared state (<c>[BddState]</c>), which <c>set_state</c> may run.</summary>
    public bool IsState { get; init; }
}

/// <summary>
/// Turns a phrase method into its phrases: attribute text, an empty attribute (the method name),
/// or, for phrase classes, a name that starts with a keyword. A problem stays on its phrase and
/// never stops the catalog (AD-016).
/// </summary>
internal static partial class BddPhraseDeclarations
{
    private const string User = "user";

    public static (IReadOnlyList<BddDeclaredPhrase> Phrases, IReadOnlyList<BddUnmatchableDeclaration> Unmatchable) Of(
        MethodInfo method,
        bool inferWithoutAttribute,
        BddArgumentTypes? types = null)
    {
        ArgumentNullException.ThrowIfNull(method);

        var declaration = $"{method.DeclaringType?.FullName}.{method.Name}";
        var attributes = method.GetCustomAttributes<BddStepAttribute>().ToArray();
        List<BddDeclaredPhrase> phrases = [];
        List<BddUnmatchableDeclaration> unmatchable = [];

        if (attributes.Length == 0)
        {
            if (inferWithoutAttribute && Prefix(Words(method.Name)) is { } prefix)
            {
                Add(prefix, fromAttribute: null, controlVerbOnly: null);
            }

            return (phrases, unmatchable);
        }

        HashSet<BddEffectiveStepKeyword> inferred = [];
        foreach (var attribute in attributes)
        {
            if (attribute.IsInferred && !inferred.Add(attribute.Keyword))
            {
                var first = phrases.FindIndex(p => p.Keyword == attribute.Keyword);
                var duplicate = new BddPhraseProblem("UATA013", $"has two empty [Bdd{attribute.Keyword}] attributes; one says the method name is the phrase.", declaration);
                if (first >= 0)
                {
                    phrases[first] = phrases[first] with { Problem = phrases[first].Problem ?? duplicate };
                }

                continue;
            }

            Add(attribute.Keyword, attribute, BddPhraseMethods.ControlVerbOnlyError(method, attribute));
        }

        return (phrases, unmatchable);

        void Add(BddEffectiveStepKeyword keyword, BddStepAttribute? fromAttribute, string? controlVerbOnly)
        {
            BddPhraseProblem? problem = null;
            var phrase = fromAttribute is { IsInferred: false }
                ? fromAttribute.Phrase
                : Infer(method, fromAttribute is null ? null : keyword, out problem);
            var (parameters, parameterProblem) = Parameters(method, phrase, types);

            problem ??= controlVerbOnly is null ? null : new BddPhraseProblem("UATA013", controlVerbOnly, declaration);
            problem ??= parameterProblem is null ? null : parameterProblem with { Declaration = declaration };

            if (phrase is null)
            {
                unmatchable.Add(new BddUnmatchableDeclaration(keyword, problem ?? new BddPhraseProblem("UATA011", "has a name that says nothing.", declaration)));
                return;
            }

            phrases.Add(new BddDeclaredPhrase(keyword, phrase, parameters, problem) { IsState = fromAttribute is BddStateAttribute });
        }
    }

    /// <summary>The phrase a method name says, or null with the problem.</summary>
    /// <param name="method">The method.</param>
    /// <param name="attributeKeyword">The empty attribute's keyword; null when the name's own prefix decides.</param>
    /// <param name="problem">Why the name does not give a phrase, or why the phrase is broken.</param>
    public static string? Infer(MethodInfo method, BddEffectiveStepKeyword? attributeKeyword, out BddPhraseProblem? problem)
    {
        problem = null;
        var declaration = $"{method.DeclaringType?.FullName}.{method.Name}";
        var words = Words(method.Name);
        var prefix = Prefix(words);
        if (prefix is not null)
        {
            words = words.Skip(1).ToList();
            if (attributeKeyword is { } keyword && keyword != prefix)
            {
                problem = new BddPhraseProblem("UATA013", $"is named {prefix}_..., but its attribute is [Bdd{keyword}].", declaration);
            }
        }

        if (words.Count == 0)
        {
            problem = new BddPhraseProblem("UATA011", "has a name with no words after its keyword.", declaration);
            return null;
        }

        if (method.GetParameters().Any(p => p.Name == "i"))
        {
            problem ??= new BddPhraseProblem("UATA011", "has a parameter named 'i', which a name cannot tell from the word I. Rename it.", declaration);
        }

        List<string> tokens = [];
        foreach (var word in words)
        {
            if (IsCapitals(word))
            {
                if (word == "USER")
                {
                    tokens.Add("{user}");
                    continue;
                }

                var parameter = method.GetParameters().FirstOrDefault(p => p.Name is { } name && name.Equals(word, StringComparison.OrdinalIgnoreCase));
                if (parameter is not null)
                {
                    tokens.Add($"{{{parameter.Name}}}");
                    continue;
                }

                if (TouchingParameters(word, method) is { } touching)
                {
                    problem ??= new BddPhraseProblem("UATA011", $"has {word}, which runs {touching} together. Separate them with _.", declaration);
                }

                tokens.Add(word);
                continue;
            }

            tokens.Add(word == "I" ? word : word.ToLowerInvariant());
        }

        return string.Join(' ', tokens);
    }

    // Which parameters a phrase supplies, and the first thing wrong with them.
    private static (IReadOnlyList<BddPhraseParameter> Parameters, BddPhraseProblem? Problem) Parameters(MethodInfo method, string? phrase, BddArgumentTypes? types)
    {
        var placeholders = phrase is null
            ? []
            : PlaceholderRegex().Matches(phrase).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var nullability = new NullabilityInfoContext();
        List<BddPhraseParameter> parameters = [];
        BddPhraseProblem? problem = null;

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.Name is not { } name || IsInjected(parameter.ParameterType, method.DeclaringType?.Assembly))
            {
                continue;
            }

            var allowsNull = !parameter.ParameterType.IsValueType && nullability.Create(parameter).WriteState == NullabilityState.Nullable;
            if (placeholders.Contains(name))
            {
                parameters.Add(new BddPhraseParameter(name, parameter.ParameterType, BddParameterSource.Phrase, parameter.IsOptional, allowsNull));
                continue;
            }

            if (BddValues.ElementType(parameter.ParameterType) is not null || !BddValues.IsInline(parameter.ParameterType, types))
            {
                parameters.Add(new BddPhraseParameter(name, parameter.ParameterType, BddParameterSource.Table, parameter.IsOptional, allowsNull));
                continue;
            }

            if (parameter.ParameterType == typeof(string) && !parameter.IsOptional)
            {
                parameters.Add(new BddPhraseParameter(name, parameter.ParameterType, BddParameterSource.DocString, parameter.IsOptional, allowsNull));
                continue;
            }

            if (!parameter.IsOptional && phrase is not null)
            {
                problem ??= new BddPhraseProblem("UATA010", $"does not name parameter '{name}' in its phrase '{phrase}'.", string.Empty);
            }
        }

        var blocks = parameters.Where(p => p.Source != BddParameterSource.Phrase).Select(p => p.Name).ToList();
        if (blocks.Count > 1)
        {
            problem ??= new BddPhraseProblem("UATA012", $"takes {string.Join(" and ", blocks.Select(b => $"'{b}'"))} from under the step, but a step has one table or doc string.", string.Empty);
        }

        return (parameters, problem);
    }

    /// <summary>Whether the engine supplies a parameter of this type itself, rather than the step.</summary>
    public static bool IsInjected(Type type, Assembly? phraseAssembly)
        => type == typeof(BddExecutionContext) || type == typeof(BddStepInvocation) || type == typeof(CancellationToken) ||
           type == typeof(BddTable) || IsSettingsType(type, phraseAssembly);

    public static bool IsSettingsType(Type parameterType, Assembly? phraseAssembly)
    {
        if (parameterType == typeof(TestSettings) ||
            parameterType.GetCustomAttribute<TestSettingsRootAttribute>() is not null ||
            parameterType.GetCustomAttribute<TestSettingsSectionAttribute>() is not null)
        {
            return true;
        }

        return phraseAssembly is not null &&
               parameterType.Assembly == phraseAssembly &&
               parameterType.IsClass &&
               parameterType.Name.EndsWith("Settings", StringComparison.Ordinal);
    }

    /// <summary>The words of an identifier: split on <c>_</c>, then on case changes; a run of capitals is one word.</summary>
    public static List<string> Words(string identifier)
    {
        List<string> words = [];
        foreach (var part in identifier.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            var current = 0;
            for (var i = 1; i < part.Length; i++)
            {
                var previous = part[i - 1];
                var character = part[i];
                var nextIsLower = i + 1 < part.Length && char.IsLower(part[i + 1]);
                if (char.IsUpper(character) && (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower)))
                {
                    words.Add(part[current..i]);
                    current = i;
                }
            }

            words.Add(part[current..]);
        }

        return words;
    }

    private static BddEffectiveStepKeyword? Prefix(IReadOnlyList<string> words)
        => words.Count > 1 && Enum.TryParse<BddEffectiveStepKeyword>(words[0], ignoreCase: false, out var keyword) && words[0] == keyword.ToString()
            ? keyword
            : null;

    private static bool IsCapitals(string word)
        => word.Length >= 2 && word.Any(char.IsLetter) && word.All(c => char.IsUpper(c) || char.IsDigit(c));

    private static string? TouchingParameters(string word, MethodInfo method)
    {
        var names = method.GetParameters().Select(p => p.Name).OfType<string>().Append(User).ToList();
        foreach (var first in names)
        {
            foreach (var second in names)
            {
                if ((first + second).Equals(word, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{first.ToUpperInvariant()} and {second.ToUpperInvariant()}";
                }
            }
        }

        return null;
    }

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9_-]*)\}")]
    private static partial Regex PlaceholderRegex();
}
