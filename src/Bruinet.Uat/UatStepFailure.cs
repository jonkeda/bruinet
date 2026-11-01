using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bruinet.Core.Exceptions;

namespace Bruinet.Uat;

/// <summary>
/// A failed step in a scenario's report, for an agent or a tool to act on: what kind of failure it was,
/// what was expected and found, which locator, which binding, and the suite's and the app's frames it
/// came through. From the step's exception, so the gate, the runner and the IDE all write the same.
/// </summary>
internal static partial class UatStepFailure
{
    // Assembly names whose frames are the framework's or a test stack's, not the suite's or the app's.
    private static readonly string[] FrameworkPrefixes = ["System.", "Microsoft.", "xunit", "nunit", "MSTest", "Castle.", "Nerdbank.", "StreamJsonRpc", "Bunit", "AngleSharp", "FlaUI.", "WebDriver", "Appium", "MauiHeadless"];

    private static readonly HashSet<string> FrameworkNames = new(StringComparer.OrdinalIgnoreCase) { "System", "netstandard", "mscorlib" };

    private const int FramesKept = 5;

    private const int ValueKept = 500;

    /// <summary>The failure's JSON; null for a step that did not fail.</summary>
    /// <param name="step">The step's result.</param>
    public static JsonObject? Of(BddStepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Status != BddStepResultStatus.Failed)
        {
            return null;
        }

