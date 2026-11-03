using System.Text.Json.Nodes;
using Xunit;

namespace Bruinet.Uat.Tests;

// Since last green (AI9): where a failing trace first went another way than a passing one, and what kind of change it was.
public sealed class UatTraceDiffTests
{
    [Fact]
    public void ADifferentPageEarlier_IsTheFirstDifference_NotTheFailingStep()
    {
        var green = Trace("passed", Step(24, "I tap Delete", "Todo Detail", "Delete.Tap"), Step(25, "the dialog asks", "Todo Detail", "Dialog.Exists"));
        var failing = Trace("failed", Step(24, "I tap Delete", "Todo Edit", "Delete.Tap"), Step(25, "the dialog asks", "Todo Edit", "Dialog.Exists", status: "Failed"));

        var difference = UatTraceDiff.Compare(failing, green);

        Assert.Equal(("page-changed", 0, 24), (difference.Kind, difference.Index, difference.Line));
        Assert.Contains("page: Todo Detail then Todo Edit", difference.Differences);
    }

    [Fact]
    public void TheSameCalls_WithAWaitThatWasNearItsBudgetWhenGreen_IsTimingOnly()
    {
        var green = Trace("passed", Step(28, "the dialog asks", "Todo Detail", "Dialog.Exists", callMs: 4200));
        var failing = Trace("failed", Step(28, "the dialog asks", "Todo Detail", "Dialog.Exists", status: "Failed", attempts: 160, result: "error", callMs: 5010));
        failing["steps"]![0]!["message"] = "No item matched the predicate in collection within 5000 ms.";

        Assert.Equal("timing-only", UatTraceDiff.Compare(failing, green).Kind);
    }

    [Fact]
    public void AWaitThatWasQuickWhenGreen_AndNowRunsOut_IsDataChanged_WhateverItsMessageSays()
    {
        var green = Trace("passed", Step(75, "Todos have Keep the piano", "Todo List", "Todos.WaitRow", callMs: 80));
        var failing = Trace("failed", Step(75, "Todos have Keep the piano", "Todo List", "Todos.WaitRow", status: "Failed", attempts: 160, result: "error", callMs: 5010));
        failing["steps"]![0]!["message"] = "No item matched the predicate in collection within 5000 ms.";

        Assert.Equal("data-changed", UatTraceDiff.Compare(failing, green).Kind);
    }

    [Fact]
    public void LinesMovedByAnEditAbove_AreTheSameSteps()
    {
        var green = Trace("passed", Step(24, "I tap Delete", "Todo Detail", "Delete.Tap"), Step(25, "the dialog asks", "Todo Detail", "Dialog.Exists"));
        var failing = Trace("failed", Step(31, "I tap Delete", "Todo Detail", "Delete.Tap"), Step(32, "the dialog asks", "Todo Detail", "Dialog.Exists", status: "Failed"));

        var difference = UatTraceDiff.Compare(failing, green);

        Assert.Equal(("data-changed", 1, 32), (difference.Kind, difference.Index, difference.Line));
    }

    [Fact]
    public void TheSamePath_WithAnotherValueShown_IsDataChanged_AndASlowerStepBeforeIsAHint()
    {
        var green = Trace("passed", Step(23, "I tap Sync", "Todo List", "Sync.Click", ms: 100), Step(24, "Todos have Keep the piano", "Todo List", "Todos.AssertRow", value: "Keep the piano"));
        var failing = Trace("failed", Step(23, "I tap Sync", "Todo List", "Sync.Click", ms: 2400), Step(24, "Todos have Keep the piano", "Todo List", "Todos.AssertRow", value: "Buy milk", status: "Failed"));

        var difference = UatTraceDiff.Compare(failing, green);

        Assert.Equal(("data-changed", 1), (difference.Kind, difference.Index));
        Assert.Contains("Todos: 'Keep the piano' then 'Buy milk'", difference.Differences);
        Assert.Equal(["line 23: 100 ms then 2400 ms"], difference.Slower);
    }

    [Fact]
    public void AFailingStepThatStoppedEarly_OnTheSamePageByAnotherName_IsDataChanged()
    {
        var green = Trace("passed", Step(75, "Todos have Keep the piano", "TodoListPage", "Todos.WaitRow"));
        var failing = Trace("failed", Step(75, "Todos have Keep the piano", "Todo List", "Todos.FindItem", status: "Failed"));
        failing["steps"]![0]!["calls"] = new JsonArray();

        var difference = UatTraceDiff.Compare(failing, green);

        Assert.Equal("data-changed", difference.Kind);
        Assert.DoesNotContain(difference.Differences, item => item.StartsWith("page:", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEditedScenario_IsScenarioChanged()
    {
        var green = Trace("passed", Step(24, "I tap Delete", "Todo Detail", "Delete.Tap"));
        var failing = Trace("failed", Step(24, "I tap Remove", "Todo Detail", "Remove.Tap", status: "Failed"));

        Assert.Equal("scenario-changed", UatTraceDiff.Compare(failing, green).Kind);
    }

    [Fact]
    public void LastGreen_IsTheNewestGreenFileWrittenBeforeTheFailure_InThisRunOrAnEarlierOne()
    {
        var root = Path.Combine(Path.GetTempPath(), "Bruinet", nameof(UatTraceDiffTests), Guid.NewGuid().ToString("N"));
        try
        {
            var relative = Path.Combine("suites", "Todo.Uat", "uat", "step-traces", "delete__Asks__trace.json");
            string Write(string run, string status, int minutesAgo)
            {
                var path = Path.Combine(root, run, relative);
                path = status == "passed" ? UatTraceDiff.GreenOf(path) : path;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Trace(status).ToJsonString());
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
                return path;
            }

            Write("run1", "passed", 30);
            var green = Write("run2", "passed", 20);
            var now = Write("run2", "failed", 10);
            Write("run3", "passed", 5);

            // run3 passed after the failure, and run2 is older than run1 by its folder's time: neither matters.
            Directory.SetLastWriteTimeUtc(Path.Combine(root, "run2"), DateTime.UtcNow.AddDays(-1));
            Assert.Equal(green, UatTraceDiff.LastGreen(now));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonObject Trace(string status, params JsonObject[] steps)
        => new() { ["status"] = status, ["steps"] = new JsonArray([.. steps]) };

    private static JsonObject Step(int line, string text, string page, string call, string status = "Passed", long ms = 100, int attempts = 1, string result = "success", string? value = null, int callMs = 50)
    {
        var (control, action) = (call.Split('.')[0], call.Split('.')[1]);
        var step = new JsonObject
        {
            ["line"] = line,
            ["file"] = "Features/delete.feature.md",
            ["text"] = text,
            ["status"] = status,
            ["durationMs"] = ms,
            ["page"] = page,
            ["calls"] = new JsonArray(new JsonObject { ["page"] = page, ["control"] = control, ["action"] = action, ["ms"] = callMs, ["attempts"] = attempts, ["result"] = result }),
        };
        if (value is not null)
        {
            step["pageAfter"] = new JsonObject { ["page"] = page, ["controls"] = new JsonArray(new JsonObject { ["name"] = control, ["shown"] = true, ["value"] = value }) };
        }

        return step;
    }
}
