using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Bruinet.Bdd;

/// <summary>
/// Step tables as typed rows: binding a table to records, and checking real rows against a table.
/// </summary>
/// <remarks>
/// <para>
/// A column is a constructor parameter (for <see cref="Bind{T}"/>) or a property (for the
/// assertions) of <c>T</c>, matched without case and spaces: <c>Due Date</c> is <c>DueDate</c>.
/// An unknown column is an error naming the known ones, never a column that is quietly ignored.
/// </para>
/// <para>
/// Cells are read by <see cref="BddValues"/>: dates <c>12-May-2026</c>, numbers <c>1,250.75</c>. An
/// empty cell or <c>null</c> is <c>null</c> where the type allows it. Dates may be relative to the
/// test data's pinned now: <c>today</c>, <c>+2d</c>, <c>-1d</c>.
/// </para>
/// </remarks>
public static partial class BddTableBinding
{
    /// <summary>One <typeparamref name="T"/> per row, built through its widest public constructor.</summary>
    /// <param name="table">The step table.</param>
    /// <param name="now">What relative dates are resolved against.</param>
    /// <exception cref="InvalidOperationException">An unknown column, a missing required column, or a cell that does not fit.</exception>
    public static IReadOnlyList<T> Bind<T>(this BddTable table, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        return BindRows(table, table.Rows, typeof(T), Context(now), now is null).Cast<T>().ToList();
    }

    /// <summary>
    /// The table as a phrase parameter of <paramref name="type"/>: a list of records (one per row),
    /// one record (a <c>Field | Value</c> table, or a table with one row), or a list of values (a
    /// table with one column).
    /// </summary>
    /// <param name="table">The step table.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="context">What cells are read against.</param>
    /// <exception cref="InvalidOperationException">The table does not fit the type; the message names the row and column.</exception>
    public static object Bind(this BddTable table, Type type, BddValueContext context)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(context);

