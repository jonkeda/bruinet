using System.Reflection;
using Bruinet.Bdd;

namespace Bruinet.Uat;

/// <summary>
/// The names pages and controls declare, read from <c>[BddPage]</c>, <c>[BddControl]</c> and <c>[BddNavigatesTo]</c>, and
/// for one release from the obsolete <c>[UatName]</c>, <c>[UatNavigatesTo]</c> and <c>[UatAction]</c> (AD-040).
/// </summary>
#pragma warning disable CS0618 // The obsolete attributes are read until they are removed.
internal static class BddNames
{
    /// <summary>The declared name of a page type or a control member; null when it declares none.</summary>
    public static string? Declared(MemberInfo member) => member switch
    {
        Type type => type.GetCustomAttribute<BddPageAttribute>()?.Name ?? type.GetCustomAttribute<UatNameAttribute>()?.Name,
        _ => FixedControlName(member) ?? member.GetCustomAttribute<BddPageAttribute>()?.Name ?? member.GetCustomAttribute<UatNameAttribute>()?.Name,
    };

    /// <summary>Whether a page type or a control member declares its name.</summary>
    public static bool IsDeclared(MemberInfo member) => Declared(member) is not null;

    /// <summary>The page a control always goes to; null when it declares none.</summary>
    public static Type? NavigatesTo(PropertyInfo property) =>
        property.GetCustomAttribute<BddNavigatesToAttribute>()?.Page ?? property.GetCustomAttribute<UatNavigatesToAttribute>()?.Page;

    /// <summary>The obsolete action names a control method declares.</summary>
    public static IEnumerable<string> Actions(MethodInfo method) =>
        method.GetCustomAttributes<UatActionAttribute>().Select(attribute => attribute.ActionName);

    // A [BddControl] with placeholders names a control a method makes from its arguments, not this member.
    private static string? FixedControlName(MemberInfo member) =>
        member.GetCustomAttributes<BddControlAttribute>().Select(attribute => attribute.Name).FirstOrDefault(name => !name.Contains('{', StringComparison.Ordinal));
}
