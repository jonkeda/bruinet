using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Bruinet.Bdd;
using Bruinet.Core.Composition;

namespace Bruinet.Uat;

/// <summary>
/// The names a step can call a page's controls by, beside the page's own control properties: what the page and its
/// parts, to any depth, declare with <c>[BddControl]</c> (<c>the filter above {column}</c>, <c>the footer</c>), and the
/// members of a kind (<c>[BddKind]</c>), named after their member. A step's <c>{control}</c> is first a control the
/// page exposes by name, then one of these: by its declared words; by its name with or without its kind's word; or,
/// for a name that is on the page more than once, by its name and the part it is in (<c>Sales price in Financial</c>).
/// </summary>
internal static partial class BddControlNames
{
    // How deep a page's parts are looked in: a page, a tab, a section, a row.
    private const int Deepest = 4;

    // A part's names are kept per suite: what counts as another page, and so is not looked in, is the suite's to say.
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<Type, IReadOnlyList<Relative>>> Caches = [];
    private static readonly object NoSuite = new();

    [ThreadStatic]
    private static IReadOnlySet<Type>? suitePages;

    [ThreadStatic]
    private static ConcurrentDictionary<Type, IReadOnlyList<Relative>>? cache;

    /// <summary>One name: its words, the parts it is reached through (none for the page itself), and its member.</summary>
    /// <param name="Phrase">The words, with <c>{parameter}</c> placeholders.</param>
    /// <param name="Path">The properties from the page to the part that has the member, outermost first.</param>
    /// <param name="Member">The method (its parameters are the placeholders) or the property.</param>
    /// <param name="Kind">The kind's word of the control's type (<c>[BddKind]</c>); null when it has none.</param>
    /// <param name="Declared">Whether the words are declared with <c>[BddControl]</c>; false when they are the member's name.</param>
    public sealed record Named(string Phrase, IReadOnlyList<PropertyInfo> Path, MemberInfo Member, string? Kind, bool Declared)
    {
        /// <summary>The part that has the member; null when the page itself does.</summary>
        public PropertyInfo? Owner => Path.Count > 0 ? Path[^1] : null;

        /// <summary>The type of the control the name gives.</summary>
        public Type ControlType => TypeOf(Member);

        internal Regex Pattern { get; } = ToPattern(Phrase, kind: null);

        // The name with its kind's word before or after it, or without: "Name", "the field Name", "the Name field".
        internal Regex? WithKind { get; } = Kind is null ? null : ToPattern(Phrase, Kind);

        /// <summary>How a message says where the name is: <c>Sales price in Financial</c>.</summary>
        public string Placed => Owner is null ? Phrase : $"{Phrase} in {NameOf(Owner)}";
    }

    // A name as its type declares it, before it is known which page reaches it and how.
    private sealed record Relative(string Phrase, IReadOnlyList<PropertyInfo> Path, MemberInfo Member, string? Kind, bool Declared);

    /// <summary>The names a page type and its parts declare.</summary>
    /// <param name="pageType">The page object's type.</param>
    /// <param name="controls">The page's control properties (they are found by their own names first).</param>
    /// <param name="pages">The suite's page types: a page another page can give is not a part of it. Null when not known.</param>
    public static IReadOnlyList<Named> Of(Type pageType, IEnumerable<PropertyInfo> controls, IReadOnlySet<Type>? pages = null)
    {
        _ = controls;
        suitePages = pages;
        cache = Caches.GetOrCreateValue((object?)pages ?? NoSuite);
        try
        {
            return [.. Walk(pageType, isPage: true).Select(name => new Named(name.Phrase, name.Path, name.Member, name.Kind, name.Declared))];
        }
        finally
        {
            suitePages = null;
            cache = null;
        }
    }

    /// <summary>The name that <paramref name="text"/> is, and its arguments; null for none, or for more than one (then <paramref name="problem"/> says which).</summary>
    public static Named? Match(IReadOnlyList<Named> names, string text, out IReadOnlyDictionary<string, string> arguments, out string? problem)
    {
        arguments = new Dictionary<string, string>();
        problem = null;
        text = text.Trim();

        // The declared words first, as written: what bound before a kind or a place could be said still binds the same.
        var found = Candidates(names.Where(name => name.Declared), text, name => name.Pattern);
        if (found.Count == 0)
        {
            found = Candidates(names, text, name => name.WithKind ?? (name.Declared ? null : name.Pattern));
        }

        if (found.Count == 0)
        {
            found = Placed(names, text);
        }

        if (found.Count == 0)
        {
            return null;
        }

        if (found.Count > 1)
        {
            problem = $"'{text}' names several controls: say {string.Join(", or ", found.Select(candidate => Distinct(candidate.Name, found.Select(other => other.Name))))}.";
            return null;
        }

        var (named, match, pattern) = found[0];
        arguments = pattern.GetGroupNumbers().Skip(1)
            .Select(pattern.GroupNameFromNumber)
            .ToDictionary(group => group, group => Unquote(match.Groups[group].Value.Trim()), StringComparer.Ordinal);
        return named;
    }

