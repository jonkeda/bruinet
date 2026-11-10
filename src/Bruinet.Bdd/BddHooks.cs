using System.Collections.Concurrent;
using System.Reflection;

namespace Bruinet.Bdd;

/// <summary>When a hook runs.</summary>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public enum BddHookKind
{
    /// <summary>Once per feature file per test run, before its first scenario.</summary>
    BeforeFeature,

    /// <summary>Before each scenario, after the test base's own <c>BeforeScenario</c>.</summary>
    BeforeScenario,

    /// <summary>After each scenario, also when a step failed.</summary>
    AfterScenario,

    /// <summary>After each step, with its result.</summary>
    AfterStep,
}

/// <summary>
/// On a static method of a step-definition class: runs once per feature file per test run, before
/// its first scenario.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BddBeforeFeatureAttribute : Attribute
{
}

/// <summary>A hook on a step-definition method, for the scenarios with one of <see cref="Tags"/>, or for every scenario.</summary>
[AttributeUsage(AttributeTargets.Method)]
public abstract class BddScenarioHookAttribute : Attribute
{
    /// <param name="tags">The tags it runs for, with or without <c>@</c>; every scenario when none.</param>
    protected BddScenarioHookAttribute(string[] tags)
    {
        Tags = tags.Select(BddTagConventions.NormalizeTag).Where(tag => tag.Length > 0).ToArray();
    }

    /// <summary>The tags it runs for; empty for every scenario.</summary>
    public IReadOnlyList<string> Tags { get; }

    internal abstract BddHookKind Kind { get; }
}

/// <summary>Runs before each scenario (with one of the tags, when given).</summary>
public sealed class BddBeforeScenarioAttribute(params string[] tags) : BddScenarioHookAttribute(tags)
{
    internal override BddHookKind Kind => BddHookKind.BeforeScenario;
}

/// <summary>Runs after each scenario (with one of the tags, when given), also when a step failed.</summary>
public sealed class BddAfterScenarioAttribute(params string[] tags) : BddScenarioHookAttribute(tags)
{
    internal override BddHookKind Kind => BddHookKind.AfterScenario;
}

/// <summary>Runs after each step of a scenario (with one of the tags, when given); may take the step's <see cref="BddStepResult"/>.</summary>
public sealed class BddAfterStepAttribute(params string[] tags) : BddScenarioHookAttribute(tags)
{
    internal override BddHookKind Kind => BddHookKind.AfterStep;
}

/// <summary>A hook as the catalog holds it.</summary>
/// <param name="Kind">When it runs.</param>
/// <param name="Name">The method, <c>Class.Method</c>: hooks of one kind run in its order.</param>
/// <param name="Tags">The tags it runs for; empty for every scenario.</param>
/// <param name="Run">Runs it; the step result is set for <see cref="BddHookKind.AfterStep"/> only.</param>
[System.Diagnostics.CodeAnalysis.Experimental("BRUINET010", UrlFormat = "https://github.com/jonkeda/bruinet/blob/main/docs/experimental.md#{0}")]
public sealed record BddHook(
    BddHookKind Kind,
    string Name,
    IReadOnlyList<string> Tags,
    Func<BddExecutionContext, BddStepResult?, CancellationToken, Task> Run)
{
    /// <summary>Whether it runs for a scenario with these tags.</summary>
    /// <param name="scenarioTags">The scenario's tags.</param>
    public bool AppliesTo(IEnumerable<string> scenarioTags)
        => Tags.Count == 0 || scenarioTags.Select(BddTagConventions.NormalizeTag).Any(tag => Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
}

// Finds the hooks of a step-definition class and runs them around a scenario.
internal static class BddHooks
{
    // Per test run: a feature's BeforeFeature hooks run before its first scenario only.
    private static readonly ConcurrentDictionary<(string Hook, string Feature), bool> FeaturesStarted = new();

    public static IReadOnlyList<BddHookMethod> Discover(Type type)
    {
        List<BddHookMethod> hooks = [];
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttribute<BddBeforeFeatureAttribute>() is not null)
            {
                if (!method.IsStatic)
                {
                    throw new InvalidOperationException($"{type.Name}.{method.Name} is a [BddBeforeFeature] hook, which runs before any scenario has a scope: make it static.");
                }

                hooks.Add(new BddHookMethod(BddHookKind.BeforeFeature, method, []));
            }

            foreach (var attribute in method.GetCustomAttributes<BddScenarioHookAttribute>())
            {
                hooks.Add(new BddHookMethod(attribute.Kind, method, attribute.Tags));
            }
        }

        return hooks;
    }

    public static async Task<BddScenarioRunResult> RunAsync(
        BddBoundScenario scenario,
        BddExecutionContext context,
        BddStepExecutionSession session,
        CancellationToken cancellationToken)
    {
        var hooks = scenario.Hooks.Where(hook => hook.AppliesTo(scenario.Source.Tags)).ToList();
        var feature = scenario.Source.Source.FilePath ?? string.Empty;
        string? failure = null;

        foreach (var hook in Of(hooks, BddHookKind.BeforeFeature).Where(hook => FeaturesStarted.TryAdd((hook.Name, feature), true))
                     .Concat(Of(hooks, BddHookKind.BeforeScenario)))
        {
            failure = await TryRun(hook, context, null, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                session.Fail($"The hook {hook.Name} failed before the scenario: {failure}");
                break;
            }
        }

        while (failure is null && session.HasNext)
        {
            var result = await session.RunNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var hook in Of(hooks, BddHookKind.AfterStep))
            {
                if (await TryRun(hook, context, result, cancellationToken).ConfigureAwait(false) is { } problem)
                {
                    session.FailLast($"the hook {hook.Name} failed after it: {problem}");
                    failure = problem;
                    break;
                }
            }

            if (session.Results[^1].Status is BddStepResultStatus.Failed or BddStepResultStatus.Canceled)
            {
                break;
            }
        }

        foreach (var hook in Of(hooks, BddHookKind.AfterScenario))
        {
            if (await TryRun(hook, context, null, CancellationToken.None).ConfigureAwait(false) is { } problem)
            {
                session.FailLast($"the hook {hook.Name} failed after the scenario: {problem}");
            }
        }

        return session.ToResult();
    }

    private static IEnumerable<BddHook> Of(List<BddHook> hooks, BddHookKind kind) => hooks.Where(hook => hook.Kind == kind);

    private static async Task<string?> TryRun(BddHook hook, BddExecutionContext context, BddStepResult? result, CancellationToken cancellationToken)
    {
        try
        {
            await hook.Run(context, result, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return (exception is TargetInvocationException { InnerException: { } inner } ? inner : exception).Message;
        }
    }
}

internal sealed record BddHookMethod(BddHookKind Kind, MethodInfo Method, IReadOnlyList<string> Tags);
