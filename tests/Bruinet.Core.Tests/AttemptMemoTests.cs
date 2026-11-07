using System.Drawing;
using Bruinet.Core.Calls;
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
}
