using Bruinet.Core.Exceptions;

namespace Bruinet.Uat;

/// <summary>
/// Marks the part of a test that sets up its state, so a failure there reports as kind <c>setup</c>
/// (a <see cref="UiSetupException"/>) and not as a broken feature. <c>using static Bruinet.Uat.UiSetup;</c>
/// lets a test write <c>Setup(() => ...)</c>.
/// </summary>
public static class UiSetup
{
    /// <summary>Runs <paramref name="setup"/>; whatever it throws comes out as a <see cref="UiSetupException"/>.</summary>
    /// <param name="setup">The setup to run.</param>
    public static void Setup(Action setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        try
        {
            setup();
        }
        catch (Exception exception) when (exception is not UiSetupException and not OperationCanceledException)
        {
            throw new UiSetupException(exception);
        }
    }

    /// <summary>Runs <paramref name="setup"/> and returns its value; whatever it throws comes out as a <see cref="UiSetupException"/>.</summary>
    /// <typeparam name="T">What the setup gives back, such as a page object.</typeparam>
    /// <param name="setup">The setup to run.</param>
    public static T Setup<T>(Func<T> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        try
        {
            return setup();
        }
        catch (Exception exception) when (exception is not UiSetupException and not OperationCanceledException)
        {
            throw new UiSetupException(exception);
        }
    }

    /// <summary>Awaits <paramref name="setup"/>; whatever it throws comes out as a <see cref="UiSetupException"/>.</summary>
    /// <param name="setup">The setup to run.</param>
    public static async Task SetupAsync(Func<Task> setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        try
        {
            await setup().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not UiSetupException and not OperationCanceledException)
        {
            throw new UiSetupException(exception);
        }
    }
}

/// <summary>
/// A check a fixture runs around each <see cref="UiFactAttribute"/> test: <see cref="BeginTest"/> before it,
/// <see cref="CheckPassedTest"/> after it passed, such as comparing the writes it sent with their contracts.
/// A throw from <see cref="CheckPassedTest"/> fails the test. A scenario fixture calls the same check from
/// <see cref="IUatScenarioLifecycle.AfterScenario"/>.
/// </summary>
public interface IUiTestCheck
{
    /// <summary>Before the test: note where it starts, such as the time.</summary>
    void BeginTest();

    /// <summary>After the test passed: throw to fail it.</summary>
    void CheckPassedTest();
}