    /// <summary>The control a name gives on a page: its part's method or property, with the step's arguments.</summary>
    /// <param name="named">The name.</param>
    /// <param name="page">The page object.</param>
    /// <param name="arguments">The name's placeholders' values.</param>
    public static object? Resolve(Named named, object page, IReadOnlyDictionary<string, string> arguments)
    {
        object? owner = page;
        foreach (var part in named.Path)
        {
            owner = part.GetValue(owner);
            if (owner is null)
            {
                return null;
            }
        }

        return named.Member switch
        {
            PropertyInfo property => property.GetValue(owner),
            MethodInfo method => method.Invoke(owner, [.. method.GetParameters().Select(parameter => Argument(parameter, arguments))]),
            _ => null,
        };
    }

    /// <summary>The names a message lists: each once, the declared words as they are, a member's name with its part.</summary>
    public static IEnumerable<string> Listed(IReadOnlyList<Named> names)
        => names.Select(name => name.Declared ? name.Phrase : name.Placed).Distinct(StringComparer.OrdinalIgnoreCase);

    private static List<(Named Name, Match Match, Regex Pattern)> Candidates(IEnumerable<Named> names, string text, Func<Named, Regex?> pattern)
    {
        List<(Named, Match, Regex)> found = [];
        foreach (var name in names)
        {
            if (pattern(name) is { } regex && regex.Match(text) is { Success: true } match &&
                !found.Any(other => other.Item1.Member == name.Member && other.Item1.Path.SequenceEqual(name.Path)))
            {
                found.Add((name, match, regex));
            }
        }

        return found;
    }

    // "Sales price in Financial", "Sales price under the section Financial": a name, and a part it is in. The last
    // such word first, so a name that holds one itself ("Paid in cash in Financial") still reads.
    private static List<(Named Name, Match Match, Regex Pattern)> Placed(IReadOnlyList<Named> names, string text)
    {
        foreach (var at in PlaceWord().Matches(text).Reverse())
        {
            var (what, where) = (text[..at.Index].Trim(), text[(at.Index + at.Length)..].Trim());
            var found = Candidates(names.Where(name => name.Path.Any(part => Is(part, where))), what, name => name.Pattern)
                is { Count: > 0 } declared
                ? declared
                : Candidates(names.Where(name => name.Path.Any(part => Is(part, where))), what, name => name.WithKind ?? (name.Declared ? null : name.Pattern));
            if (found.Count > 0)
            {
                return found;
            }
        }

        return [];
    }

    // Whether a part is what a step calls it: its name, with or without "the" and its kind's word.
    private static bool Is(PropertyInfo part, string said)
        => ToPattern(NameOf(part), KindOf(part.PropertyType)).IsMatch(said) || ToPattern(NameOf(part), kind: null).IsMatch(said);

    // What tells a candidate from the others in a message: its kind when the kinds differ, else its part.
    private static string Distinct(Named name, IEnumerable<Named> all)
        => name.Kind is not null && all.Count(other => string.Equals(other.Kind, name.Kind, StringComparison.OrdinalIgnoreCase)) == 1 && !name.Phrase.Contains('{', StringComparison.Ordinal)
            ? $"the {name.Kind} {StripThe(name.Phrase)}"
            : name.Placed;

    private static object? Argument(ParameterInfo parameter, IReadOnlyDictionary<string, string> arguments)
    {
        if (parameter.Name is { } name && arguments.TryGetValue(name, out var value))
        {
            var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
            return type == typeof(string) ? value : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        return parameter.HasDefaultValue ? parameter.DefaultValue : null;
    }

    // The types being read now, outermost first, and the shallowest of them a read below it ran into again.
    [ThreadStatic]
    private static List<Type>? reading;

    [ThreadStatic]
    private static int shallowestMet;

    // The names of a type and, through its properties, of its parts. A page's own properties with a declared name are
    // its controls already (the page finds them by name first); here they are named again only for their kind's word.
    // A part that leads back to a type being read (its Parent, its Self, its page) is not looked in again. A part's
    // names are the same whichever page reaches it, so they are kept, but only when they were read whole: a read
    // that ran into a type being read above it left that way out.
    private static IReadOnlyList<Relative> Walk(Type type, bool isPage)
    {
        if (!isPage && cache!.TryGetValue(type, out var known))
        {
            return known;
        }

        var stack = reading ??= [];
        var depth = stack.Count;
        var metBefore = depth == 0 ? int.MaxValue : shallowestMet;
        shallowestMet = int.MaxValue;
        stack.Add(type);
        List<Relative> names = [];
        try
        {
            Collect(type, isPage, names, stack);
        }
        finally
        {
            stack.RemoveAt(depth);
        }

        var whole = shallowestMet >= depth;
        shallowestMet = Math.Min(metBefore, shallowestMet);
        if (whole && !isPage)
        {
            cache![type] = names;
        }

        return names;
    }

