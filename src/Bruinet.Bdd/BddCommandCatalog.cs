using System.Text.RegularExpressions;

namespace Bruinet.Bdd;

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddCommandCatalog
{
    private readonly List<BddCommandPattern> _patterns = [];

    // The patterns by keyword and their leading whole words (BddCommandPattern.IndexKey): a step tries only those its own
    // first one and two words name, and those that begin with a parameter, not every pattern of its keyword (next-plan
    // item 7). Each list in registration order, by the pattern's place in _patterns.
    private readonly Dictionary<(BddEffectiveStepKeyword Keyword, string Words), List<int>> _index = [];

    private readonly List<BddUnmatchableDeclaration> _unmatchable = [];
    private readonly List<BddHook> _hooks = [];

    public IReadOnlyList<BddCommandPattern> Patterns => _patterns;

    /// <summary>The hooks of the step-definition classes.</summary>
    public IReadOnlyList<BddHook> Hooks => _hooks;

    /// <summary>Adds a hook.</summary>
    /// <param name="hook">The hook.</param>
    public void AddHook(BddHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _hooks.Add(hook);
    }

    /// <summary>Declarations that are broken and have no phrase text to match (AD-016).</summary>
    public IReadOnlyList<BddUnmatchableDeclaration> Unmatchable => _unmatchable;

    /// <summary>The app's own argument types, which step values are read with.</summary>
    public BddArgumentTypes ArgumentTypes { get; set; } = BddArgumentTypes.Empty;

    /// <summary>
    /// What a head adds to binding beyond namespaces: Bruinet.Uat's pages, which a step's page
    /// scope follows. Null for a catalog without one: then steps bind without page scope.
    /// </summary>
    public IStepScopeModel? Scope { get; set; }

    /// <summary>
    /// Whether this is the generic catalog of built-in phrases only (Bruinet.Uat's <c>UatSpecCommandCatalog</c>),
    /// which knows no app's phrases: a file's <c>Using</c> is not checked against it.
    /// </summary>
    public bool IsGeneric { get; set; }

    public BddCommandPattern Register(
        BddEffectiveStepKeyword keyword,
        string phrase,
        string? commandId = null,
        bool requiresTable = false,
        bool allowsTable = true,
        BddCommandHandler? handler = null,
        IReadOnlyList<BddPhraseParameter>? parameters = null,
        BddPhraseProblem? problem = null,
        bool namesPage = false,
        string? page = null,
        string? pageAfter = null,
        BddControlVerb? controlVerb = null,
        string? phraseNamespace = null,
        bool isState = false)
    {
        var pattern = new BddCommandPattern(keyword, phrase, commandId, requiresTable, allowsTable, handler, parameters, problem)
        {
            IsState = isState,
            NamesPage = namesPage,
            Page = page,
            PageAfter = pageAfter,
            ControlVerb = controlVerb,
            Namespace = BddNamespaces.Normalize(phraseNamespace),
        };
        var key = (keyword, pattern.IndexKey);
        if (!_index.TryGetValue(key, out var bucket))
        {
            _index[key] = bucket = [];
        }

        bucket.Add(_patterns.Count);
        _patterns.Add(pattern);
        return pattern;
    }

    /// <summary>Records a broken declaration that has no phrase to match.</summary>
    public void AddUnmatchable(BddUnmatchableDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        _unmatchable.Add(declaration);
    }

    public IReadOnlyList<BddCommandMatch> Match(BddStep step) => Select(Candidates(step));

    /// <summary>
    /// Every pattern the step's text fits, in registration order, before <see cref="Select"/> picks among them. Only the
    /// patterns its first words could fit are tried: the same matches as trying them all.
    /// </summary>
    public IReadOnlyList<BddCommandMatch> Candidates(BddStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        var (one, two) = BddCommandPattern.LeadingWords(step.Text);
        var tried = new List<int>();
        foreach (var words in (ReadOnlySpan<string?>)[string.Empty, one, two])
        {
            if (words is not null && _index.TryGetValue((step.EffectiveKeyword, words), out var bucket))
            {
                tried.AddRange(bucket);
            }
        }

        tried.Sort();
        return tried
            .Select(at => _patterns[at].TryMatch(step))
            .OfType<BddCommandMatch>()
            .ToArray();
    }

    /// <summary>
    /// The matches a step binds to: exact phrases before parameterised ones, then the most
    /// specific. One rule for C# phrases and callable phrases alike, so neither shadows the other.
    /// </summary>
    public static IReadOnlyList<BddCommandMatch> Select(IReadOnlyList<BddCommandMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var exactMatches = matches.Where(x => x.Pattern.IsExact).ToArray();
        if (exactMatches.Length > 0)
        {
            return exactMatches;
        }

        if (matches.Count <= 1)
        {
            return matches;
        }

        var highestSpecificity = matches.Max(x => x.Pattern.Specificity);
        return matches.Where(x => x.Pattern.Specificity == highestSpecificity).ToArray();
    }
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed class BddCommandPattern
{
    private readonly Regex _regex;
    private readonly IReadOnlyList<string> _parameterNames;

    public BddCommandPattern(
        BddEffectiveStepKeyword keyword,
        string phrase,
        string? commandId = null,
        bool requiresTable = false,
        bool allowsTable = true,
        BddCommandHandler? handler = null,
        IReadOnlyList<BddPhraseParameter>? parameters = null,
        BddPhraseProblem? problem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phrase);

        Keyword = keyword;
        Phrase = phrase.Trim();
        CommandId = string.IsNullOrWhiteSpace(commandId) ? Phrase : commandId.Trim();
        RequiresTable = requiresTable;
        AllowsTable = allowsTable;
        Handler = handler;
        Parameters = parameters ?? [];
        Problem = problem;

        (_regex, _parameterNames) = Compile(Phrase);
        IsExact = _parameterNames.Count == 0;
        Specificity = CalculateSpecificity(Phrase);
        IndexKey = KeyOf(Phrase);
    }

    public BddEffectiveStepKeyword Keyword { get; }

    /// <summary>Whether it is a declared state (<c>[BddState]</c>): a <c>Given</c> that <c>set_state</c> may run (AI10).</summary>
    public bool IsState { get; init; }

    /// <summary>
    /// The catalog's index key: the phrase's first one or two whole literal words, a space between; empty for a phrase that
    /// begins with a parameter. A step this pattern matches begins with exactly these words, since the regex is
    /// <c>^</c>, the literal text and <c>\s+</c> between words.
    /// </summary>
    public string IndexKey { get; }

    /// <summary>
    /// A step's first word and its first two (a space between), split on whitespace as a phrase's <c>\s+</c> matches it;
    /// null where it has none, or where it begins with whitespace, which only a phrase that begins with a parameter matches.
    /// </summary>
    public static (string? One, string? Two) LeadingWords(string text)
    {
        if (text.Length == 0 || char.IsWhiteSpace(text[0]))
        {
            return (null, null);
        }

        var first = EndOfWord(text, 0);
        var one = text[..first];
        var second = first;
        while (second < text.Length && char.IsWhiteSpace(text[second]))
        {
            second++;
        }

        return second < text.Length ? (one, $"{one} {text[second..EndOfWord(text, second)]}") : (one, null);
    }

    private static int EndOfWord(string text, int from)
    {
        while (from < text.Length && !char.IsWhiteSpace(text[from]))
        {
            from++;
        }

        return from;
    }

    // A word is whole when whitespace follows it in the phrase, or when it ends a phrase with no parameter: "I open {page}"
    // is "I open", "I{x}" begins with a parameter as far as the index knows, "Done" is "Done".
    private static string KeyOf(string phrase)
    {
        var parameter = Regex.Match(phrase, @"\{[A-Za-z][A-Za-z0-9_-]*\}");
        var literal = parameter.Success ? phrase[..parameter.Index] : phrase;
        var words = new List<string>(2);
        var at = 0;
        while (words.Count < 2 && at < literal.Length && !char.IsWhiteSpace(literal[at]))
        {
            var end = EndOfWord(literal, at);
            var whole = end < literal.Length || !parameter.Success;
            if (!whole)
            {
                break;
            }

            words.Add(literal[at..end]);
            at = end;
            while (at < literal.Length && char.IsWhiteSpace(literal[at]))
            {
                at++;
            }
        }

        return string.Join(' ', words);
    }

    public string Phrase { get; }

    public string CommandId { get; }

    public bool RequiresTable { get; }

    public bool AllowsTable { get; }

    public BddCommandHandler? Handler { get; }

    /// <summary>What the step supplies, with types; empty for a pattern whose arguments stay text.</summary>
    public IReadOnlyList<BddPhraseParameter> Parameters { get; }

    /// <summary>Why the phrase cannot run, reported when a step binds to it; null when it is fine.</summary>
    public BddPhraseProblem? Problem { get; }

    /// <summary>The callable phrase this pattern calls; null for a C# phrase.</summary>
    public BddCallable? Callable { get; init; }

    /// <summary>Whether the step says which page the user is on (<c>I am on the {page} page</c>).</summary>
    public bool NamesPage { get; init; }

    /// <summary>The page a page phrase belongs to; null for any other phrase.</summary>
    public string? Page { get; init; }

    /// <summary>The page that is current after the step: a page phrase's returned page; null when the step does not say.</summary>
    public string? PageAfter { get; init; }

    /// <summary>What the control method is, for a control verb; null for any other phrase.</summary>
    public BddControlVerb? ControlVerb { get; init; }

    /// <summary>The namespace of a C# phrase class's phrase (<see cref="BddNamespaces"/>); <c>""</c> when every scenario sees it.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Whether the step binds against the current page: a control verb or a page phrase.</summary>
    public bool IsPageScoped => ControlVerb is not null || Page is not null;

    public bool IsExact { get; }

    public int Specificity { get; }

    public BddCommandMatch? TryMatch(BddStep step)
    {
        if (step.EffectiveKeyword != Keyword)
        {
            return null;
        }

        var match = _regex.Match(step.Text);
        if (!match.Success)
        {
            return null;
        }

        Dictionary<string, string> arguments = new(StringComparer.Ordinal);
        Dictionary<string, string> raw = new(StringComparer.Ordinal);
        for (var i = 0; i < _parameterNames.Count; i++)
        {
            raw[_parameterNames[i]] = match.Groups[$"p{i}"].Value.Trim();
            arguments[_parameterNames[i]] = TrimArgument(match.Groups[$"p{i}"].Value);
        }

        return new BddCommandMatch(this, arguments) { RawArguments = raw };
    }

    /// <summary>The <c>{x}</c> placeholders of a phrase, in order.</summary>
    public static IReadOnlyList<string> ParameterNames(string phrase)
        => Regex.Matches(phrase, @"\{([A-Za-z][A-Za-z0-9_-]*)\}").Select(match => match.Groups[1].Value).ToArray();

    private static (Regex Regex, IReadOnlyList<string> Parameters) Compile(string phrase)
    {
        var matches = Regex.Matches(phrase, @"\{([A-Za-z][A-Za-z0-9_-]*)\}");
        List<string> parameterNames = [];
        var pattern = "^";
        var lastIndex = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            pattern += EscapeLiteral(phrase[lastIndex..match.Index]);
            pattern += $"(?<p{i}>.+?)";
            parameterNames.Add(match.Groups[1].Value);
            lastIndex = match.Index + match.Length;
        }

        pattern += EscapeLiteral(phrase[lastIndex..]);
        pattern += "$";

        // Interpreted, not compiled: every step is tried against every pattern, so a compiled one cost its compile and JIT on a
        // catalog's first walk, seconds for a suite's hundreds of phrases (speed plan, iteration 4), to match one short line.
        return (new Regex(pattern, RegexOptions.CultureInvariant), parameterNames);
    }

    private static int CalculateSpecificity(string phrase)
    {
        var withoutParameters = Regex.Replace(phrase, @"\{[A-Za-z][A-Za-z0-9_-]*\}", string.Empty);
        return withoutParameters.Count(x => !char.IsWhiteSpace(x));
    }

    private static string EscapeLiteral(string literal)
    {
        var escaped = string.Empty;
        var inWhitespace = false;

        foreach (var character in literal)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!inWhitespace)
                {
                    escaped += @"\s+";
                    inWhitespace = true;
                }

                continue;
            }

            escaped += Regex.Escape(character.ToString());
            inWhitespace = false;
        }

        return escaped;
    }

    private static string TrimArgument(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }
}

/// <summary>The control method a control verb invokes, which the binder checks a page's control against.</summary>
/// <param name="MethodName">The method, invoked by name.</param>
/// <param name="ArgumentCount">How many arguments the step passes: the item, then the value or literal.</param>
/// <param name="NamesControl">Whether the phrase has <c>{control}</c>; without it, the page's one control that declares the verb runs it.</param>
/// <param name="Navigates">Whether the verb can leave the page (<see cref="Bruinet.Core.Testing.BddStepAttribute.Navigates"/>).</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddControlVerb(string MethodName, int ArgumentCount, bool NamesControl, bool Navigates);

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddCommandMatch(
    BddCommandPattern Pattern,
    IReadOnlyDictionary<string, string> Arguments)
{
    /// <summary>The arguments as written, quotes kept, which typed parameters are read from.</summary>
    public IReadOnlyDictionary<string, string> RawArguments { get; init; } = Arguments;
}

[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public delegate Task<BddStepResult> BddCommandHandler(
    BddExecutionContext context,
    BddStepInvocation invocation,
    CancellationToken cancellationToken);
