using Bruinet.Core.Logging;
using Bruinet.Core.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bruinet.Logging.Tests;

// An app's log through its services into the step that runs: a warning and up, under the logger's category.
public sealed class StepLogProviderTests
{
    [Fact]
    public void WhatTheAppLogs_ThroughItsServices_IsTheSteps()
    {
        using var services = new ServiceCollection().AddBruinetStepLog().BuildServiceProvider();
        var logger = services.GetRequiredService<ILogger<StepLogProviderTests>>();
        IReadOnlyList<StepLogEntry> said;

        using (TestScope.EnterStep("7: When I sync"))
        {
            var step = TestScope.Current;
            var mark = StepLog.Mark();

            // ILogger itself, as LoggerMessage's generated code calls it.
            logger.Log(LogLevel.Error, default, "Sync failed: ServerError", new InvalidOperationException("500"), (state, _) => state);
            logger.Log(LogLevel.Debug, default, "not kept", null, (state, _) => state);
            said = StepLog.Since(mark, step);
        }

        var entry = Assert.Single(said);
        Assert.Equal(
            (StepLogLevel.Error, typeof(StepLogProviderTests).FullName, "Sync failed: ServerError", "InvalidOperationException: 500"),
            (entry.Level, entry.Source, entry.Message, entry.Exception));
    }

    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Critical, true)]
    public void ALevel_IsKeptFromWarningUp(LogLevel level, bool kept)
        => Assert.Equal(kept, new StepLogProvider().CreateLogger("app").IsEnabled(level));
}