        // Whatever goes wrong describing it, the report is still written: the failure says so.
        try
        {
            return Describe(step);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new JsonObject { ["kind"] = "error", ["binding"] = step.Invocation.CommandId, ["unreadable"] = exception.Message };
        }
    }

    private static JsonObject Describe(BddStepResult step)
    {
        var exceptions = Chain(step.Exception).ToList();
        var node = new JsonObject
        {
            ["kind"] = Kind(exceptions, bound: step.Invocation.Command.Handler is not null),
            ["exception"] = exceptions.LastOrDefault(exception => exception is not TargetInvocationException)?.GetType().Name,
            ["binding"] = step.Invocation.CommandId,
        };

        if (exceptions.OfType<AssertionException>().FirstOrDefault() is { } assertion)
        {
            node["expected"] = Kept(Text(assertion.Expected));
            node["actual"] = Kept(Text(assertion.Actual));
        }

        node["locator"] = Locate(exceptions);
        if (exceptions.OfType<WaitTimeoutException>().FirstOrDefault() is { } wait)
        {
            node["waitedMs"] = wait.TimeoutMs;
            node["condition"] = Kept(wait.Condition);
        }

        if (exceptions.OfType<ElementNotReadyException>().FirstOrDefault() is { } notReady)
        {
            node["notReady"] = notReady.Reason.ToString();
        }

        var frames = Frames(exceptions).ToList();
        if (frames.Count > 0)
        {
            node["frames"] = new JsonArray([.. frames.Select(frame => (JsonNode?)frame)]);
        }

        return node;
    }

    /// <summary>assertion, locator-not-found, timeout, app-unavailable, bind, app-exception or error.</summary>
    internal static string Kind(IReadOnlyList<Exception> exceptions, bool bound)
    {
        if (exceptions.Count == 0)
        {
            return bound ? "error" : "bind";
        }

        if (exceptions.Any(exception => exception is AssertionException || exception.GetType().FullName?.StartsWith("Xunit.", StringComparison.Ordinal) == true))
        {
            return "assertion";
        }

        if (exceptions.Any(exception => exception is ElementNotFoundException or StaleElementException))
        {
            return "locator-not-found";
        }

        if (exceptions.Any(exception => exception is ElementNotReadyException or ScopeNotReadyException or WaitTimeoutException or TimeoutException))
        {
            return "timeout";
        }

        if (exceptions.Any(exception => exception is AppUnavailableException or PageLoadException or RouteUnavailableException))
        {
            return "app-unavailable";
        }

        // Thrown from the suite's or the app's code, a runtime call there included (int.Parse, First()): the first
        // frame outside the runtime is theirs, not Bruinet's.
        return Thrower(exceptions[^1]) is { } thrower && !IsFramework(thrower) ? "app-exception" : "error";
    }

    internal static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    /// <summary>The control's locator: an exception's own, else the <c>Locator: ...</c> a control's message names it by.</summary>
    internal static string? Locate(IReadOnlyList<Exception> exceptions)
        => exceptions.Select(Locator).FirstOrDefault(locator => locator is not null)
            ?? exceptions.Select(exception => LocatorInMessage().Match(exception.Message)).FirstOrDefault(match => match.Success)?.Groups[1].Value;

    private static string? Locator(Exception exception) => exception switch
    {
        AssertionException assertion => assertion.ControlLocator,
        ElementNotFoundException notFound => notFound.LocatorString ?? notFound.LocatorInfo?.ToString(),
        ElementNotReadyException notReady => notReady.Locator.ToString(),
        StaleElementException stale => stale.Locator?.ToString(),
        _ => null,
    };

    // Each user-code frame once, innermost exception first: Type.Method (file:line).
    internal static IEnumerable<string> Frames(IEnumerable<Exception> exceptions)
        => exceptions.Reverse()
            .SelectMany(exception => new StackTrace(exception, fNeedFileInfo: true).GetFrames())
            .Where(frame => frame.GetMethod()?.DeclaringType is { } type && !IsFramework(type))
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .Take(FramesKept);

    // An async method's or a lambda's frame by the method it was written in, not the compiler's class.
    private static string Describe(StackFrame frame)
    {
        var method = frame.GetMethod()!;
        var type = method.DeclaringType!;
        var name = method.Name;
        if (type.Name.StartsWith('<') && type.DeclaringType is { } outer)
        {
            name = CompilerName().Match(type.Name) is { Success: true } stateMachine ? stateMachine.Groups[1].Value
                : CompilerName().Match(name) is { Success: true } lambda ? lambda.Groups[1].Value + " (lambda)"
                : name;
            type = outer;
        }

        var where = frame.GetFileName() is { } file
            ? string.Create(CultureInfo.InvariantCulture, $" ({Path.GetFileName(file)}:{frame.GetFileLineNumber()})")
            : string.Empty;
        return $"{Plain(type.Name)}.{name}{where}";
    }

    private static Type? Thrower(Exception exception)
        => new StackTrace(exception, fNeedFileInfo: false).GetFrames()
            .Select(frame => frame.GetMethod()?.DeclaringType)
            .FirstOrDefault(type => type is not null && !IsRuntime(type));

    // Bruinet's own assemblies are framework; a suite's and a sample app's are not.
    private static bool IsFramework(Type type)
        => type.Assembly.GetName().Name is not { } name
            || IsRuntime(type)
            || FrameworkPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || (name.StartsWith("Bruinet.", StringComparison.Ordinal)
                && !name.Contains(".Samples.", StringComparison.Ordinal)
                && !name.EndsWith("Tests", StringComparison.Ordinal)
                && !name.EndsWith(".Uat", StringComparison.Ordinal));

    private static bool IsRuntime(Type type)
        => type.Assembly.GetName().Name is not { } name
            || FrameworkNames.Contains(name)
            || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase);

    private static string Plain(string typeName) => typeName.IndexOf('`', StringComparison.Ordinal) is var tick and > 0 ? typeName[..tick] : typeName;

    private static string? Kept(string? value) => value is { Length: > ValueKept } ? $"{value[..ValueKept]}… ({value.Length - ValueKept} more)" : value;

    private static string? Text(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    [GeneratedRegex(@"Locator: (\S+)")]
    private static partial Regex LocatorInMessage();

    // <Method>d__3 (a state machine) or <Method>b__0_0 (a lambda): the method's own name.
    [GeneratedRegex(@"^<([^>]+)>")]
    private static partial Regex CompilerName();
}
