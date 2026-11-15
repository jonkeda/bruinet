using System.Reflection;
using Bruinet.Bdd;

namespace Bruinet.Uat;

/// <summary>
/// Extension verbs (<see cref="BddVerbsAttribute"/>): finding the classes, whether one applies to a control's type, and
/// the method to call for it.
/// </summary>
internal static class BddExtensionVerbs
{
    /// <summary>The <c>[BddVerbs]</c> classes of <paramref name="assemblies"/>.</summary>
    public static IEnumerable<Type> In(IEnumerable<Assembly> assemblies) =>
        assemblies.Distinct().SelectMany(Types).Where(IsVerbClass);

    /// <summary>Whether a type is a <c>[BddVerbs]</c> class.</summary>
    public static bool IsVerbClass(Type type) =>
        type is { IsAbstract: true, IsSealed: true } && type.IsDefined(typeof(BddVerbsAttribute), inherit: false);

    /// <summary>The verb's method for a control of <paramref name="controlType"/>, made concrete; null when it does not apply.</summary>
    public static MethodInfo? For(MethodInfo verb, Type controlType)
    {
        var target = verb.GetParameters()[0].ParameterType;
        if (!verb.IsGenericMethodDefinition)
        {
            return target.IsAssignableFrom(controlType) ? verb : null;
        }

        // A generic verb on a generic type (this TableControl<TScope> table): the control's matching base or interface
        // gives the type arguments.
        foreach (var candidate in SelfBasesAndInterfaces(controlType))
        {
            if (!candidate.IsGenericType || !target.IsGenericType || candidate.GetGenericTypeDefinition() != target.GetGenericTypeDefinition())
            {
                continue;
            }

            var map = new Dictionary<Type, Type>();
            var targetArguments = target.GetGenericArguments();
            var candidateArguments = candidate.GetGenericArguments();
            for (var i = 0; i < targetArguments.Length; i++)
            {
                if (targetArguments[i].IsGenericParameter)
                {
                    map[targetArguments[i]] = candidateArguments[i];
                }
            }

            var parameters = verb.GetGenericArguments();
            if (parameters.All(map.ContainsKey))
            {
                try
                {
                    return verb.MakeGenericMethod([.. parameters.Select(parameter => map[parameter])]);
                }
                catch (ArgumentException)
                {
                    return null; // a constraint the control's type arguments do not meet
                }
            }
        }

        return null;
    }

    private static IEnumerable<Type> SelfBasesAndInterfaces(Type type)
    {
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current;
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return contract;
        }
    }

    private static Type[] Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return [.. exception.Types.OfType<Type>()];
        }
    }
}
