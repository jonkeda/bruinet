using System.Globalization;

namespace Bruinet.Samples.Todo.UnitTests.Rules;

[Trait("Module", "Todo")]
[Trait("Pyramid", "Unit")]
public sealed class TodoStatusRulesTests
{
    [Theory]
    [Trait("Journey", "TOD.05.1")]
    [InlineData(TodoStatus.Open, TodoStatus.InProgress)]
    [InlineData(TodoStatus.InProgress, TodoStatus.Done)]
    [InlineData(TodoStatus.Done, TodoStatus.Open)]
    public void Next_CyclesThroughTheStatuses(TodoStatus from, TodoStatus to)
        => Assert.Equal(to, TodoStatusRules.Next(from));

    // The rule table for TOD.05.2: status x due date x today (2026-03-10).
    [Theory]
    [Trait("Journey", "TOD.05.2")]
    [InlineData(TodoStatus.Open, null, DisplayStatus.Open)]
    [InlineData(TodoStatus.Open, "2026-03-09", DisplayStatus.Overdue)]
    [InlineData(TodoStatus.Open, "2026-03-10", DisplayStatus.Open)]
    [InlineData(TodoStatus.Open, "2026-03-11", DisplayStatus.Open)]
    [InlineData(TodoStatus.InProgress, null, DisplayStatus.InProgress)]
    [InlineData(TodoStatus.InProgress, "2026-01-01", DisplayStatus.Overdue)]
    [InlineData(TodoStatus.InProgress, "2026-03-10", DisplayStatus.InProgress)]
    [InlineData(TodoStatus.Done, null, DisplayStatus.Done)]
    [InlineData(TodoStatus.Done, "2026-01-01", DisplayStatus.Done)]
    [InlineData(TodoStatus.Done, "2026-03-11", DisplayStatus.Done)]
    public void Display_IsOverdueOnlyWhenNotDoneAndPastDue(TodoStatus status, string? due, DisplayStatus expected)
    {
        DateOnly? dueDate = due is null ? null : DateOnly.Parse(due, CultureInfo.InvariantCulture);

        Assert.Equal(expected, TodoStatusRules.Display(status, dueDate, Build.Today));
    }

    [Theory]
    [Trait("Journey", "TOD.05.2")]
    [InlineData(DisplayStatus.Open, "○", "Open")]
    [InlineData(DisplayStatus.InProgress, "◐", "In progress")]
    [InlineData(DisplayStatus.Done, "●", "Done")]
    [InlineData(DisplayStatus.Overdue, "⚠", "Overdue")]
    public void GlyphAndText_NameEveryDisplayStatus(DisplayStatus status, string glyph, string text)
    {
        Assert.Equal(glyph, TodoStatusRules.Glyph(status));
        Assert.Equal(text, TodoStatusRules.Text(status));
    }
}

[Trait("Module", "Todo")]
[Trait("Pyramid", "Unit")]
public sealed class TodoValidationTests
{
    [Theory]
    [Trait("Journey", "TOD.02.3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t \r\n")]
    [Trait("Rule", "TOD.02.R2")]
    public void ValidateTitle_EmptyOrWhitespace_IsRequired(string? title)
        => Assert.Equal(TodoValidation.TitleRequired, TodoValidation.ValidateTitle(title));

    [Theory]
    [Trait("Journey", "TOD.02.4")]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    [InlineData(500, false)]
    [Trait("Rule", "TOD.02.R3")]
    public void ValidateTitle_AllowsAtMost100Characters(int length, bool valid)
    {
        var error = TodoValidation.ValidateTitle(new string('x', length));

        Assert.Equal(valid, error is null);
        if (!valid)
        {
            Assert.Equal(TodoValidation.TitleTooLong, error);
        }
    }

    [Fact]
    [Trait("Journey", "TOD.02.4")]
    [Trait("Rule", "TOD.02.R3")]
    public void ValidateTitle_MeasuresAfterTrimming()
        => Assert.Null(TodoValidation.ValidateTitle("  " + new string('x', 100) + "  "));
}

[Trait("Module", "Todo")]
[Trait("Pyramid", "Unit")]
public sealed class TodoListRulesTests
{
    [Theory]
    [Trait("Journey", "TOD.01.4")]
    [InlineData(TodoFilter.All, new[] { "Busy one", "Open one", "Done one" })]
    [InlineData(TodoFilter.Open, new[] { "Open one" })]
    [InlineData(TodoFilter.InProgress, new[] { "Busy one" })]
    [InlineData(TodoFilter.Done, new[] { "Done one" })]
    public void Arrange_KeepsOnlyTheFilteredStatus(TodoFilter filter, string[] expected)
    {
        var items = new[]
        {
            Build.Todo("Open one", TodoStatus.Open),
            Build.Todo("Busy one", TodoStatus.InProgress),
            Build.Todo("Done one", TodoStatus.Done),
        };

        Assert.Equal(expected, TodoListRules.Arrange(items, filter).Select(item => item.Title));
    }

