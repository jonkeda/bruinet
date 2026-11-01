using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Bdd;

/// <summary>What a value is read against: the clock for relative dates, the users, the app's own types.</summary>
/// <param name="Now">What <c>today</c> and <c>+2d</c> resolve against.</param>
/// <param name="Apps">The <c>Apps</c> table, for <see cref="BddUserRole"/> values.</param>
/// <param name="Types">The app's argument types; none when null.</param>
/// <param name="Services">The scenario's services, for converters that need them; none at bind time.</param>
public sealed record BddValueContext(
    DateTimeOffset Now,
    IReadOnlyList<BddAppConfig> Apps,
    BddArgumentTypes? Types = null,
    IServiceProvider? Services = null)
{
    /// <summary>A context with the current time, no users and no app types.</summary>
    public static BddValueContext Default => new(DateTimeOffset.UtcNow, []);

    /// <summary>
    /// While a callable phrase's body is bound to learn its types: a value that is exactly a
    /// placeholder (<c>&lt;hours&gt;</c>) is not read, but reported here with the type it would be
    /// read as. Null otherwise.
    /// </summary>
    public Action<string, Type>? Placeholders { get; init; }
}

/// <summary>Why a value could not be read.</summary>
/// <param name="Code">The diagnostic code, <c>UATA001</c>-<c>UATA005</c>.</param>
/// <param name="Message">What is wrong and how to write it.</param>
/// <param name="Fix">The value rewritten, when there is exactly one right answer.</param>
/// <param name="Original">The value as written, when there is a <paramref name="Fix"/>.</param>
public sealed record BddValueProblem(string Code, string Message, string? Fix = null, string? Original = null);

/// <summary>A value that does not fit its type.</summary>
public sealed class BddValueException(BddValueProblem problem) : FormatException(problem.Message)
{
    /// <summary>The problem.</summary>
    public BddValueProblem Problem { get; } = problem;
}

/// <summary>
/// The one reader and writer of values in scenario files: step arguments, Examples cells and table
/// cells. Invariant whatever the machine culture: dates <c>12-May-2026</c>, numbers <c>1,250.75</c>.
/// See AD-016.
/// </summary>
public static partial class BddValues
{
    private static readonly string[] Months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>Reads <paramref name="text"/> as <paramref name="type"/>.</summary>
    /// <param name="text">The value as written.</param>
    /// <param name="type">The parameter, field or element type.</param>
    /// <param name="context">The clock, the users and the app's types.</param>
    /// <param name="value">The value, when it fits.</param>
    /// <param name="problem">Why it does not fit, otherwise.</param>
    /// <param name="allowNull">Whether <c>null</c> or an empty cell reads as null for a reference type.</param>
    public static bool TryRead(string text, Type type, BddValueContext context, out object? value, out BddValueProblem? problem, bool allowNull = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(context);

        value = null;
        problem = null;
        var underlying = Nullable.GetUnderlyingType(type);
        var target = underlying ?? type;
        var trimmed = text.Trim();

        if (context.Placeholders is { } placeholders && BddOutline.WholePlaceholder(trimmed) is { } placeholder)
        {
            placeholders(placeholder, type);
            return true;
        }

        if ((underlying is not null || (allowNull && !type.IsValueType)) && (trimmed.Length == 0 || trimmed == "null"))
        {
            return true;
        }

        var types = context.Types ?? BddArgumentTypes.Empty;
        if (types.IsRegistered(target))
        {
            return types.TryRead(Unquote(trimmed), target, context.Services, out value, out problem);
        }

        if (ElementType(target) is { } element)
        {
            return TryReadList(trimmed, target, element, context, out value, out problem);
        }

        problem = target switch
        {
            _ when target == typeof(string) => Set(Unquote(trimmed), out value),
            _ when target == typeof(bool) => ReadBool(trimmed, out value),
            _ when target.IsEnum => ReadEnum(Unquote(trimmed), target, out value),
            _ when IsInteger(target) => ReadNumber(trimmed, target, integer: true, out value),
            _ when IsDecimal(target) => ReadNumber(trimmed, target, integer: false, out value),
            _ when target == typeof(DateOnly) || target == typeof(DateTime) || target == typeof(DateTimeOffset) => ReadDate(trimmed, target, context.Now, out value),
            _ when target == typeof(TimeOnly) => ReadTime(trimmed, out value),
            _ when target == typeof(TimeSpan) => ReadDuration(trimmed, out value),
            _ when target == typeof(Guid) => Guid.TryParse(Unquote(trimmed), out var guid) ? Set(guid, out value) : NotA(trimmed, target),
            _ when target == typeof(BddUserRole) => ReadUser(Unquote(trimmed), context, out value),
            _ when ParseMethod(target) is { } parse => ReadParsable(Unquote(trimmed), target, parse, out value),
            _ => new BddValueProblem("UATA001", $"'{trimmed}' cannot be written in a scenario: {Describe(target)} has no inline form. Use a table, or register it with [BddArgumentType]."),
        };

        if (problem?.Fix is not null)
        {
            problem = problem with { Original = trimmed };
        }

        return problem is null;
    }

