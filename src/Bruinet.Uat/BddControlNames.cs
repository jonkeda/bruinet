using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Bruinet.Bdd;

namespace Bruinet.Uat;

/// <summary>
/// The control names a page and its components declare with <c>[BddControl]</c> on a method or a component's property
/// (<c>the filter above {column}</c>, <c>the footer</c>): words that name a control which has no fixed name of its own. A
/// step's <c>{control}</c> is first a control the page exposes by name, then one of these.
/// </summary>
internal static partial class BddControlNames
{
    /// <summary>One declared name: its words, the component that declares it (null for the page), and its member.</summary>
    /// <param name="Phrase">The words, with <c>{parameter}</c> placeholders.</param>
    /// <param name="Owner">The page's property of the component that declares it; null when the page itself does.</param>
    /// <param name="Member">The method (its parameters are the placeholders) or the property.</param>
    public sealed record Named(string Phrase, PropertyInfo? Owner, MemberInfo Member)
    {
        /// <summary>The type of the control the name gives.</summary>
        public Type ControlType => Member switch
        {
            MethodInfo method => method.ReturnType,
            PropertyInfo property => property.PropertyType,
            _ => typeof(object),
        };

        internal Regex Pattern { get; } = ToPattern(Phrase);
    }

    /// <summary>The names a page type and the components its control properties hold declare.</summary>
    /// <param name="pageType">The page object's type.</param>
    /// <param name="controls">The page's control properties.</param>
    public static IReadOnlyList<Named> Of(Type pageType, IEnumerable<PropertyInfo> controls)
    {
        List<Named> names = [.. Declared(pageType, owner: null, includeProperties: false)];
        foreach (var control in controls)
        {
            names.AddRange(Declared(control.PropertyType, control, includeProperties: true));
        }

        return names;
    }

    /// <summary>The name that <paramref name="text"/> is, and its arguments; null for none, or for more than one (then <paramref name="problem"/> says which).</summary>
    public static Named? Match(IReadOnlyList<Named> names, string text, out IReadOnlyDictionary<string, string> arguments, out string? problem)
    {
        arguments = new Dictionary<string, string>();
        problem = null;
        var matches = names
            .Select(name => (name, match: name.Pattern.Match(text.Trim())))
            .Where(candidate => candidate.match.Success)
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        if (matches.Count > 1)
        {
            problem = $"'{text}' names several controls ({string.Join(", ", matches.Select(m => m.name.Owner is null ? m.name.Phrase : $"{m.name.Phrase} in {m.name.Owner.Name}"))}).";
            return null;
        }

        var (found, success) = matches[0];
        arguments = found.Pattern.GetGroupNumbers().Skip(1)
            .Select(found.Pattern.GroupNameFromNumber)
            .ToDictionary(group => group, group => Unquote(success.Groups[group].Value.Trim()), StringComparer.Ordinal);
        return found;
    }

    /// <summary>The control a name gives on a page: its component's method or property, with the step's arguments.</summary>
    /// <param name="named">The name.</param>
    /// <param name="page">The page object.</param>
    /// <param name="arguments">The name's placeholders' values.</param>
    public static object? Resolve(Named named, object page, IReadOnlyDictionary<string, string> arguments)
    {
        var owner = named.Owner is null ? page : named.Owner.GetValue(page);
        if (owner is null)
        {
            return null;
        }

        return named.Member switch
        {
            PropertyInfo property => property.GetValue(owner),
            MethodInfo method => method.Invoke(owner, [.. method.GetParameters().Select(parameter => Argument(parameter, arguments))]),
            _ => null,
        };
    }

    private static object? Argument(ParameterInfo parameter, IReadOnlyDictionary<string, string> arguments)
    {
        if (parameter.Name is { } name && arguments.TryGetValue(name, out var value))
        {
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            return type == typeof(string) ? value : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        return parameter.HasDefaultValue ? parameter.DefaultValue : null;
    }

    private static IEnumerable<Named> Declared(Type type, PropertyInfo? owner, bool includeProperties)
    {
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => !method.IsSpecialName))
        {
            foreach (var attribute in method.GetCustomAttributes<BddControlAttribute>(inherit: true))
            {
                yield return new Named(attribute.Name, owner, method);
            }
        }

        if (!includeProperties)
        {
            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            foreach (var attribute in property.GetCustomAttributes<BddControlAttribute>(inherit: true))
            {
                yield return new Named(attribute.Name, owner, property);
            }
        }
    }

    // "the filter above {column}" → ^the filter above (?<column>.+)$, any case; a leading "the" may be left out.
    private static Regex ToPattern(string phrase)
    {
        var pattern = Placeholder().Replace(Regex.Escape(phrase.Trim()).Replace("\\ ", "\\s+", StringComparison.Ordinal), match => $"(?<{match.Groups[1].Value}>.+?)");
        if (pattern.StartsWith("the\\s+", StringComparison.OrdinalIgnoreCase))
        {
            pattern = "(?:the\\s+)?" + pattern["the\\s+".Length..];
        }

        return new Regex($"^{pattern}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    [GeneratedRegex(@"\\\{([A-Za-z][A-Za-z0-9_-]*)}")]
    private static partial Regex Placeholder();
}
