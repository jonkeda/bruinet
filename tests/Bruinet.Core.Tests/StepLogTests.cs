using Bruinet.Core.Logging;
using Bruinet.Core.Testing;
using Bruinet.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bruinet.Core.Tests;

// What the app said during a step: kept under the step that ran, warnings and up, through an ILoggerProvider.
public sealed class StepLogTests
{
    [Fact]
    public void WhatIsSaid_IsKeptUnderTheStepThatRuns_AndNoOther()
    {
        var scenario = $"Scenario {Guid.NewGuid():N}";
        using (TestScope.EnterTest("StepLogTests"))
        using (TestScope.EnterScenario(scenario))
        {
            using (TestScope.EnterStep("3: Given the server fails"))
            {
                StepLog.Add(StepLogLevel.Error, "app", "Sync failed", new InvalidOperationException("500"));
                StepLog.Add(StepLogLevel.Information, "app", "Too small to keep");
            }

            using (TestScope.EnterStep("4: Then it shows Retry"))
            {
                StepLog.Add(StepLogLevel.Warning, "app", "Retrying");
            }
        }

        var first = Assert.Single(StepLog.Of("StepLogTests", scenario, "3: Given the server fails"));
        Assert.Equal((StepLogLevel.Error, "app", "Sync failed", "InvalidOperationException: 500"), (first.Level, first.Source, first.Message, first.Exception));
        Assert.Equal("Retrying", Assert.Single(StepLog.Of(null, scenario, "4: Then it shows Retry")).Message);
    }

    [Fact]
    public void WhatTheAppLogs_ThroughItsServices_IsKept()
    {
        var scenario = $"Scenario {Guid.NewGuid():N}";
        using var services = new ServiceCollection().AddBruinetStepLog().BuildServiceProvider();
        var logger = services.GetRequiredService<ILogger<StepLogTests>>();

        using (TestScope.EnterScenario(scenario))
        using (TestScope.EnterStep("7: When I sync"))
        {
            // ILogger itself, as LoggerMessage's generated code calls it.
            logger.Log(LogLevel.Error, default, "Sync failed: ServerError", null, (state, _) => state);
            logger.Log(LogLevel.Debug, default, "not kept", null, (state, _) => state);
        }

        var entry = Assert.Single(StepLog.Of(null, scenario, "7: When I sync"));
        Assert.Equal((StepLogLevel.Error, typeof(StepLogTests).FullName, "Sync failed: ServerError"), (entry.Level, entry.Source, entry.Message));
    }

    [Fact]
    public void ALongMessage_IsCut()
    {
        var scenario = $"Scenario {Guid.NewGuid():N}";
        using (TestScope.EnterScenario(scenario))
        using (TestScope.EnterStep("1: Given a long message"))
        {
            StepLog.Add(StepLogLevel.Error, "app", new string('x', 600));
        }

        Assert.Equal(501, Assert.Single(StepLog.Of(null, scenario, "1: Given a long message")).Message.Length);
    }
}
