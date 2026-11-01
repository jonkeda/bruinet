using System.Diagnostics.CodeAnalysis;
namespace Bruinet.Bdd;

/// <summary>The callable phrases a scenario file can see: its own and the global phrase files'.</summary>
public sealed class BddCallableSet
{
    private readonly Dictionary<BddCatalogSet, BddPreparedCallables> _prepared = [];

    /// <summary>Creates the set.</summary>
    /// <param name="callables">The callables, in the order they were found.</param>
    public BddCallableSet(IEnumerable<BddCallable> callables)
    {
        ArgumentNullException.ThrowIfNull(callables);
        Callables = callables.ToArray();
    }

    /// <summary>No callables.</summary>
    public static BddCallableSet Empty { get; } = new([]);

    /// <summary>The callables.</summary>
    public IReadOnlyList<BddCallable> Callables { get; }

    /// <summary>The callables with their types and problems, against <paramref name="catalogs"/>; built once per catalog set.</summary>
    public BddPreparedCallables Prepare(BddCatalogSet catalogs)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        lock (_prepared)
        {
            if (!_prepared.TryGetValue(catalogs, out var prepared))
            {
                prepared = new BddPreparedCallables(Callables, catalogs);
                _prepared[catalogs] = prepared;
            }

            return prepared;
        }
    }
}

/// <summary>A callable with what binding its body taught: the types of its values, and whether it is broken.</summary>
/// <param name="Callable">The callable.</param>
/// <param name="Types">The heading's parameters and, for a per-row callable, its columns.</param>
/// <param name="Columns">A per-row callable's columns: the body's placeholders that the heading does not name.</param>
/// <param name="Passes">What the body takes from under the call.</param>
/// <param name="BlockType">What the passed table binds to, when a body step says; null otherwise.</param>
/// <param name="Problem">Why it cannot be called; null when it can. Only its calls fail (AD-016 A10).</param>
public sealed record BddPreparedCallable(
    BddCallable Callable,
    IReadOnlyDictionary<string, Type> Types,
    IReadOnlyList<string> Columns,
    BddPassedBlock Passes,
    Type? BlockType,
    BddPhraseProblem? Problem)
{
    /// <summary>The phrase as the phrase list shows it: <c>a project {name: text}</c>, and a per-row callable's columns.</summary>
    public override string ToString()
    {
        var text = Callable.Phrase;
        foreach (var name in Callable.HeadingParameters.Where(Types.ContainsKey))
        {
            text = text.Replace($"{{{name}}}", $"{{{name}: {BddValues.Describe(Types[name])}}}", StringComparison.Ordinal);
        }

        if (Callable.Kind == BddCallableKind.EachRow)
        {
            text += $"  for each row ({string.Join(", ", Columns.Where(Types.ContainsKey).Select(column => $"{column}: {BddValues.Describe(Types[column])}"))})";
        }
        else if (Passes == BddPassedBlock.Table)
        {
            text += BlockType is null || BlockType == typeof(BddTable) ? "  + table" : $"  + table: {BddValues.Describe(BlockType)}";
        }
        else if (Passes == BddPassedBlock.DocString)
        {
            text += "  + doc string";
        }

        return text;
    }
}

/// <summary>
/// The callables of a file prepared against its catalogs: each body bound once with its
/// placeholders in, to learn the types, before any call is bound.
/// </summary>
public sealed class BddPreparedCallables
{
    private const string Table = "table";

    private static readonly BddEffectiveStepKeyword[] Keywords = [BddEffectiveStepKeyword.Given, BddEffectiveStepKeyword.When, BddEffectiveStepKeyword.Then];

    private readonly BddCatalogSet _catalogs;
    private readonly Dictionary<BddCallable, BddPreparedCallable> _prepared = [];
    private readonly HashSet<BddCallable> _preparing = [];
    private readonly List<BddCommandPattern> _patterns;
    private readonly Lazy<IReadOnlyList<Type>> _knownTypes;

    internal BddPreparedCallables(IReadOnlyList<BddCallable> callables, BddCatalogSet catalogs)
    {
        _catalogs = catalogs;
        Callables = callables;

        // A callable has no keyword: its heading is a phrase for Given, When and Then alike.
        _patterns = callables
            .SelectMany(callable => Keywords.Select(keyword =>
                new BddCommandPattern(keyword, callable.Phrase, $"Callable: {callable.Phrase}") { Callable = callable }))
            .ToList();
        _knownTypes = new(() => KnownTypes(catalogs));
    }

    /// <summary>The callables, unprepared.</summary>
    public IReadOnlyList<BddCallable> Callables { get; }

