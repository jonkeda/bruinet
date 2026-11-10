using Bruinet.Core.Locators;

namespace Bruinet.Core.Exceptions;

/// <summary>
/// Exception thrown when an assertion fails.
/// </summary>
/// <remarks>
/// The overloads carrying <c>actual</c> put it into the message. They used to keep it as a
/// property only, so a failure said what was expected and never what was found, and the value the
/// framework had already read had to be rediscovered by hand before the failure meant anything.
/// </remarks>
public class ControlAssertionException : BruinetException
{
    /// <summary>The value the assertion required.</summary>
    public object? Expected { get; }

    /// <summary>The value the control actually reported.</summary>
    public object? Actual { get; }

    /// <summary>The locator of the control asserted on, when the caller supplied one.</summary>
    public Locator? Locator { get; }

    public ControlAssertionException(string message) : base(message) { }

    public ControlAssertionException(string message, object? expected, object? actual)
        : base(Describe(message, actual, null))
    {
        Expected = expected;
        Actual = actual;
    }

    public ControlAssertionException(string message, object? expected, object? actual, Locator locator)
        : base(Describe(message, actual, locator?.ToString()))
    {
        Expected = expected;
        Actual = actual;
        Locator = locator;
    }

    public ControlAssertionException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Appends what was actually found to the caller's message.
    /// </summary>
    private static string Describe(string message, object? actual, string? controlLocator)
    {
        var described = $"{message}{Environment.NewLine}Actual: {Format(actual)}";

        return controlLocator == null
            ? described
            : $"{described}{Environment.NewLine}Locator: {controlLocator}";
    }

    /// <summary>
    /// Renders a value so that null, empty and whitespace are told apart at a glance.
    /// </summary>
    private static string Format(object? value)
    {
        if (value is null)
            return "(null)";

        if (value is string text)
            return text.Length == 0 ? "(empty string)" : $"'{text}'";

        if (value is System.Collections.IEnumerable items and not string)
        {
            var rendered = new List<string>();
            foreach (var item in items)
            {
                rendered.Add(item is null ? "(null)" : item.ToString() ?? string.Empty);
                if (rendered.Count == 10)
                {
                    rendered.Add("...");
                    break;
                }
            }

            return $"[{string.Join(", ", rendered)}]";
        }

        return $"'{value}'";
    }
}
