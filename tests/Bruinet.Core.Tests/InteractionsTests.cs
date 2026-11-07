using System.Drawing;
using Bruinet.Core.Recording;

namespace Bruinet.Core.Tests;

/// <summary>Tests that report to or record <see cref="Interactions"/>, whose logs are process-wide.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class InteractionLog
{
    public const string Name = "Interaction log";
}

/// <summary>The interaction log a recording keeps: what a driver's element reports before it acts.</summary>
[Collection(InteractionLog.Name)]
public sealed class InteractionsTests
{
    [Fact]
    public void WithNothingRecording_AReport_ReadsNothing()
    {
        var read = false;

        using (Interactions.Report(InteractionKind.Tap, () => { read = true; return Rectangle.Empty; }))
        {
        }

        Assert.Null(Interactions.Log);
        Assert.False(read);
    }

    [Fact]
    public void WhileRecording_AnAction_IsLoggedOnce_WithItsBounds_EvenWhenItRunsAnother()
    {
        var log = new Log();
        using (Interactions.Begin(Window(1), log))
        {
            // A double click that runs as a gesture, which reports too: one interaction.
            using (Interactions.Report(InteractionKind.DoubleTap, () => new Rectangle(10, 20, 30, 40), () => "Row"))
            {
                using (Interactions.Report(InteractionKind.Tap, () => new Rectangle(0, 0, 1, 1)))
                {
                }
            }

            using (Interactions.Report(InteractionKind.Swipe, () => new Rectangle(0, 100, 400, 300), from: new Point(200, 120), to: new Point(200, 380)))
            {
            }
        }

        Assert.Equal([InteractionKind.DoubleTap, InteractionKind.Swipe], log.Taken.Select(interaction => interaction.Kind));
        Assert.Equal(new Rectangle(10, 20, 30, 40), log.Taken[0].Bounds);
        Assert.Equal("Row", log.Taken[0].Label);
        Assert.Equal(new Point(200, 380), log.Taken[1].To);
        Assert.Null(Interactions.Log);
    }

    [Fact]
    public void ASwipeWithADirectionOnly_IsDrawnAcrossTheMiddleHalfOfItsTarget()
    {
        var log = new Log();
        using (Interactions.Begin(Window(1), log))
        {
            using (Interactions.ReportSwipe(() => new Rectangle(0, 100, 400, 400), dx: 0, dy: 1))
            {
            }
        }

        var swipe = Assert.Single(log.Taken);
        Assert.Equal(InteractionKind.Swipe, swipe.Kind);
        Assert.Equal(new Point(200, 200), swipe.From);
        Assert.Equal(new Point(200, 400), swipe.To);
    }

    [Fact]
    public void ATargetWhoseBoundsCannotBeRead_LeavesNoMark_AndTheActionRuns()
    {
        var log = new Log();
        using (Interactions.Begin(Window(1), log))
        {
            using (Interactions.Report(InteractionKind.Tap, () => throw new InvalidOperationException("The element is stale.")))
            {
            }
        }

        Assert.Empty(log.Taken);
    }

    [Fact]
    public void TwoRecordingsAtOnce_KeepTheirTapsApart()
    {
        var first = new Log();
        var second = new Log();
        using (Interactions.Begin(Window(1), first))
        using (Interactions.Begin(Window(2), second))
        {
            using (Interactions.Report(InteractionKind.Tap, () => new Rectangle(1, 1, 1, 1), target: () => Window(1)))
            {
            }

            using (Interactions.Report(InteractionKind.Swipe, () => new Rectangle(2, 2, 2, 2), target: () => Window(2)))
            {
            }

            // A report that cannot name its recording, while two record, belongs to neither.
            using (Interactions.Report(InteractionKind.Tap, () => new Rectangle(3, 3, 3, 3)))
            {
            }

            Assert.Null(Interactions.Log);
        }

        Assert.Equal(InteractionKind.Tap, Assert.Single(first.Taken).Kind);
        Assert.Equal(InteractionKind.Swipe, Assert.Single(second.Taken).Kind);
        Assert.Null(Interactions.Log);
    }

    [Fact]
    public void AReportNamingAnotherTarget_IsNotThisRecordings()
    {
        var log = new Log();
        using (Interactions.Begin(Window(1), log))
        {
            using (Interactions.Report(InteractionKind.Tap, () => new Rectangle(1, 1, 1, 1), target: () => Window(9)))
            {
            }
        }

        Assert.Empty(log.Taken);
    }

    private static RecordingTarget Window(int handle) => new RecordingTarget.Window(handle);

    private sealed class Log : IInteractionLog
    {
        public List<Interaction> Taken { get; } = [];

        public void Add(Interaction interaction) => Taken.Add(interaction);
    }
}