    /// <summary>Every callable, prepared.</summary>
    public IReadOnlyList<BddPreparedCallable> All => Callables.Select(Get).ToArray();

    /// <summary>The callables whose heading the step's text fits.</summary>
    public IReadOnlyList<BddCommandMatch> Candidates(BddStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return _patterns.Select(pattern => pattern.TryMatch(step)).OfType<BddCommandMatch>().ToArray();
    }

    /// <summary>The callable, prepared (once).</summary>
    public BddPreparedCallable Get(BddCallable callable)
    {
        ArgumentNullException.ThrowIfNull(callable);
        lock (_prepared)
        {
            if (_prepared.TryGetValue(callable, out var prepared))
            {
                return prepared;
            }

            // Reached again while its own body is being prepared: a cycle, which the step that
            // reached it reports.
            if (_preparing.Contains(callable))
            {
                return new BddPreparedCallable(callable, new Dictionary<string, Type>(), [], BddPassedBlock.None, null,
                    new BddPhraseProblem("UATC004", "calls itself.", callable.Source.ToString()));
            }

            _preparing.Add(callable);
            try
            {
                prepared = Prepare(callable);
            }
            finally
            {
                _preparing.Remove(callable);
            }

            _prepared[callable] = prepared;
            return prepared;
        }
    }

    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    private BddPreparedCallable Prepare(BddCallable callable)
    {
        var declaration = callable.Source.ToString();
        BddPhraseProblem? problem = null;
        void Fail(string code, string message) => problem ??= new BddPhraseProblem(code, message, declaration);

        var heading = callable.HeadingParameters;
        var used = callable.Steps.SelectMany(BddOutline.Placeholders).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in heading.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            Fail("UATC001", $"names {{{name.Key}}} twice in its heading.");
        }

        foreach (var name in heading.Where(name => !used.Contains(name, StringComparer.Ordinal)))
        {
            Fail("UATC001", $"has {{{name}}} in its heading, but no step uses <{name}>.");
        }

        List<string> columns = [];
        if (callable.Kind == BddCallableKind.Once)
        {
            foreach (var name in used.Where(name => !heading.Contains(name, StringComparer.Ordinal)))
            {
                Fail("UATC001", $"uses <{name}>, which its heading does not name. Add {{{name}}} to the heading.");
            }
        }
        else
        {
            columns = used.Where(name => !heading.Contains(name, StringComparer.Ordinal)).ToList();
            if (callable.Steps.Any(step => step.Passes == BddPassedBlock.Table))
            {
                Fail("UATC007", "runs once per row of the call's table, so it has no <table> to pass on.");
            }
        }

        var passes = callable.Steps.Any(step => step.Passes == BddPassedBlock.Table) ? BddPassedBlock.Table
            : callable.Steps.Any(step => step.Passes == BddPassedBlock.DocString) ? BddPassedBlock.DocString
            : BddPassedBlock.None;

        var inference = new BddInference();
        BddBinder.Infer(callable, _catalogs, this, inference);
        if (inference.Problem is { } found)
        {
            Fail(found.Code, found.Message);
        }

        Dictionary<string, Type> types = new(StringComparer.Ordinal);
        foreach (var name in heading.Concat(columns).Distinct(StringComparer.Ordinal))
        {
            var recorded = inference.Types.TryGetValue(name, out var list) ? list : [];
            if (Merge(recorded, out var conflict) is { } merged)
            {
                types[name] = merged;
            }
            else
            {
                Fail("UATC002", $"passes <{name}> to steps that cannot agree on its type: no value is both {conflict}.");
                types[name] = typeof(string);
            }
        }

