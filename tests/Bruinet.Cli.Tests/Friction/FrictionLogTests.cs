using Bruinet.Cli.Friction;

namespace Bruinet.Cli.Tests.Friction;

/// <summary>A row added to a suite's friction log: numbered after the last, in the log's own line ends.</summary>
public sealed class FrictionLogTests
{
    private const string Log = "# Friction log\r\n\r\n| # | Date | Stage | What happened | Cost | Workaround | Change | Where |\r\n| --- | --- | --- | --- | --- | --- | --- | --- |\r\n| 1 | 2026-11-14 | 0 Setup | First | 5 min | None | Done: a test | Bruinet |\r\n| 12 | 2026-11-15 | 3 Spec | Twelfth | 1 run | None | Open: a flag | Bruinet |\r\n";

    private static readonly DateOnly Today = new(2026, 11, 17);

    [Fact]
    public void A_row_gets_the_number_after_the_highest_and_goes_after_the_last_row()
    {
        var (markdown, number) = FrictionLog.Add(Log, Today, "6 Run (B7.HRS.04)", "The list counted 0 rows", "1 failed run", "", "Done: either kind of row", "Bouw7");

        Assert.Equal(13, number);
        Assert.EndsWith("| 12 | 2026-11-15 | 3 Spec | Twelfth | 1 run | None | Open: a flag | Bruinet |\r\n| 13 | 2026-11-17 | 6 Run (B7.HRS.04) | The list counted 0 rows | 1 failed run | None | Done: either kind of row | Bouw7 |\r\n", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n| 13", markdown.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void A_cell_s_bars_are_escaped_and_its_lines_joined()
    {
        var row = FrictionLog.Row(2, Today, "5 Scenarios", "A step\nwith a | in it", "2 min", "None", "Open: x", "the suite");

        Assert.Equal("| 2 | 2026-11-17 | 5 Scenarios | A step with a \\| in it | 2 min | None | Open: x | the suite |", row);
    }

    [Fact]
    public void A_log_without_a_table_is_refused_and_a_log_is_found_from_a_folder_below_it()
    {
        Assert.Throws<FormatException>(() => FrictionLog.Add("# Friction log\n", Today, "s", "w", "c", "", "ch", "wh"));

        var suite = Directory.CreateTempSubdirectory("bruinet-friction").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(suite, ".testing"));
            Directory.CreateDirectory(Path.Combine(suite, "Suite.UITests", "Modules"));
            File.WriteAllText(Path.Combine(suite, ".testing", FrictionLog.FileName), Log);

            Assert.Equal(Path.Combine(suite, ".testing", FrictionLog.FileName), FrictionLog.Find(Path.Combine(suite, "Suite.UITests", "Modules")));
        }
        finally
        {
            Directory.Delete(suite, recursive: true);
        }
    }
}
