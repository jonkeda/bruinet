using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Bruinet.Core.Exceptions;

namespace Bruinet.Uat;

/// <summary>
/// A failed step in a scenario's report, for an agent or a tool to act on: what kind of failure it was,
/// what was expected and found, which locator, which binding, and the user-code frames it came through
/// (.my/aiimprovements/AI2-design.md). From the step's exception, so the gate, the runner and the
/// IDE all write the same.
/// </summary>
internal static partial class UatStepFailure
{
    // Assemblies whose frames are the framework's, not the suite's or the app's: by name, so a suite's own
    // Bruinet.Uat.Tests is not taken for Bruinet.Uat.
    private static readonly string[] FrameworkPrefixes = ["System", "Microsoft.", "netstandard", "mscorlib", "xunit", "Castle.", "Nerdbank.", "StreamJsonRpc"];

    private static readonly HashSet<string> FrameworkAssemblies = new(StringComparer.Ordinal)
    {
        "Bruinet.Core", "Bruinet.Bdd", "Bruinet.Uat", "Bruinet.Html", "Bruinet.Html.Bunit", "Bruinet.Html.Playwright",
        "Bruinet.Blazor", "Bruinet.Maui", "Bruinet.Maui.Headless", "Bruinet.Maui.FlaUI", "Bruinet.Maui.Appium",
        "Bruinet.Appium", "Bruinet.NativeAndroid", "Bruinet.Wpf", "Bruinet.WinForms", "Bruinet.Stride", "Bruinet.Narrator",
    };

    private const int FramesKept = 5;

    /// <summary>The failure's JSON; null for a step that did not fail.</summary>
    /// <param name="step">The step's result.</param>
    public static JsonObject? Of(BddStepResult step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.Status != BddStepResultStatus.Failed)
        {
            return null;
        }

        var exceptions = Chain(step.Exception).ToList();
        var node = new JsonObject
        {
            ["kind"] = Kind(exceptions, bound: step.Invocation.Command.Handler is not null),
            ["exception"] = exceptions.LastOrDefault(exception => exception is not System.Reflection.TargetInvocationException)?.GetType().Name,
            ["binding"] = step.Invocation.CommandId,
        };

        if (exceptions.OfType<AssertionException>().FirstOrDefault() is { } assertion)
        {
            node["expected"] = Text(assertion.Expected);
            node["actual"] = Text(assertion.Actual);
        }

        node["locator"] = Locate(exceptions);
        if (exceptions.OfType<WaitTimeoutException>().FirstOrDefault() is { } wait)
        {
            node["waitedMs"] = wait.TimeoutMs;
            node["condition"] = wait.Condition;
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
    /// <param name="exceptions">The failure's exception and its inner ones, outermost first.</param>
    /// <param name="bound">Whether the step had a definition to run.</param>
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

        // Thrown in the suite's or the app's own code, not in Bruinet's.
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
    /// <param name="exceptions">The failure's exception and its inner ones.</param>
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

    private static string Describe(StackFrame frame)
    {
        var method = frame.GetMethod()!;
        var where = frame.GetFileName() is { } file
            ? string.Create(CultureInfo.InvariantCulture, $" ({Path.GetFileName(file)}:{frame.GetFileLineNumber()})")
            : string.Empty;
        return $"{method.DeclaringType!.Name}.{method.Name}{where}";
    }

    private static Type? Thrower(Exception exception)
        => new StackTrace(exception, fNeedFileInfo: false).GetFrame(0)?.GetMethod()?.DeclaringType;

    private static bool IsFramework(Type type)
        => type.Assembly.GetName().Name is not { } name
            || FrameworkAssemblies.Contains(name)
            || FrameworkPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string? Text(object? value) => value switch
    {
        null => null,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    [System.Text.RegularExpressions.GeneratedRegex(@"Locator: (\S+)")]
    private static partial System.Text.RegularExpressions.Regex LocatorInMessage();
}
