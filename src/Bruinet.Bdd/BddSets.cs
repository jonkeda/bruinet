using System.Reflection;
using System.Text;
using System.Globalization;

namespace Bruinet.Bdd;

/// <summary>
/// Compares what a step expected with what it found, item by item on their public properties, and
/// fails with one diff: the missing items, the extra ones, and for each missing one the closest
/// found item's different cells. For a step table against objects, use
/// <see cref="BddTableBinding.AssertExactly{T}"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public static class BddSets
{
    /// <summary>The two lists hold the same items, in any order unless <paramref name="ordered"/>.</summary>
    /// <param name="expected">What the step expected, such as a table bound to records.</param>
    /// <param name="actual">What it found.</param>
    /// <param name="ordered">Whether the order counts: then item <c>i</c> is compared with item <c>i</c>.</param>
    /// <param name="what">What the items are, for the message: <c>requests</c>.</param>
    /// <exception cref="InvalidOperationException">They differ; the message is the diff.</exception>
    public static void AssertSet<T>(IEnumerable<T> expected, IEnumerable<T> actual, bool ordered = false, string? what = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var wanted = expected.ToList();
        var found = actual.ToList();
        var columns = Columns<T>();
        HashSet<int> missing = [];
        HashSet<int> extra = [];

        if (ordered)
        {
            for (var i = 0; i < Math.Max(wanted.Count, found.Count); i++)
            {
                if (i >= found.Count || i >= wanted.Count || !Same(wanted[i], found[i], columns))
                {
                    if (i < wanted.Count)
                    {
                        missing.Add(i);
                    }

                    if (i < found.Count)
                    {
                        extra.Add(i);
                    }
                }
            }
        }
        else
        {
            var unused = Enumerable.Range(0, found.Count).ToList();
            for (var i = 0; i < wanted.Count; i++)
            {
                var match = unused.FindIndex(index => Same(wanted[i], found[index], columns));
                if (match >= 0)
                {
                    unused.RemoveAt(match);
                }
                else
                {
                    missing.Add(i);
                }
            }

            extra.UnionWith(unused);
        }

        if (missing.Count == 0 && extra.Count == 0)
        {
            return;
        }

        var name = what ?? typeof(T).Name;
        var headers = columns.Select(column => column.Name).ToList();
        var message = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"The {name} differ{(ordered ? " (in order)" : string.Empty)}: {missing.Count} missing, {extra.Count} extra.")
            .AppendLine(BddTableBinding.Format("expected", headers, wanted.Select(item => Cells(item, columns)), i => missing.Contains(i) ? "<- missing" + Closest(wanted[i], found, columns, ordered ? i : null) : null))
            .Append(BddTableBinding.Format("actual", headers, found.Select(item => Cells(item, columns)), i => extra.Contains(i) ? "<- extra" : null));
        throw new InvalidOperationException(message.ToString());
    }

    // A string or number is its own one column; a record or class is its public properties.
    private static IReadOnlyList<Column> Columns<T>()
    {
        var type = typeof(T);
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type.IsEnum || Nullable.GetUnderlyingType(type) is not null)
        {
            return [new Column("Value", item => item)];
        }

        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(property => new Column(property.Name, item => property.GetValue(item)))
            .ToList();
    }

    private static bool Same<T>(T expected, T actual, IReadOnlyList<Column> columns)
        => columns.All(column => Equals(column.Read(expected), column.Read(actual)));

    private static string Closest<T>(T expected, List<T> found, IReadOnlyList<Column> columns, int? at)
    {
        var candidates = at is { } index ? found.Skip(index).Take(1) : found;
        var best = candidates
            .Select(item => (Item: item, Differences: columns.Where(column => !Equals(column.Read(expected), column.Read(item))).ToList()))
            .Where(candidate => candidate.Differences.Count < columns.Count)
            .OrderBy(candidate => candidate.Differences.Count)
            .FirstOrDefault();

        return best.Differences is null || best.Differences.Count == 0
            ? string.Empty
            : $" ({(at is null ? "closest " : string.Empty)}differs in {string.Join(", ", best.Differences.Select(column => $"{column.Name}: {BddValues.Write(column.Read(best.Item))}"))})";
    }

    private static List<string> Cells<T>(T item, IReadOnlyList<Column> columns)
        => columns.Select(column => BddValues.Write(column.Read(item))).ToList();

    private sealed record Column(string Name, Func<object?, object?> Read);
}
