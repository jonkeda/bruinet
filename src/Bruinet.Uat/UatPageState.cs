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
    public static BddRunnerPageState Read(UatRuntimePage? page, object? instance)
    {
        if (page is null || instance is null)
        {
            return new BddRunnerPageState(page?.Name, [], page is null ? "No page is current: no step has opened one yet." : $"The page '{page.Name}' has no object to read.");
        }

        List<BddRunnerControlState> controls = [];
        foreach (var binding in page.Controls)
        {
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

    private static BddRunnerControlState ReadControl(string name, object control)
    {
        var type = control.GetType();
        var kind = Plain(type.Name);
        try
        {
            var shown = Invoke(control, "IsVisible", takesTimeout: false) as bool?;
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
