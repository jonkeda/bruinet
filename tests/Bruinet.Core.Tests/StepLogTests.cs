using Bruinet.Core.Logging;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

// What the app said during a step: what came after the step's mark, on its flow or said elsewhere while it was latest.
// One at a time: the latest scope is the process's.
[Collection(ConsoleOutput.Name)]
public sealed class StepLogTests
{
    [Fact]
    public void AStep_GetsWhatWasSaidSinceItsMark_OnItsFlow_AndNoOtherStepsWords()
    {
        TestScope first;
        TestScope second;
        long firstMark;
        long secondMark;
        using (TestScope.EnterScenario("Sync"))
        {
            using (TestScope.EnterStep("3: Given the server fails"))
            {
                first = TestScope.Current;
                firstMark = StepLog.Mark();
                StepLog.Add(StepLogLevel.Error, "app", "Sync failed", new InvalidOperationException("500"));
                StepLog.Add(StepLogLevel.Information, "app", "Too small to keep");
            }

            using (TestScope.EnterStep("4: Then it shows Retry"))
            {
                second = TestScope.Current;
                secondMark = StepLog.Mark();
                StepLog.Add(StepLogLevel.Warning, "app", "Retrying");
            }
        }

        var said = Assert.Single(StepLog.Since(firstMark, first));
        Assert.Equal((StepLogLevel.Error, "app", "Sync failed", "InvalidOperationException: 500"), (said.Level, said.Source, said.Message, said.Exception));
        Assert.Equal("Retrying", Assert.Single(StepLog.Since(secondMark, second)).Message);
    }

    [Fact]
    public void TheSameStepRunAgain_GetsOnlyWhatItsOwnRunSaid()
    {
        IReadOnlyList<StepLogEntry> again;
        using (TestScope.EnterScenario("Sync"))
        {
            using (TestScope.EnterStep("26: When I sync"))
            {
                StepLog.Add(StepLogLevel.Error, "app", "Sync failed");
            }

            using (TestScope.EnterStep("26: When I sync"))
            {
                var step = TestScope.Current;
                var mark = StepLog.Mark();
                again = StepLog.Since(mark, step);
            }
        }

        Assert.Empty(again);
    }

    [Fact]
    public async Task WhatIsSaidElsewhere_GoesToTheStepEnteredLast()
    {
        IReadOnlyList<StepLogEntry> said;
        using (TestScope.EnterStep("5: When the page renders"))
        {
            var step = TestScope.Current;
            var mark = StepLog.Mark();

            // As a browser's event loop says it: on a flow that carries none of the test's scope.
            Task elsewhere;
            using (ExecutionContext.SuppressFlow())
            {
                elsewhere = Task.Run(() => StepLog.Add(StepLogLevel.Error, "browser", "Uncaught TypeError", elsewhere: true));
            }

            await elsewhere;
            said = StepLog.Since(mark, step);
        }

        Assert.Equal("Uncaught TypeError", Assert.Single(said).Message);
    }

    [Fact]
    public void ALongText_IsCut()
    {
        IReadOnlyList<StepLogEntry> said;
        using (TestScope.EnterStep("1: Given a long message"))
        {
            var step = TestScope.Current;
            var mark = StepLog.Mark();
            StepLog.Add(StepLogLevel.Error, new string('s', 600), new string('x', 600), new InvalidOperationException(new string('e', 600)));
            said = StepLog.Since(mark, step);
        }

        var entry = Assert.Single(said);
        Assert.Equal((501, 501, 501), (entry.Source.Length, entry.Message.Length, entry.Exception!.Length));
    }

    [Fact]
    public void TheLatestScope_GoesBack_WhenItsStepEnds()
    {
        using (TestScope.EnterScenario("Latest"))
        {
            using (TestScope.EnterStep("1: Given a step"))
            {
                Assert.Equal("1: Given a step", TestScope.Latest.Step);
            }

            Assert.Equal(("Latest", null), (TestScope.Latest.Scenario, TestScope.Latest.Step));
        }
    }
}