        var blockType = inference.BlockTypes.FirstOrDefault(type => type != typeof(BddTable)) ?? inference.BlockTypes.FirstOrDefault();
        var context = new BddValueContext(DateTimeOffset.UtcNow, _catalogs.Apps, _catalogs.Default.ArgumentTypes);
        foreach (var declared in callable.Declared)
        {
            var isTable = declared.Name.Equals(Table, StringComparison.OrdinalIgnoreCase) && passes == BddPassedBlock.Table;
            var name = isTable ? Table : types.Keys.FirstOrDefault(key => BddTableBinding.Key(key) == BddTableBinding.Key(declared.Name));
            if (name is null)
            {
                Fail("UATC003", $"declares '{declared.Name}' in its Parameters table, which it does not have. It has: {string.Join(", ", types.Keys)}.");
                continue;
            }

            if (TypeNamed(declared.TypeName) is not { } type)
            {
                Fail("UATC003", $"declares '{declared.Name}' as '{declared.TypeName}', which is not a type a scenario can write. Use text, whole number, number, true/false, date, date and time, time, duration, user, list of ..., table, or a type a phrase already takes.");
                continue;
            }

            var inferred = isTable ? blockType : types[name];
            if (inferred is not null && inferred != typeof(string) && inferred != typeof(BddTable) && BddValues.Describe(inferred) != BddValues.Describe(type))
            {
                Fail("UATC002", $"declares '{declared.Name}' as {BddValues.Describe(type)}, but passes it to a step that takes {BddValues.Describe(inferred)}.");
                continue;
            }

            if (isTable)
            {
                blockType = type;
                continue;
            }

            types[name] = type;
            if (!string.IsNullOrEmpty(declared.Example) && !BddValues.TryRead(declared.Example, type, context, out _, out var exampleProblem, allowNull: true))
            {
                Fail("UATC003", $"has an example for '{declared.Name}' that is not a {BddValues.Describe(type)}: {exampleProblem!.Message}");
            }
        }

        return new BddPreparedCallable(callable, types, columns, passes, blockType, problem);
    }

    // Text gives way to any type; a whole number and a number agree on the whole number; other
    // kinds that differ have no value in common.
    private static Type? Merge(IReadOnlyList<Type> recorded, out string? conflict)
    {
        conflict = null;
        var kinds = recorded
            .Select(type => Nullable.GetUnderlyingType(type) ?? type)
            .Where(type => type != typeof(string))
            .GroupBy(BddValues.Describe)
            .Select(group => group.First())
            .ToList();
        if (kinds.Count == 0)
        {
            return typeof(string);
        }

        if (kinds.Count == 1)
        {
            return kinds[0];
        }

        if (kinds.All(type => BddValues.Describe(type) is "whole number" or "number"))
        {
            return kinds.First(type => BddValues.Describe(type) == "whole number");
        }

        conflict = string.Join(" and ", kinds.Select(BddValues.Describe));
        return null;
    }

    /// <summary>The type a <c>Parameters</c> table names, as the phrase list writes it; null when there is none.</summary>
    /// <param name="name">A type name: <c>number</c>, <c>list of date</c>, <c>TodoStatus</c>.</param>
    public Type? TypeNamed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.StartsWith("list of ", StringComparison.OrdinalIgnoreCase))
        {
            return TypeNamed(trimmed["list of ".Length..]) is { } element ? typeof(List<>).MakeGenericType(element) : null;
        }

        return trimmed.ToLowerInvariant() switch
        {
            "text" => typeof(string),
            "whole number" => typeof(int),
            "number" => typeof(decimal),
            "true/false" => typeof(bool),
            "date" => typeof(DateOnly),
            "date and time" => typeof(DateTime),
            "time" => typeof(TimeOnly),
            "duration" => typeof(TimeSpan),
            "user" => typeof(BddUserRole),
            Table => typeof(BddTable),
            _ => _knownTypes.Value.FirstOrDefault(type => type.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)),
        };
    }

    // The types the catalogs' phrases already take, and what those are made of: a type no phrase
    // takes could not be passed anywhere, so no assembly needs scanning for one.
    private static IReadOnlyList<Type> KnownTypes(BddCatalogSet catalogs)
    {
        HashSet<Type> known = [];
        void Add(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!known.Add(type))
            {
                return;
            }

            if (BddValues.ElementType(type) is { } element)
            {
                Add(element);
            }
            else if (!type.IsPrimitive && !type.IsEnum && type != typeof(string) && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
            {
                foreach (var parameter in type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
                {
                    Add(parameter.ParameterType);
                }
            }
        }

        foreach (var catalog in catalogs.All)
        {
            foreach (var parameter in catalog.Patterns.SelectMany(pattern => pattern.Parameters))
            {
                Add(parameter.Type);
            }

            foreach (var type in catalog.ArgumentTypes.Registered)
            {
                Add(type);
            }
        }

        return known.ToArray();
    }
}

/// <summary>What binding a callable's body with its placeholders in has taught so far.</summary>
internal sealed class BddInference
{
    public Dictionary<string, List<Type>> Types { get; } = new(StringComparer.Ordinal);

    public List<Type> BlockTypes { get; } = [];

    public BddPhraseProblem? Problem { get; private set; }

    public void Record(string name, Type type)
    {
        if (!Types.TryGetValue(name, out var list))
        {
            Types[name] = list = [];
        }

        list.Add(type);
    }

    public void Fail(string code, string message) => Problem ??= new BddPhraseProblem(code, message, string.Empty);
}
