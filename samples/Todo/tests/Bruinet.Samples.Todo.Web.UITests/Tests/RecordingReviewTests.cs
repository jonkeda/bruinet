using Bruinet.Mocking.Data.Replay;
using Bruinet.Samples.Todo.TestSupport;

namespace Bruinet.Samples.Todo.Web.UITests.Tests;

/// <summary>
/// The gate on re-records (.my/sql-replay/plan.md, step 8): every recording as a person accepted it after reading its
/// summary (bruinet sql diff --recordings Recordings, then bruinet sql accept --recordings Recordings). Not while recording, which rewrites them.
/// </summary>
/// <remarks>A unit test in a UI suite: it reads the recordings, with no app and no database.</remarks>
[Trait("Pyramid", "Unit")]
public sealed class RecordingReviewTests
{
    [Fact]
    public void Recordings_AreAllReviewed()
    {
        if (TodoSqlReplay.ModeOfRun == ReplayMode.Record)
        {
            return;
        }

        Assert.Empty(RecordingReview.Unreviewed(TodoSqlReplay.RecordingsOf("Bruinet.Samples.Todo.Web.UITests")));
    }
}
