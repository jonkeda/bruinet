using System.Reflection;
using System.Text;

namespace Bruinet.Traits;

/// <summary>One test with a trait problem.</summary>
/// <param name="Test">The test, as <c>Namespace.Class.Method</c>.</param>
/// <param name="Problem">What is wrong, in one sentence.</param>
public sealed record TraitProblem(string Test, string Problem);

/// <summary>What <see cref="TraitCheck"/> accepts as an owner.</summary>
public sealed class TraitCheckOptions
{
    /// <summary>The module names the app knows, or <c>null</c> to accept any.</summary>
    public IReadOnlyCollection<string>? KnownModules { get; init; }

    /// <summary>The feature names the app knows, or <c>null</c> to accept any.</summary>
    public IReadOnlyCollection<string>? KnownFeatures { get; init; }

    /// <summary>The component names the app knows, or <c>null</c> to accept any.</summary>
    public IReadOnlyCollection<string>? KnownComponents { get; init; }

    /// <summary>The Common area names the app knows, or <c>null</c> to accept any.</summary>
    public IReadOnlyCollection<string>? KnownCommonAreas { get; init; }
}

/// <summary>
/// Checks that every test in an assembly has exactly one tier and at least one known owner.
/// </summary>
/// <remarks>
/// Reads both the Bruinet trait attributes and plain <c>[Trait("key", "value")]</c>, from the method
/// and from its class and the class's base classes, which is what xUnit reports. Use it in one
/// architecture test per test project; start by printing <see cref="Report"/> and switch to
/// asserting an empty result once the backlog is fixed.
/// </remarks>
public static class TraitCheck
{
    private const string FactAttributeName = "Xunit.FactAttribute";
    private const string TraitAttributeName = "Xunit.TraitAttribute";

    /// <summary>Returns every trait problem in <paramref name="assembly"/>, ordered by test name.</summary>
    public static IReadOnlyList<TraitProblem> Check(Assembly assembly, TraitCheckOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        options ??= new TraitCheckOptions();

        var problems = new List<TraitProblem>();
        foreach (var (test, traits) in ReadTraits(assembly))
            problems.AddRange(CheckOne(test, traits, options));

        return [.. problems.OrderBy(p => p.Test, StringComparer.Ordinal).ThenBy(p => p.Problem, StringComparer.Ordinal)];
    }

    /// <summary>Returns the traits of every test in <paramref name="assembly"/>, keyed by test name.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, string>>> ReadTraits(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var result = new SortedDictionary<string, IReadOnlyList<KeyValuePair<string, string>>>(StringComparer.Ordinal);
        foreach (var type in LoadableTypes(assembly).Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsGenericTypeDefinition))
        {
            var classTraits = ClassChain(type).SelectMany(TraitsOf).ToList();
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!IsTest(method))
                    continue;

                result[$"{type.FullName}.{method.Name}"] = [.. TraitsOf(method), .. classTraits];
            }
        }

        return result;
    }

    /// <summary>A plain-text report: one line per problem and a count.</summary>
    public static string Report(IReadOnlyList<TraitProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        var text = new StringBuilder();
        foreach (var problem in problems)
            text.Append(problem.Test).Append(": ").AppendLine(problem.Problem);
        text.Append(problems.Select(p => p.Test).Distinct(StringComparer.Ordinal).Count())
            .Append(" test(s) with ").Append(problems.Count).Append(" trait problem(s).");
        return text.ToString();
    }

    private static IEnumerable<TraitProblem> CheckOne(
        string test, IReadOnlyList<KeyValuePair<string, string>> traits, TraitCheckOptions options)
    {
        // An old tier name next to its new one is one tier, not two.
        var tiers = Values(traits, TraitKeys.Pyramid).Select(TierNames.Current).Distinct(StringComparer.Ordinal).ToList();
        if (tiers.Count == 0)
            yield return new(test, "has no tier; add [Unit], [Integration], [Ui], [Live] or [Video].");
        else if (tiers.Count > 1)
            yield return new(test, $"has more than one tier: {string.Join(", ", tiers)}.");

        foreach (var tier in tiers.Where(t => !TierNames.All.Contains(t)))
            yield return new(test, $"has unknown tier '{tier}'.");

        var modules = Values(traits, TraitKeys.Module);
        var features = Values(traits, TraitKeys.Feature);
        var components = Values(traits, TraitKeys.Component);
        var areas = Values(traits, TraitKeys.CommonArea);
        if (modules.Count + features.Count + components.Count + areas.Count == 0)
            yield return new(test, "has no owner; add [Module], [Feature], [Component] or [CommonArea].");

        foreach (var problem in Unknown(test, "module", modules, options.KnownModules)
                     .Concat(Unknown(test, "feature", features, options.KnownFeatures))
                     .Concat(Unknown(test, "component", components, options.KnownComponents))
                     .Concat(Unknown(test, "Common area", areas, options.KnownCommonAreas)))
            yield return problem;
    }

    private static IEnumerable<TraitProblem> Unknown(
        string test, string kind, IReadOnlyList<string> names, IReadOnlyCollection<string>? known)
        => known is null
            ? []
            : names.Where(n => !known.Contains(n, StringComparer.Ordinal))
                .Select(n => new TraitProblem(test, $"names unknown {kind} '{n}'."));

    private static List<string> Values(IReadOnlyList<KeyValuePair<string, string>> traits, string key)
        => [.. traits.Where(t => t.Key == key).Select(t => t.Value).Distinct(StringComparer.Ordinal)];

    private static bool IsTest(MethodInfo method)
        => method.GetCustomAttributes(inherit: true).Any(a => DerivesFrom(a.GetType(), FactAttributeName));

    private static IEnumerable<Type> ClassChain(Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
            yield return current;
    }

    private static IEnumerable<KeyValuePair<string, string>> TraitsOf(MemberInfo member)
    {
        foreach (var data in member.GetCustomAttributesData())
        {
            if (data.AttributeType.FullName == TraitAttributeName
                && data.ConstructorArguments is [{ Value: string key }, { Value: string value }])
            {
                yield return new(key, value);
            }
        }

        foreach (var attribute in member.GetCustomAttributes<BruinetTraitAttribute>(inherit: false))
        {
            foreach (var trait in attribute.Traits)
                yield return trait;
        }
    }

    private static bool DerivesFrom(Type? type, string fullName)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.FullName == fullName)
                return true;
        }

        return false;
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
