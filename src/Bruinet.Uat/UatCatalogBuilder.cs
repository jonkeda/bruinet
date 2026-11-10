using Bruinet.Bdd;
using System.Reflection;
using Bruinet.Core.Interfaces;

namespace Bruinet.Uat;

/// <summary>
/// A control step discovered from a <see cref="BddStepAttribute"/> on a control method. The engine invokes
/// <see cref="MethodName"/> by name, so no reference to the control type is needed.
/// </summary>
internal sealed record UatStepBinding(
    BddEffectiveStepKeyword Keyword,
    string Phrase,
    string CommandSuffix,
    string MethodName,
    string? ValueArgumentName,
    bool? Literal,
    Type? ValueType = null)
{
    /// <summary>The placeholder that names the collection's row; null for a verb on the control itself.</summary>
    public string? ItemArgumentName { get; init; }

    /// <summary>Whether the phrase names its control with <c>{control}</c>.</summary>
    public bool NamesControl { get; init; } = true;

    /// <summary>Whether the verb can take the user to another page.</summary>
    public bool Navigates { get; init; }

    /// <summary>How many arguments the step passes: the item, then the value or literal.</summary>
    public int ArgumentCount => (ItemArgumentName is null ? 0 : 1) + (ValueArgumentName is null && Literal is null ? 0 : 1);
}

/// <summary>
/// Discovers control-step vocabulary from <see cref="BddStepAttribute"/> declarations on control methods and
/// projects it into a <see cref="BddCommandCatalog"/>. This replaces the hand-written phrase
/// tables: the runtime and spec catalogs are the same discovery pass with and without handlers.
/// </summary>
internal static class UatCatalogBuilder
{
    public const string ControlParameter = "control";
    public const string ItemParameter = "item";
    private const string UserParameter = "user";

    // The Bruinet.Core assembly declares the built-in control vocabulary via [BddWhen]/[BddThen].
    // A type whose dependency is missing (xunit, when the bruinet data tool loads Core) is skipped.
    private static readonly Type[] BuiltinStepTypes = LoadableTypes(typeof(IElementObject<>).Assembly);

    public static IReadOnlyList<Type> CoreStepTypes => BuiltinStepTypes;

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }

    public static void RegisterControlVerbs(
        BddCommandCatalog catalog,
        IEnumerable<Type> stepTypes,
        string commandIdPrefix,
        Func<UatStepBinding, BddCommandHandler?>? handlerFactory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stepTypes);
        ArgumentNullException.ThrowIfNull(commandIdPrefix);

        foreach (var binding in DiscoverBindings(stepTypes, catalog.AddUnmatchable))
        {
            catalog.Register(
                binding.Keyword,
                binding.Phrase,
                commandIdPrefix + binding.CommandSuffix,
                allowsTable: false,
                handler: handlerFactory?.Invoke(binding),
                controlVerb: new BddControlVerb(binding.MethodName, binding.ArgumentCount, binding.NamesControl, binding.Navigates),
                parameters: [
                    .. binding.ItemArgumentName is { } item
                        ? [new BddPhraseParameter(item, typeof(string), BddParameterSource.Phrase, Optional: false, AllowsNull: false)]
                        : Array.Empty<BddPhraseParameter>(),
                    .. binding is { ValueArgumentName: { } name, ValueType: { } type }
                        ? [new BddPhraseParameter(name, type, BddParameterSource.Phrase, Optional: false, AllowsNull: false)]
                        : Array.Empty<BddPhraseParameter>(),
                ]);
        }
    }

    public static IReadOnlyList<UatStepBinding> DiscoverBindings(IEnumerable<Type> stepTypes, Action<BddUnmatchableDeclaration>? unmatchable = null)
    {
        List<UatStepBinding> bindings = [];
        HashSet<(BddEffectiveStepKeyword Keyword, string Phrase)> seen = [];

        foreach (var type in stepTypes)
        {
            foreach (var method in type.GetMethods(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var attribute in method.GetCustomAttributes<BddStepAttribute>(inherit: false))
                {
                    // A control verb declares {control} and its command in text; it is never inferred.
                    if (attribute.IsInferred)
                    {
                        unmatchable?.Invoke(new BddUnmatchableDeclaration(
                            attribute.Keyword,
                            new BddPhraseProblem(
                                "UATA013",
                                $"is a control verb with an empty [Bdd{attribute.Keyword}]; a control verb writes its phrase, with {{control}}.",
                                $"{type.FullName}.{method.Name}")));
                        continue;
                    }

                    if (!seen.Add((attribute.Keyword, attribute.Phrase)))
                    {
                        continue;
                    }

                    var names = BddCommandPattern.ParameterNames(attribute.Phrase);
                    var itemArgument = names.Contains(ItemParameter) ? ItemParameter : null;
                    var valueArgument = ResolveValueArgument(names);
                    var valueIndex = itemArgument is null ? 0 : 1;
                    bindings.Add(new UatStepBinding(
                        attribute.Keyword,
                        attribute.Phrase,
                        attribute.CommandId ?? method.Name,
                        method.Name,
                        valueArgument,
                        attribute.HasLiteral ? attribute.Literal : null,
                        valueArgument is null ? null : method.GetParameters().ElementAtOrDefault(valueIndex)?.ParameterType is { IsGenericParameter: false } valueType ? valueType : null)
                    {
                        ItemArgumentName = itemArgument,
                        NamesControl = names.Contains(ControlParameter),
                        Navigates = attribute.Navigates,
                    });
                }
            }
        }

        return bindings;
    }

    // The one placeholder that is not {control}, {user} (the actor, AD-015) or {item} (the row) is
    // the value argument; literal-only phrases have none.
    private static string? ResolveValueArgument(IReadOnlyList<string> names)
    {
        var values = names.Where(name => name is not (ControlParameter or UserParameter or ItemParameter)).ToArray();
        return values.Length == 1 ? values[0] : null;
    }
}