    private static void Collect(Type type, bool isPage, List<Relative> names, List<Type> stack)
    {
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => !method.IsSpecialName))
        {
            foreach (var attribute in method.GetCustomAttributes<BddControlAttribute>(inherit: true))
            {
                names.Add(new Relative(attribute.Name, [], method, KindOf(method.ReturnType), Declared: true));
            }
        }

        // A property a type declares again over its base's is one property: the type's own.
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).GroupBy(property => property.Name, StringComparer.Ordinal).Select(MostDerived))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null || property.GetCustomAttribute<BddIgnoreAttribute>() is not null)
            {
                continue;
            }

            var kind = KindOf(property.PropertyType);
            var declared = property.GetCustomAttributes<BddControlAttribute>(inherit: true).Select(attribute => attribute.Name).ToList();
            foreach (var name in declared)
            {
                // A page's own declared control is found by the page; it is here too when its kind adds ways to say it.
                if (!isPage || kind is not null)
                {
                    names.Add(new Relative(name, [], property, kind, Declared: true));
                }
            }

            if (declared.Count == 0 && kind is not null)
            {
                names.Add(new Relative(FromMember(property.Name), [], property, kind, Declared: false));
            }

            if (!IsPart(property.PropertyType))
            {
                continue;
            }

            var met = stack.IndexOf(property.PropertyType);
            if (met >= 0)
            {
                shallowestMet = Math.Min(shallowestMet, met);
                continue;
            }

            if (stack.Count > Deepest)
            {
                shallowestMet = -1; // cut short by depth, not by a circle: whole for nobody
                continue;
            }

            foreach (var inner in Walk(property.PropertyType, isPage: false).Where(inner => inner.Path.Count < Deepest))
            {
                names.Add(inner with { Path = [property, .. inner.Path] });
            }
        }
    }

    // A part is an object of the suite's or a library's own: not a text, a number, a list, or anything of the runtime's.
    // Another page a page can give (the page a tab opens) is not a part of it: its controls are that page's.
    private static bool IsPart(Type type)
        => suitePages?.Contains(type) != true && !TestPages.IsPage(type) && type.IsClass && type != typeof(string) && !type.IsArray && !typeof(Delegate).IsAssignableFrom(type) &&
           !typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
           type.Namespace is { } space && !space.StartsWith("System", StringComparison.Ordinal) && !space.StartsWith("Microsoft", StringComparison.Ordinal);

    private static PropertyInfo MostDerived(IEnumerable<PropertyInfo> same)
        => same.Aggregate((best, next) => next.DeclaringType is { } declaring && best.DeclaringType is { } current && declaring.IsSubclassOf(current) ? next : best);

    private static string? KindOf(Type type) => type.GetCustomAttribute<BddKindAttribute>(inherit: true)?.Kind;

    private static Type TypeOf(MemberInfo member) => member switch
    {
        MethodInfo method => method.ReturnType,
        PropertyInfo property => property.PropertyType,
        _ => typeof(object),
    };

    // What a part is called: its declared fixed name, else its member's.
    private static string NameOf(PropertyInfo part)
        => part.GetCustomAttributes<BddControlAttribute>(inherit: true).Select(attribute => attribute.Name).FirstOrDefault(name => !name.Contains('{', StringComparison.Ordinal))
           ?? FromMember(part.Name);

    /// <summary>A member's name as words, as a label is written: <c>HourlyRate</c> is <c>Hourly rate</c>.</summary>
    internal static string FromMember(string identifier) => StepNames.LabelFromMember(identifier);

    // "the filter above {column}" → ^(?:the )?filter above (?<column>.+?)$, any case; a leading "the" may be left out.
    // With a kind, its word may stand before or after the name: (?:the )?(?:field )?Name(?: field)?.
    private static Regex ToPattern(string phrase, string? kind)
    {
        var words = Placeholder().Replace(Regex.Escape(StripThe(phrase.Trim())).Replace("\\ ", "\\s+", StringComparison.Ordinal), match => $"(?<{match.Groups[1].Value}>.+?)");
        if (kind is not null)
        {
            var word = Regex.Escape(kind).Replace("\\ ", "\\s+", StringComparison.Ordinal);
            words = $"(?:{word}\\s+)?{words}(?:\\s+{word})?";
        }

        return new Regex($"^(?:the\\s+)?{words}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string StripThe(string phrase)
        => phrase.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? phrase[4..].TrimStart() : phrase;

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    [GeneratedRegex(@"\\\{([A-Za-z][A-Za-z0-9_-]*)}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"\s+(?:in|under)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlaceWord();
}
