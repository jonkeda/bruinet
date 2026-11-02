namespace Bruinet.Core.Testing;

/// <summary>
/// Where the code that runs now belongs: the xUnit test, the scenario and the step (.my/test-selection/speed-plan.md, T1).
/// The runners set it; <see cref="Logging.Timing"/> and the call log read it, so every row names its test, and a run's rows
/// are both its timings and its touch map. It flows with the code, as an <see cref="AsyncLocal{T}"/> does.
/// </summary>
/// <param name="Test">The xUnit test's display name; null outside one.</param>
/// <param name="Scenario">The scenario's name; null outside one.</param>
/// <param name="Step">The step, as its line and text; null outside one.</param>
public sealed record TestScope(string? Test, string? Scenario, string? Step)
{
    private static readonly AsyncLocal<TestScope?> Now = new();

    /// <summary>The scope of the code running now; every part null outside a test.</summary>
    public static TestScope Current => Now.Value ?? Empty;

    /// <summary>No test, scenario or step.</summary>
    public static TestScope Empty { get; } = new(null, null, null);

    /// <summary>
    /// The scope entered last on any flow: for what runs outside a test's own flow, as an app's code on its UI thread
    /// does, so what it says can still be put under the step that made it run (<see cref="Logging.StepLog"/>).
    /// </summary>
    public static TestScope Latest => Volatile.Read(ref _latest) ?? Empty;

    private static TestScope? _latest;

    /// <summary>Enters a test; its scenario and step start empty. Disposing puts back the scope before.</summary>
    public static IDisposable EnterTest(string test) => Enter(new TestScope(test, null, null));

    /// <summary>Enters a scenario of the current test; its step starts empty.</summary>
    public static IDisposable EnterScenario(string scenario) => Enter(Current with { Scenario = scenario, Step = null });

    /// <summary>Enters a step of the current scenario.</summary>
    public static IDisposable EnterStep(string step) => Enter(Current with { Step = step });

    private static IDisposable Enter(TestScope scope)
    {
        var before = Now.Value;
        Now.Value = scope;
        Volatile.Write(ref _latest, scope);
        return new Restore(before);
    }

    private sealed class Restore(TestScope? before) : IDisposable
    {
        public void Dispose() => Now.Value = before;
    }
}