    [Fact]
    [Trait("Journey", "TOD.01.5")]
    public void Arrange_OrdersNotDoneFirst_ThenByDueDate_UndatedLast_ThenByTitle()
    {
        var items = new[]
        {
            Build.Todo("Done early", TodoStatus.Done, new DateOnly(2026, 1, 1)),
            Build.Todo("Undated b"),
            Build.Todo("Due later", due: new DateOnly(2026, 4, 1)),
            Build.Todo("Undated a"),
            Build.Todo("Due soon", TodoStatus.InProgress, new DateOnly(2026, 3, 11)),
        };

        Assert.Equal(
            ["Due soon", "Due later", "Undated a", "Undated b", "Done early"],
            TodoListRules.Arrange(items, TodoFilter.All).Select(item => item.Title));
    }

    [Fact]
    [Trait("Journey", "TOD.06.2")]
    public void Arrange_LeavesOutTombstones()
        => Assert.Empty(TodoListRules.Arrange([Build.Todo(deleted: true)], TodoFilter.All));

    // The state table behind TOD.01.1, TOD.01.3, TOD.08.1 and TOD.09.4.
    [Theory]
    [Trait("Journey", "TOD.01.1")]
    [InlineData(true, 0, SyncError.None, ListState.Loading)]
    [InlineData(true, 3, SyncError.None, ListState.Content)]
    [InlineData(false, 3, SyncError.None, ListState.Content)]
    [InlineData(false, 0, SyncError.None, ListState.Empty)]
    [InlineData(false, 3, SyncError.ServerError, ListState.Error)]
    [InlineData(false, 0, SyncError.Unauthorized, ListState.Error)]
    [InlineData(false, 3, SyncError.Unreachable, ListState.Content)]
    [InlineData(false, 3, SyncError.Timeout, ListState.Content)]
    [InlineData(false, 0, SyncError.Offline, ListState.Empty)]
    public void StateFor_ShowsErrorOnlyForServerAndKeyFailures(bool loading, int count, SyncError error, ListState expected)
        => Assert.Equal(expected, TodoListRules.StateFor(loading, count, error));

    [Fact]
    [Trait("Journey", "TOD.08.5")]
    public void ErrorMessage_ForARefusedKey_SaysSo()
        => Assert.Contains("refused the app's key", TodoListRules.ErrorMessage(SyncError.Unauthorized), StringComparison.Ordinal);
}

[Trait("Module", "Todo")]
[Trait("Pyramid", "Unit")]
public sealed class TodoFormattingTests
{
    // Patterns pinned rather than taken from the OS, whose culture data differs between machines.
    private static readonly CultureInfo Dutch = WithPatterns("nl-NL", "d-M-yyyy", "HH:mm");
    private static readonly CultureInfo American = WithPatterns("en-US", "M/d/yyyy", "h:mm tt");

    [Fact]
    [Trait("Journey", "TOD.03.2")]
    public void Due_UsesTheDevicesDateFormat()
    {
        Assert.Equal("Due 12-3-2026", TodoFormatting.Due(new DateOnly(2026, 3, 12), Dutch));
        Assert.Equal("Due 3/12/2026", TodoFormatting.Due(new DateOnly(2026, 3, 12), American));
        Assert.Equal(string.Empty, TodoFormatting.Due(null, Dutch));
    }

    [Fact]
    [Trait("Journey", "TOD.03.2")]
    public void Timestamp_ShowsStoredUtcInTheDevicesZone()
    {
        var amsterdam = TimeZoneInfo.CreateCustomTimeZone("UTC+1", TimeSpan.FromHours(1), "UTC+1", "UTC+1");

        Assert.Equal("10-3-2026 10:00", TodoFormatting.Timestamp(Build.Now, amsterdam, Dutch));
    }

    [Theory]
    [Trait("Journey", "TOD.03.3")]
    [InlineData(SyncState.Synced, "Synced")]
    [InlineData(SyncState.Pending, "Waiting to sync")]
    [InlineData(SyncState.LocalOnly, "Local only")]
    public void SyncState_NamesEachState(SyncState state, string text)
        => Assert.Equal(text, TodoFormatting.SyncState(state));

    [Theory]
    [Trait("Journey", "TOD.08.8")]
    [InlineData(false, SyncError.None, "Offline")]
    [InlineData(true, SyncError.Unreachable, "Server unreachable")]
    [InlineData(true, SyncError.Timeout, "Sync timed out")]
    [InlineData(true, SyncError.Unauthorized, "Not authorised")]
    [InlineData(true, SyncError.ServerError, "Sync failed")]
    [InlineData(true, SyncError.None, "Synced 09:00")]
    public void SyncLine_SaysWhatTheLastSyncDid(bool online, SyncError error, string expected)
        => Assert.Equal(
            expected,
            TodoFormatting.SyncLine(online, new SyncResult(error, Build.Now), TimeZoneInfo.Utc, CultureInfo.InvariantCulture));

    [Fact]
    [Trait("Journey", "TOD.07.2")]
    public void SyncLine_BeforeAnySync_SaysNotSyncedYet()
        => Assert.Equal("Not synced yet", TodoFormatting.SyncLine(true, null, culture: CultureInfo.InvariantCulture));

    private static CultureInfo WithPatterns(string name, string date, string time)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(name).Clone();
        culture.DateTimeFormat.ShortDatePattern = date;
        culture.DateTimeFormat.ShortTimePattern = time;
        return culture;
    }
}
