using System.Drawing;
using Bruinet.Core.Calls;
using Bruinet.Core.Exceptions;
using Bruinet.Core.Recording;

namespace Bruinet.Core.Tests;

/// <summary>What an attempt keeps, and what ends it before the attempt does.</summary>
[Collection(InteractionLog.Name)]
public sealed class AttemptMemoTests
{
    private readonly object _element = new();

    [Fact]
    public void AnActionInTheAttempt_ForgetsWhatWasRead()
    {
        using (AttemptMemo.Begin())
        {
            _ = AttemptMemo.Remember(_element, "Visible", () => false);

            using (Interactions.Report(InteractionKind.Tap, () => Rectangle.Empty))
            {
            }

            Assert.False(AttemptMemo.Knows(_element, "Visible"));
        }
    }

    [Fact]
    public void AWaitInsideTheAttempt_ReadsAfresh_OnEachPoll()
    {
        using (AttemptMemo.Begin())
        {
            _ = AttemptMemo.Remember(_element, "Visible", () => false);
            var reads = 0;

            var shown = Settle.Until(() => AttemptMemo.Remember(_element, "Visible", () => ++reads >= 2), windowMs: 2000, pollingIntervalMs: 1);

            Assert.True(shown);
            Assert.Equal(2, reads);
        }
    }

    [Fact]
    public void ANestedCallsFirstAttempt_UsesWhatTheEnclosingAttemptRead()
    {
        using (AttemptMemo.Begin())
        {
            _ = AttemptMemo.Remember(_element, "Rect", () => 1);
            var reads = 0;

            using (AttemptMemo.Begin(inherit: true))
            {
                Assert.Equal(1, AttemptMemo.Remember(_element, "Rect", () => ++reads));
            }

            using (AttemptMemo.Begin())
            {
                Assert.Equal(1, AttemptMemo.Remember(_element, "Rect", () => ++reads));
            }

            Assert.Equal(1, reads);
        }
    }

    [Fact]
    public void AnActionInANestedCall_ForgetsWhatTheEnclosingAttemptRead()
    {
        using (AttemptMemo.Begin())
        {
            _ = AttemptMemo.Remember(_element, "Visible", () => false);

            using (AttemptMemo.Begin(inherit: true))
            {
                using (Interactions.Report(InteractionKind.Tap, () => Rectangle.Empty))
                {
                }
            }

            Assert.False(AttemptMemo.Knows(_element, "Visible"));
        }
    }

    [Fact]
    public void AStaleRead_IsReportedAgain_WithoutReadingAgain()
    {
        using (AttemptMemo.Begin())
        {
            var reads = 0;
            int Read() => ++reads > 0 ? throw new StaleElementException() : 0;

            Assert.Throws<StaleElementException>(() => AttemptMemo.Remember(_element, "Rect", Read));
            Assert.Throws<StaleElementException>(() => AttemptMemo.Remember(_element, "Rect", Read));

            Assert.Equal(1, reads);
            Assert.False(AttemptMemo.Knows(_element, "Rect"));
        }
    }
}
