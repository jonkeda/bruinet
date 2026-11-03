using System.Globalization;
using Bruinet.Bdd.Runner;

namespace Bruinet.Uat;

/// <summary>
/// The current page's controls and what each shows now, read for a paused Step-through: the toggle's state,
/// the range's value, the selector's choice or the text, and whether it is shown. Each read waits a moment
/// only, so a control that is not there costs little.
/// </summary>
internal static class UatPageState
{
    private const int ReadTimeoutMs = 200;

    private const int ValueKept = 200;

    // A control's interface, by the generic definition's name, to the member that reads what it shows.
    private static readonly (string Interface, string Member, bool TakesTimeout)[] Readers =
    [
        ("IToggleControlObject`1", "IsChecked", false),
        ("IRangeControlObject`1", "GetValue", true),
        ("ISelectorControlObject`1", "GetSelectedText", true),
        ("IEditableTextControlObject`1", "GetText", true),
        ("ITextControlObject`1", "GetText", true),
    ];

    /// <summary>The page's controls, or why there are none to read.</summary>
    /// <param name="page">The current page, null when none is.</param>
    /// <param name="instance">The page object.</param>
    /// <param name="most">The most controls read; the rest are left out, and the reason says so.</param>
    /// <param name="budget">How long the reads may take in all; the controls not reached by then are left out.</param>
    public static BddRunnerPageState Read(UatRuntimePage? page, object? instance, int most = int.MaxValue, TimeSpan? budget = null)
    {
        if (page is null || instance is null)
        {
            return new BddRunnerPageState(page?.Name, [], page is null ? "No page is current: no step has opened one yet." : $"The page '{page.Name}' has no object to read.");
        }

        List<BddRunnerControlState> controls = [];
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var bindings = page.Controls.ToList();
        foreach (var binding in bindings)
        {
            if (controls.Count >= most || clock.Elapsed > budget)
            {
                return new BddRunnerPageState(page.Name, controls, $"{controls.Count} of {bindings.Count} controls read: the rest left out ({(controls.Count >= most ? $"at most {most}" : $"after {budget!.Value.TotalSeconds:0} s")}).");
            }

            object? control;
            try
            {
                control = binding.Get(instance);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                controls.Add(new BddRunnerControlState(binding.Name, binding.Property.PropertyType.Name, null, null, Unreadable(exception)));
                continue;
            }

            if (control is null)
            {
                continue;
            }

            controls.Add(ReadControl(binding.Name, control));
        }

        return new BddRunnerPageState(page.Name, controls, null);
    }

    // The pages that say they are loaded, each asked for a moment only: what shows when no step made a page current
    // (a set_state's state starts the app but names no page).
    internal static List<UatRuntimePage> Loaded(IEnumerable<(UatRuntimePage Page, object? Instance)> pages)
    {
        List<UatRuntimePage> loaded = [];
        foreach (var (page, instance) in pages)
        {
            try
            {
                // IsLoaded(timeoutMs) on a MAUI page, IsLoaded() on a web one.
                if (instance is not null
                    && (Bruinet.Bdd.BddInvoker.TryInvokeBool(instance, "IsLoaded", ReadTimeoutMs, out var shown) || Bruinet.Bdd.BddInvoker.TryInvokeBool(instance, "IsLoaded", null, out shown))
                    && shown)
                {
                    loaded.Add(page);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
        }

        return loaded;
    }

    // rows: whether a collection's rows are read; not for a row's own controls, which may lead back to the list.
    private static BddRunnerControlState ReadControl(string name, object control, bool rows = true)
    {
        var type = control.GetType();
        var kind = Plain(type.Name);
        try
        {
            var shown = Invoke(control, "IsVisible", takesTimeout: false) as bool?;
            if (rows && shown != false && Rows(control) is { } shownRows)
            {
                return new BddRunnerControlState(name, kind, shown, shownRows, null);
            }

            var interfaces = type.GetInterfaces().Where(face => face.IsGenericType).Select(face => face.GetGenericTypeDefinition().Name).ToHashSet(StringComparer.Ordinal);
            var reader = Readers.FirstOrDefault(candidate => interfaces.Contains(candidate.Interface));
            // No Core interface reads it (Maui's Label): its own GetText, when it has one.
            var value = shown == false ? null
                : reader.Member is not null ? Text(Invoke(control, reader.Member, reader.TakesTimeout))
                : Text(Invoke(control, "GetText", takesTimeout: true) ?? Invoke(control, "GetText", takesTimeout: false));
            return new BddRunnerControlState(name, kind, shown, value, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new BddRunnerControlState(name, kind, null, null, Unreadable(exception));
        }
    }

    // A collection's rows, as its row objects' own controls show them: "3 rows: [Title 'Buy milk', Status 'Open'], ...";
    // ten rows and six controls a row at most. Null for a control that is no collection (no Items of row objects).
    internal static string? Rows(object control)
    {
        if (control.GetType().GetProperty("Items")?.GetValue(control) is not System.Collections.IEnumerable items || items is string)
        {
            return null;
        }

        var all = items.Cast<object>().ToList();
        var rows = all.Take(RowsKept).Select(row => "[" + string.Join(", ", RowControls(row)) + "]").ToList();
        var text = $"{all.Count} rows" + (rows.Count > 0 ? ": " + string.Join(", ", rows) : string.Empty) + (all.Count > RowsKept ? ", ..." : string.Empty);
        return text.Length > RowsValueKept ? text[..RowsValueKept] + "…" : text;
    }

    private static IEnumerable<string> RowControls(object row)
    {
        foreach (var property in row.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0 && !property.PropertyType.IsValueType && property.PropertyType != typeof(string)))
        {
            object? control;
            try
            {
                control = property.GetValue(row);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                continue;
            }

            if (control is null || control.GetType().GetMethod("IsVisible", Type.EmptyTypes) is null)
            {
                continue;
            }

            // A row's control that is a list itself (or leads back to its own) is not read: the rows stay one level deep.
            if (control.GetType().GetProperty("Items") is not null)
            {
                continue;
            }

            var state = ReadControl(property.Name, control, rows: false);
            if (state.Shown != false && state.Value is { Length: > 0 } value)
            {
                yield return $"{property.Name} '{value}'";
            }
        }
    }

    private const int RowsKept = 10;

    private const int RowsValueKept = 1500;

    private static object? Invoke(object control, string member, bool takesTimeout)
    {
        var method = control.GetType().GetMethods().FirstOrDefault(candidate => candidate.Name == member && candidate.GetParameters().Length == (takesTimeout ? 1 : 0));
        return method?.Invoke(control, takesTimeout ? [ReadTimeoutMs] : []);
    }

    private static string? Text(object? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() is { Length: > ValueKept } text ? text[..ValueKept] + "…" : value.ToString(),
    };

    private static string Unreadable(Exception exception)
        => (exception is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : exception).Message;

    private static string Plain(string typeName) => typeName.IndexOf('`', StringComparison.Ordinal) is var tick and > 0 ? typeName[..tick] : typeName;
}