    /// <summary>Reads <paramref name="text"/> as <paramref name="type"/>, or throws.</summary>
    /// <exception cref="BddValueException">The value does not fit.</exception>
    public static object? Read(string text, Type type, BddValueContext context, bool allowNull = false)
        => TryRead(text, type, context, out var value, out var problem, allowNull) ? value : throw new BddValueException(problem!);

    /// <summary>Whether a value of <paramref name="type"/> can be written on the step line or in a cell.</summary>
    public static bool IsInline(Type type, BddArgumentTypes? types = null)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if ((types ?? BddArgumentTypes.Empty).IsRegistered(target))
        {
            return true;
        }

        if (ElementType(target) is { } element)
        {
            return ElementType(element) is null && IsInline(element, types);
        }

        return target == typeof(string) || target == typeof(bool) || target.IsEnum || IsInteger(target) || IsDecimal(target)
            || target == typeof(DateOnly) || target == typeof(DateTime) || target == typeof(DateTimeOffset)
            || target == typeof(TimeOnly) || target == typeof(TimeSpan) || target == typeof(Guid) || target == typeof(BddUserRole)
            || ParseMethod(target) is not null;
    }

    /// <summary>The element type of an array or list type; null for anything else, including <see cref="string"/>.</summary>
    public static Type? ElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(IReadOnlyList<>) ||
             definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IReadOnlyCollection<>)))
        {
            return type.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>A short name for the kind of value a type takes, as the phrase list shows it.</summary>
    public static string Describe(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (ElementType(target) is { } element)
        {
            return "list of " + Describe(element);
        }

        return target switch
        {
            _ when target == typeof(string) => "text",
            _ when target == typeof(bool) => "true/false",
            _ when target.IsEnum => string.Join("/", Enum.GetNames(target)),
            _ when IsInteger(target) => "whole number",
            _ when IsDecimal(target) => "number",
            _ when target == typeof(DateOnly) => "date",
            _ when target == typeof(DateTime) || target == typeof(DateTimeOffset) => "date and time",
            _ when target == typeof(TimeOnly) => "time",
            _ when target == typeof(TimeSpan) => "duration",
            _ when target == typeof(BddUserRole) => "user",
            _ => target.Name,
        };
    }

    /// <summary>A value as a scenario writes it, so a message can be pasted back into a scenario.</summary>
    public static string Write(object? value) => value switch
    {
        null => "null",
        "" => "\"\"",
        // Quoted too when it is in quotes itself: Read would take them for the scenario's.
        string text when string.IsNullOrWhiteSpace(text) || text != text.Trim() || (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            => $"\"{text.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        string text => text,
        bool boolean => boolean ? "true" : "false",
        DateOnly date => WriteDate(date),
        DateTime time => time.TimeOfDay == TimeSpan.Zero ? WriteDate(DateOnly.FromDateTime(time)) : WriteDate(DateOnly.FromDateTime(time)) + " " + WriteTime(TimeOnly.FromDateTime(time)),
        DateTimeOffset time => WriteDate(DateOnly.FromDateTime(time.DateTime)) + " " + WriteTime(TimeOnly.FromDateTime(time.DateTime)),
        TimeOnly time => WriteTime(time),
        TimeSpan duration => WriteDuration(duration),
        decimal or double or float => ((IFormattable)value).ToString("#,0.#############################", CultureInfo.InvariantCulture),
        byte or sbyte or short or ushort or int or uint or long or ulong => ((IFormattable)value).ToString("#,0", CultureInfo.InvariantCulture),
        BddUserRole user => user.Name,
        IEnumerable items and not string => string.Join(", ", items.Cast<object?>().Select(Write)),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>Whether <paramref name="text"/> is a date relative to the clock: <c>today</c>, <c>tomorrow</c>, <c>yesterday</c>, <c>+2d</c>.</summary>
    public static bool IsRelativeDate(string text) => RelativeDays(text.Trim()) is not null;

    /// <summary>Removes surrounding double quotes and unescapes <c>\"</c> inside them.</summary>
    public static string Unquote(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
            : trimmed;
    }

    /// <summary>Splits an inline list: items separated by <c>, </c> and a last <c>and</c>, outside quotes.</summary>
    public static IReadOnlyList<string> SplitList(string text)
    {
        List<string> items = [];
        var current = new StringBuilder();
        var quoted = false;
        var trimmed = text.Trim();

        for (var i = 0; i < trimmed.Length; i++)
        {
            var character = trimmed[i];
            if (character == '"' && (i == 0 || trimmed[i - 1] != '\\'))
            {
                quoted = !quoted;
            }

            if (!quoted && character == ',' && i + 1 < trimmed.Length && char.IsWhiteSpace(trimmed[i + 1]))
            {
                Flush();
                i = SkipWhitespace(trimmed, i + 1) - 1;
                if (StartsWithWord(trimmed, i + 1, "and"))
                {
                    i += "and".Length;
                }

                continue;
            }

            if (!quoted && char.IsWhiteSpace(character) && StartsWithWord(trimmed, SkipWhitespace(trimmed, i), "and"))
            {
                Flush();
                i = SkipWhitespace(trimmed, i) + "and".Length;
                continue;
            }

            current.Append(character);
        }

        Flush();
        return items;

        void Flush()
        {
            if (current.ToString().Trim() is { Length: > 0 } item)
            {
                items.Add(item);
            }

            current.Clear();
        }
    }

    private static bool TryReadList(string text, Type listType, Type element, BddValueContext context, out object? value, out BddValueProblem? problem)
    {
        value = null;
        problem = null;
        var items = SplitList(text);
        var array = Array.CreateInstance(element, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            if (!TryRead(items[i], element, context, out var item, out var itemProblem))
            {
                problem = itemProblem! with { Message = $"item {i + 1} of the list: {itemProblem!.Message}", Fix = null };
                return false;
            }

            array.SetValue(item, i);
        }

        value = listType.IsArray || listType.IsInterface
            ? array
            : Activator.CreateInstance(listType, array);
        return true;
    }

    private static BddValueProblem? Set(object? read, out object? value)
    {
        value = read;
        return null;
    }

    private static BddValueProblem NotA(string text, Type type)
        => new("UATA001", $"'{text}' is not a {Describe(type)}.");

    private static BddValueProblem? ReadBool(string text, out object? value)
    {
        value = null;
        return text.ToLowerInvariant() switch
        {
            // on/off is how a switch is read aloud (a user's "Receives reminders | off").
            "true" or "on" => Set(true, out value),
            "false" or "off" => Set(false, out value),
            _ => new BddValueProblem("UATA001", $"'{text}' is not true or false. Write true or false (on or off for a switch); a phrase says yes or no in its words."),
        };
    }

    private static BddValueProblem? ReadEnum(string text, Type type, out object? value)
    {
        value = null;
        var key = Key(text);
        var names = Enum.GetNames(type);
        var name = names.FirstOrDefault(candidate => Key(candidate) == key);
        if (name is not null)
        {
            return Set(Enum.Parse(type, name), out value);
        }

        var closest = names.OrderBy(candidate => Distance(key, Key(candidate))).FirstOrDefault(candidate => Distance(key, Key(candidate)) <= 2);
        return new BddValueProblem(
            "UATA004",
            $"'{text}' is not a {type.Name}." + (closest is null ? string.Empty : $" Did you mean {closest}?") + $" It is one of {string.Join(", ", names)}.");

        static string Key(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static BddValueProblem? ReadNumber(string text, Type type, bool integer, out object? value)
    {
        value = null;
        var valid = integer ? IntegerRegex().IsMatch(text) : DecimalRegex().IsMatch(text);
        if (!valid)
        {
            if (CommaDecimalRegex().IsMatch(text))
            {
                var fix = text.Replace(".", "\u0001", StringComparison.Ordinal).Replace(',', '.').Replace("\u0001", ",", StringComparison.Ordinal);
                return new BddValueProblem("UATA003", $"'{text}' uses a comma for decimals. Write {fix}: '.' for decimals and ',' for thousands.");
            }

            if (DigitsRegex().IsMatch(text))
            {
                return new BddValueProblem(
                    "UATA003",
                    integer && text.Contains('.', StringComparison.Ordinal)
                        ? $"'{text}' is not a whole number."
                        : $"'{text}' groups its digits wrongly. Write '.' for decimals and ',' between groups of three digits, e.g. 1,250.75.");
            }

            return NotA(text, type);
        }

        try
        {
            var plain = text.Replace(",", string.Empty, StringComparison.Ordinal);
            return Set(System.Convert.ChangeType(decimal.Parse(plain, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture), out value);
        }
        catch (OverflowException)
        {
            return new BddValueProblem("UATA001", $"'{text}' is too large for a {type.Name}.");
        }
    }

    private static BddValueProblem? ReadDate(string text, Type type, DateTimeOffset now, out object? value)
    {
        value = null;
        if (RelativeDays(text) is { } days)
        {
            return Set(
                type == typeof(DateOnly) ? DateOnly.FromDateTime(now.UtcDateTime).AddDays(days)
                : type == typeof(DateTimeOffset) ? (object)now.AddDays(days)
                : now.UtcDateTime.AddDays(days),
                out value);
        }

        var match = DateRegex().Match(text);
        if (!match.Success)
        {
            return DateProblem(text, type);
        }

        var month = Array.FindIndex(Months, candidate => candidate.Equals(match.Groups["month"].Value, StringComparison.OrdinalIgnoreCase)) + 1;
        if (month == 0)
        {
            return new BddValueProblem(
                "UATA002",
                $"'{text}': '{match.Groups["month"].Value}' is not an English month. Write the month as {string.Join(", ", Months)}, e.g. 12-May-2026.",
                FullMonth(match.Groups["month"].Value) is { } short3 ? $"{match.Groups["day"].Value}-{short3}-{match.Groups["year"].Value}" : null);
        }

        var hasTime = match.Groups["hour"].Success;
        if (hasTime && type == typeof(DateOnly))
        {
            return new BddValueProblem("UATA002", $"'{text}' has a time, but the value is a date. Write only the date, e.g. 12-May-2026.");
        }

        try
        {
            var date = new DateOnly(int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture), month, int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture));
            var time = hasTime
                ? new TimeOnly(int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture), match.Groups["second"].Success ? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture) : 0)
                : TimeOnly.MinValue;
            var dateTime = date.ToDateTime(time);
            return Set(
                type == typeof(DateOnly) ? date
                : type == typeof(DateTimeOffset) ? (object)new DateTimeOffset(dateTime, now.Offset)
                : dateTime,
                out value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new BddValueProblem("UATA002", $"'{text}' is not a date that exists.");
        }
    }

    private static BddValueProblem DateProblem(string text, Type type)
    {
        var example = type == typeof(DateOnly) ? "12-May-2026" : "12-May-2026 14:30";
        var iso = IsoDateRegex().Match(text);
        if (iso.Success &&
            int.TryParse(iso.Groups["month"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var month) && month is >= 1 and <= 12)
        {
            var fix = $"{iso.Groups["day"].Value}-{Months[month - 1]}-{iso.Groups["year"].Value}"
                + (iso.Groups["time"].Success && type != typeof(DateOnly) ? " " + iso.Groups["time"].Value : string.Empty);
            return new BddValueProblem("UATA002", $"'{text}' is not written as dd-MMM-yyyy. Write {fix}.", fix);
        }

        return new BddValueProblem("UATA002", $"'{text}' is not a {Describe(type)}. Write it as dd-MMM-yyyy{(type == typeof(DateOnly) ? string.Empty : " HH:mm")}, e.g. {example}, or relative: today, +2d, -1d.");
    }

    private static string? FullMonth(string month)
        => Months.FirstOrDefault(candidate => month.Length > 3 && month.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

    private static BddValueProblem? ReadTime(string text, out object? value)
    {
        value = null;
        var match = TimeRegex().Match(text);
        if (!match.Success)
        {
            return new BddValueProblem("UATA002", $"'{text}' is not a time. Write it as HH:mm, e.g. 14:30.");
        }

        try
        {
            return Set(new TimeOnly(int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture), match.Groups["second"].Success ? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture) : 0), out value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new BddValueProblem("UATA002", $"'{text}' is not a time that exists.");
        }
    }

    private static BddValueProblem? ReadDuration(string text, out object? value)
    {
        value = null;
        var match = DurationRegex().Match(text);
        if (!match.Success || text.Length == 0)
        {
            return new BddValueProblem("UATA001", $"'{text}' is not a duration. Write it in d, h, m and s, largest first, e.g. 90m or 1h 30m.");
        }

        double Part(string name) => match.Groups[name].Success ? double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : 0;
        return Set(TimeSpan.FromDays(Part("d")) + TimeSpan.FromHours(Part("h")) + TimeSpan.FromMinutes(Part("m")) + TimeSpan.FromSeconds(Part("s")), out value);
    }

    private static BddValueProblem? ReadUser(string text, BddValueContext context, out object? value)
    {
        value = null;
        if (context.Apps.Count == 0)
        {
            return new BddValueProblem("UATA005", $"'{text}' is a user, but the config has no Apps table to say who the users are.");
        }

        var byApp = context.Apps.FirstOrDefault(app => app.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
        var user = byApp?.User ?? context.Apps.Select(app => app.User).FirstOrDefault(name => name.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            var known = context.Apps.Select(app => app.User).Distinct(StringComparer.OrdinalIgnoreCase);
            return new BddValueProblem("UATA005", $"'{text}' is not a user in the Apps table. It has: {string.Join(", ", known)}.");
        }

        return Set(BddUserRole.Of(user, context.Apps), out value);
    }

    private static System.Reflection.MethodInfo? ParseMethod(Type type)
        => type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IParsable<>))
            ? type.GetMethod("Parse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(string), typeof(IFormatProvider)])
            : null;

    private static BddValueProblem? ReadParsable(string text, Type type, System.Reflection.MethodInfo parse, out object? value)
    {
        value = null;
        try
        {
            return Set(parse.Invoke(null, [text, CultureInfo.InvariantCulture]), out value);
        }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is FormatException or ArgumentException or OverflowException)
        {
            return new BddValueProblem("UATA001", $"'{text}' is not a {type.Name}: {exception.InnerException.Message}");
        }
    }

    private static bool IsInteger(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
           || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);

    private static bool IsDecimal(Type type) => type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    private static int? RelativeDays(string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "today":
                return 0;
            case "tomorrow":
                return 1;
            case "yesterday":
                return -1;
        }

        var match = RelativeDaysRegex().Match(text);
        return match.Success ? int.Parse(match.Groups["days"].Value, CultureInfo.InvariantCulture) * (match.Groups["sign"].Value == "-" ? -1 : 1) : null;
    }

    private static string WriteDate(DateOnly date) => $"{date.Day:00}-{Months[date.Month - 1]}-{date.Year:0000}";

    private static string WriteTime(TimeOnly time) => time.Second == 0 ? $"{time.Hour:00}:{time.Minute:00}" : $"{time.Hour:00}:{time.Minute:00}:{time.Second:00}";

    private static string WriteDuration(TimeSpan duration)
    {
        List<string> parts = [];
        if (duration.Days > 0)
        {
            parts.Add($"{duration.Days}d");
        }

        if (duration.Hours > 0)
        {
            parts.Add($"{duration.Hours}h");
        }

        if (duration.Minutes > 0)
        {
            parts.Add($"{duration.Minutes}m");
        }

        if (duration.Seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{duration.Seconds}s");
        }

        return string.Join(' ', parts);
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool StartsWithWord(string text, int index, string word)
        => index > 0 && char.IsWhiteSpace(text[index - 1]) &&
           index + word.Length < text.Length &&
           string.CompareOrdinal(text, index, word, 0, word.Length) == 0 &&
           char.IsWhiteSpace(text[index + word.Length]);

    internal static int Distance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            }

            previous = current;
        }

        return previous[b.Length];
    }

    [GeneratedRegex(@"^-?(\d{1,3}(,\d{3})+|\d+)$")]
    private static partial Regex IntegerRegex();

    [GeneratedRegex(@"^-?(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$")]
    private static partial Regex DecimalRegex();

    [GeneratedRegex(@"^-?(\d{1,3}(\.\d{3})*|\d+),\d+$")]
    private static partial Regex CommaDecimalRegex();

    [GeneratedRegex(@"^-?[\d.,]+$")]
    private static partial Regex DigitsRegex();

    [GeneratedRegex(@"^(?<day>\d{1,2})-(?<month>[A-Za-z]+)-(?<year>\d{4})(?:\s+(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?)?$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})(?:[T ](?<time>\d{2}:\d{2}))?")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"^(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?$")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"^(?:(?<d>\d+)d)?\s*(?:(?<h>\d+)h)?\s*(?:(?<m>\d+)m)?\s*(?:(?<s>\d+)s)?$")]
    private static partial Regex DurationRegex();

    [GeneratedRegex("^(?<sign>[+-])(?<days>\\d+)d$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeDaysRegex();
}