        var problems = Check(table, type, context);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, problems.Select(p => $"{p.Location}: {p.Problem.Message}")));
        }

        var element = BddValues.ElementType(type);
        if (element is not null && BddValues.IsInline(element, context.Types))
        {
            var column = table.Columns[0];
            var values = Array.CreateInstance(element, table.Rows.Count);
            for (var i = 0; i < table.Rows.Count; i++)
            {
                values.SetValue(BddValues.Read(table.Rows[i].Cells[column], element, context), i);
            }

            return AsList(type, values);
        }

        if (element is not null)
        {
            var records = BindRows(table, table.Rows, element, context, strictNow: false);
            var array = Array.CreateInstance(element, records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                array.SetValue(records[i], i);
            }

            return AsList(type, array);
        }

        var source = IsVertical(table) ? Vertical(table) : table;
        return BindRows(source, source.Rows, type, context, strictNow: false)[0];
    }

    /// <summary>What is wrong with <paramref name="table"/> as a parameter of <paramref name="type"/>, at the row it is in.</summary>
    /// <param name="table">The step table.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="context">What cells are read against.</param>
    public static IReadOnlyList<(BddValueProblem Problem, BddSourceLocation Location)> Check(BddTable table, Type type, BddValueContext context)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(context);

        List<(BddValueProblem, BddSourceLocation)> problems = [];
        var element = BddValues.ElementType(type);
        if (element is not null && BddValues.IsInline(element, context.Types))
        {
            if (table.Columns.Count != 1)
            {
                problems.Add((new BddValueProblem("UATA006", $"a list of {BddValues.Describe(element)} takes a table with one column, not {table.Columns.Count}."), table.Source));
                return problems;
            }

            foreach (var row in table.Rows)
            {
                if (!BddValues.TryRead(row.Cells[table.Columns[0]], element, context, out _, out var problem))
                {
                    problems.Add((problem!, row.Source));
                }
            }

            return problems;
        }

        var recordType = element ?? type;
        var source = element is null && IsVertical(table) ? Vertical(table) : table;
        if (element is null && source.Rows.Count != 1)
        {
            problems.Add((new BddValueProblem("UATA006", $"one {recordType.Name} takes a 'Field | Value' table or a table with one row; this one has {table.Rows.Count} rows."), table.Source));
            return problems;
        }

        var constructor = WidestConstructor(recordType);
        if (constructor is null)
        {
            problems.Add((new BddValueProblem("UATA006", $"{recordType.Name} has no public constructor to bind a table to."), table.Source));
            return problems;
        }

        var parameters = constructor.GetParameters();
        Dictionary<string, string> byColumn;
        try
        {
            byColumn = MapColumns(source, parameters.Select(p => p.Name!).ToList(), recordType);
        }
        catch (InvalidOperationException exception)
        {
            problems.Add((new BddValueProblem("UATA006", StripSource(source, exception.Message)), source.Source));
            return problems;
        }

        var missing = parameters.Where(p => !p.HasDefaultValue && !byColumn.Values.Contains(p.Name!)).Select(p => Title(p.Name!)).ToList();
        if (missing.Count > 0)
        {
            problems.Add((new BddValueProblem("UATA006", $"the table needs column(s) {string.Join(", ", missing)} for {recordType.Name}."), source.Source));
            return problems;
        }

        var nullability = new NullabilityInfoContext();
        foreach (var row in source.Rows)
        {
            foreach (var (column, member) in byColumn)
            {
                var parameter = parameters.First(p => p.Name == member);
                if (!BddValues.TryRead(row.Cells[column], parameter.ParameterType, context, out _, out var problem, nullability.Create(parameter).WriteState == NullabilityState.Nullable))
                {
                    problems.Add((problem! with { Message = $"column '{column}': {problem!.Message}" }, row.Source));
                }
            }
        }

        return problems;
    }

    private static List<object> BindRows(BddTable table, IReadOnlyList<BddTableRow> rows, Type recordType, BddValueContext context, bool strictNow)
    {
        var constructor = WidestConstructor(recordType)
            ?? throw new InvalidOperationException($"{recordType.Name} has no public constructor to bind a table to.");
        var parameters = constructor.GetParameters();
        var byColumn = MapColumns(table, parameters.Select(p => p.Name!).ToList(), recordType);

        var missing = parameters
            .Where(p => !p.HasDefaultValue && !byColumn.Values.Contains(p.Name!))
            .Select(p => p.Name!)
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"{table.Source}: the table needs column(s) {string.Join(", ", missing.Select(Title))} for {recordType.Name}.");
        }

        var nullability = new NullabilityInfoContext();
        return rows.Select(row =>
        {
            var arguments = parameters.Select(parameter =>
            {
                var column = byColumn.FirstOrDefault(pair => pair.Value == parameter.Name).Key;
                return column is null
                    ? parameter.DefaultValue
                    : Convert(row.Cells[column], parameter.ParameterType, nullability.Create(parameter).WriteState == NullabilityState.Nullable, context, strictNow, row.Source, column);
            }).ToArray();

            return constructor.Invoke(arguments);
        }).ToList();
    }

    private static ConstructorInfo? WidestConstructor(Type type)
        => type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

    // A Field | Value table is one record, written top to bottom.
    private static bool IsVertical(BddTable table)
        => table.Columns.Count == 2 &&
           table.Columns[0].Equals("Field", StringComparison.OrdinalIgnoreCase) &&
           table.Columns[1].Equals("Value", StringComparison.OrdinalIgnoreCase);

    private static BddTable Vertical(BddTable table)
    {
        var columns = table.Rows.Select(row => row.Cells[table.Columns[0]]).ToList();
        var cells = table.Rows.ToDictionary(row => row.Cells[table.Columns[0]], row => row.Cells[table.Columns[1]], StringComparer.Ordinal);
        return new BddTable(columns, [new BddTableRow(cells, table.Rows.Count > 0 ? table.Rows[0].Source : table.Source)], table.Source);
    }

    private static object AsList(Type type, Array values)
        => type.IsArray || type.IsInterface ? values : Activator.CreateInstance(type, values)!;

    private static string StripSource(BddTable table, string message)
    {
        var prefix = $"{table.Source}: ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message[prefix.Length..] : message;
    }

    private static BddValueContext Context(DateTimeOffset? now) => new(now ?? DateTimeOffset.UtcNow, []);

    /// <summary>Every row of the table matches an item of <paramref name="actual"/>, on the listed columns.</summary>
    /// <exception cref="InvalidOperationException">A row has no match; the message shows the expected and actual rows.</exception>
    public static void AssertContains<T>(this BddTable table, IEnumerable<T> actual, DateTimeOffset? now = null)
        => Compare(table, actual, now, exactly: false);

    /// <summary>The rows of the table and the items of <paramref name="actual"/> match one to one, on the listed columns.</summary>
    /// <exception cref="InvalidOperationException">A row has no match, or an item is not in the table.</exception>
    public static void AssertExactly<T>(this BddTable table, IEnumerable<T> actual, DateTimeOffset? now = null)
        => Compare(table, actual, now, exactly: true);

    /// <summary>No item of <paramref name="actual"/> matches a row of the table, on the listed columns.</summary>
    /// <exception cref="InvalidOperationException">An item matches; the message shows it.</exception>
    public static void AssertDoesNotContain<T>(this BddTable table, IEnumerable<T> actual, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actual);

        var columns = PropertyColumns<T>(table);
        var expected = ExpectedRows(table, columns, now);
        var items = actual.ToList();
        var found = items.Where(item => expected.Any(row => Matches(row, item, columns))).ToList();
        if (found.Count > 0)
        {
            throw new InvalidOperationException(
                $"{table.Source}: expected no {typeof(T).Name} like these rows, but found {found.Count}." + Environment.NewLine +
                Format("found", table.Columns, found.Select(item => Cells(item, columns)), _ => null));
        }
    }

    private static void Compare<T>(BddTable table, IEnumerable<T> actual, DateTimeOffset? now, bool exactly)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actual);

        var columns = PropertyColumns<T>(table);
        var expected = ExpectedRows(table, columns, now);
        var items = actual.ToList();
        var unused = new List<T>(items);
        var missing = new List<int>();

        for (var i = 0; i < expected.Count; i++)
        {
            var match = unused.FindIndex(item => Matches(expected[i], item, columns));
            if (match >= 0)
            {
                unused.RemoveAt(match);
            }
            else
            {
                missing.Add(i);
            }
        }

        var extra = exactly ? unused : [];
        if (missing.Count == 0 && extra.Count == 0)
        {
            return;
        }

        var summary = new List<string>();
        if (missing.Count > 0)
        {
            summary.Add($"{missing.Count} row(s) missing");
        }

        if (extra.Count > 0)
        {
            summary.Add($"{extra.Count} extra");
        }

        var message = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"{table.Source}: {typeof(T).Name} rows do not match - {string.Join(", ", summary)}.")
            .AppendLine(Format("expected", table.Columns, table.Rows.Select(r => table.Columns.Select(c => r.Cells[c]).ToList()), i => missing.Contains(i) ? "<- missing" + Closest(expected[i], items, columns) : null))
            .Append(Format("actual", table.Columns, items.Select(item => Cells(item, columns)), i => extra.Contains(items[i]) ? "<- extra" : null));

        throw new InvalidOperationException(message.ToString());
    }

    private static List<PropertyColumn> PropertyColumns<T>(BddTable table)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead).ToList();
        var byColumn = MapColumns(table, properties.Select(p => p.Name).ToList(), typeof(T));
        var nullability = new NullabilityInfoContext();
        return table.Columns.Select(column =>
        {
            var property = properties.First(p => p.Name == byColumn[column]);
            return new PropertyColumn(column, property, nullability.Create(property).ReadState == NullabilityState.Nullable);
        }).ToList();
    }

    private static List<object?[]> ExpectedRows(BddTable table, List<PropertyColumn> columns, DateTimeOffset? now)
        => table.Rows
            .Select(row => columns
                .Select(c => Convert(row.Cells[c.Column], c.Property.PropertyType, c.Nullable, Context(now), now is null, row.Source, c.Column))
                .ToArray())
            .ToList();

    private static bool Matches<T>(object?[] expected, T item, List<PropertyColumn> columns)
        => columns.Select((column, index) => Equals(expected[index], column.Property.GetValue(item))).All(equal => equal);

    private static string Closest<T>(object?[] expected, List<T> items, List<PropertyColumn> columns)
    {
        var best = items
            .Select(item => (Item: item, Differences: columns.Where((column, index) => !Equals(expected[index], column.Property.GetValue(item))).ToList()))
            .Where(candidate => candidate.Differences.Count < columns.Count)
            .OrderBy(candidate => candidate.Differences.Count)
            .FirstOrDefault();

        return best.Item is null
            ? string.Empty
            : $" (closest differs in {string.Join(", ", best.Differences.Select(d => $"{d.Column}: {Show(d.Property.GetValue(best.Item))}"))})";
    }

    private static List<string> Cells<T>(T item, List<PropertyColumn> columns)
        => columns.Select(column => Show(column.Property.GetValue(item))).ToList();

    private static string Show(object? value) => BddValues.Write(value);

    internal static string Format(string label, IReadOnlyList<string> columns, IEnumerable<IReadOnlyList<string>> rows, Func<int, string?> note)
    {
        var all = rows.ToList();
        var widths = columns.Select((c, i) => Math.Max(c.Length, all.Count == 0 ? 0 : all.Max(r => r[i].Length))).ToList();
        string Line(IReadOnlyList<string> cells) => "    | " + string.Join(" | ", cells.Select((cell, i) => cell.PadRight(widths[i]))) + " |";

        var builder = new StringBuilder().AppendLine(CultureInfo.InvariantCulture, $"  {label}:").AppendLine(Line(columns));
        for (var i = 0; i < all.Count; i++)
        {
            var suffix = note(i) is { } text ? "   " + text : string.Empty;
            builder.AppendLine(Line(all[i]) + suffix);
        }

        if (all.Count == 0)
        {
            builder.AppendLine("    (none)");
        }

        return builder.ToString().TrimEnd();
    }

    private static Dictionary<string, string> MapColumns(BddTable table, IReadOnlyList<string> members, Type type)
    {
        var byKey = members.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var column in table.Columns)
        {
            if (!byKey.TryGetValue(Key(column), out var member))
            {
                var suggestion = members.OrderBy(m => Distance(Key(column), Key(m))).FirstOrDefault(m => Distance(Key(column), Key(m)) <= 2);
                throw new InvalidOperationException(
                    $"{table.Source}: column '{column}' is not a field of {type.Name}." +
                    (suggestion is null ? string.Empty : $" Did you mean '{Title(suggestion)}'?") +
                    $" Fields: {string.Join(", ", members.Select(Title))}.");
            }

            map[column] = member;
        }

        return map;
    }

    private static object? Convert(string cell, Type type, bool nullableReference, BddValueContext context, bool strictNow, BddSourceLocation source, string column)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (strictNow && BddValues.IsRelativeDate(cell) && (target == typeof(DateOnly) || target == typeof(DateTime) || target == typeof(DateTimeOffset)))
        {
            throw new InvalidOperationException($"{source}: column '{column}': '{cell.Trim()}' is a relative date, but no 'now' was given to resolve it against.");
        }

        return BddValues.TryRead(cell, type, context, out var value, out var problem, nullableReference)
            ? value
            : throw new InvalidOperationException($"{source}: column '{column}': {problem!.Message.TrimEnd('.')}.", new BddValueException(problem));
    }

    internal static string Key(string name) => name.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string Title(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    private static int Distance(string a, string b)
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

    private sealed record PropertyColumn(string Column, PropertyInfo Property, bool Nullable);
}
