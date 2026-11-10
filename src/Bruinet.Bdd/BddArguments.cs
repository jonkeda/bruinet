using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bruinet.Bdd;

/// <summary>
/// A typed phrase's values: checked when a step binds, read again when it runs with the
/// scenario's clock and services. One reader, <see cref="BddValues"/>, for both (AD-016).
/// </summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
internal static class BddArguments
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>What is wrong with the values a step gives its phrase, at the step or the table row.</summary>
    /// <param name="pattern">The phrase the step bound to.</param>
    /// <param name="step">The step.</param>
    /// <param name="arguments">The placeholders' text as written, quotes kept.</param>
    /// <param name="context">The users and the app's types; the clock only needs to exist.</param>
    public static IReadOnlyList<BddDiagnostic> Check(
        BddCommandPattern pattern,
        BddStep step,
        IReadOnlyDictionary<string, string> arguments,
        BddValueContext context)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        List<BddDiagnostic> diagnostics = [];
        if (pattern.Problem is { } broken)
        {
            diagnostics.Add(Error(broken.Code, $"Step '{step.Text}': the phrase '{pattern.Phrase}' from {broken.Declaration} {broken.Message}", step.Source));
            return diagnostics;
        }

        foreach (var parameter in pattern.Parameters)
        {
            switch (parameter.Source)
            {
                case BddParameterSource.Phrase:
                    if (arguments.TryGetValue(parameter.Name, out var text) &&
                        !BddValues.TryRead(text, parameter.Type, context, out _, out var problem, parameter.AllowsNull))
                    {
                        diagnostics.Add(Error(problem!.Code, $"Step '{step.Text}': '{parameter.Name}' is a {BddValues.Describe(parameter.Type)}; {problem.Message}", step.Source) with { Fix = FixOf(problem) });
                    }

                    break;

                case BddParameterSource.Table when step.Table is not null:
                    diagnostics.AddRange(BddTableBinding.Check(step.Table, parameter.Type, context)
                        .Select(found => Error(found.Problem.Code, $"Step '{step.Text}': '{parameter.Name}': {found.Problem.Message}", found.Location) with { Fix = FixOf(found.Problem) }));
                    break;

                case BddParameterSource.Table when step.DocString is not null:
                    if (TryReadJson(step.DocString, parameter.Type, out var error) is null && error is not null)
                    {
                        diagnostics.Add(Error("UATA001", $"Step '{step.Text}': the doc string is not a {parameter.Type.Name}: {error}", step.Source));
                    }

                    break;

                case BddParameterSource.Table when !parameter.Optional:
                    diagnostics.Add(Error("UATA007", $"Step '{step.Text}' needs a table under it for '{parameter.Name}' ({Shape(parameter.Type)}).", step.Source));
                    break;

                case BddParameterSource.DocString when step.DocString is null && !parameter.Optional:
                    diagnostics.Add(Error("UATA007", $"Step '{step.Text}' needs a doc string (\"\"\") under it for '{parameter.Name}', which its phrase does not name.", step.Source));
                    break;
            }
        }

        return diagnostics;
    }

    /// <summary>The value of one typed parameter for a running step.</summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="invocation">The bound step.</param>
    /// <param name="context">The scenario's clock, users, types and services.</param>
    /// <returns>The value, or <see cref="Type.Missing"/> for an optional one the step leaves out.</returns>
    /// <exception cref="InvalidOperationException">The value does not fit; the message names the parameter.</exception>
    public static object? Read(BddPhraseParameter parameter, BddStepInvocation invocation, BddValueContext context)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(context);

        var step = invocation.Step;
        try
        {
            return parameter.Source switch
            {
                BddParameterSource.Phrase when invocation.RawArguments.TryGetValue(parameter.Name, out var text)
                    => BddValues.Read(text, parameter.Type, context, parameter.AllowsNull),
                BddParameterSource.Table when step.Table is not null => step.Table.Bind(parameter.Type, context),
                BddParameterSource.Table when step.DocString is not null
                    => TryReadJson(step.DocString, parameter.Type, out var error) ?? throw new InvalidOperationException($"the doc string is not a {parameter.Type.Name}: {error}"),
                BddParameterSource.DocString when step.DocString is not null => step.DocString,
                _ when parameter.Optional => Type.Missing,
                _ => throw new InvalidOperationException($"the step gives no value for '{parameter.Name}'."),
            };
        }
        catch (BddValueException exception)
        {
            throw new InvalidOperationException($"'{parameter.Name}' is a {BddValues.Describe(parameter.Type)}; {exception.Problem.Message}", exception);
        }
    }

    private static object? TryReadJson(string text, Type type, out string? error)
    {
        error = null;
        try
        {
            return JsonSerializer.Deserialize(text, type, Json);
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return null;
        }
    }

    private static BddTextFix? FixOf(BddValueProblem problem)
        => problem is { Fix: { } fix, Original: { } original } ? new BddTextFix(original, fix) : null;

    private static string Shape(Type type)
        => BddValues.ElementType(type) is { } element
            ? BddValues.IsInline(element) ? $"a one-column table of {BddValues.Describe(element)}" : $"a row per {element.Name}"
            : $"a 'Field | Value' table for one {type.Name}";

    private static BddDiagnostic Error(string code, string message, BddSourceLocation location)
        => new(BddDiagnosticSeverity.Error, code, message, location);
}
