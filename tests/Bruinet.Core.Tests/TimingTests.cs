using Bruinet.Core.Logging;
using Bruinet.Core.Testing;

namespace Bruinet.Core.Tests;

// A timings file: a header and the clock row, then one row per call, query or span, each a CSV line a reader can split.
public sealed class TimingTests
{
    private static readonly DateTime Zero = new(2026, 11, 7, 8, 0, 0, DateTimeKind.Utc);

    private readonly StringWriter _text = new();

    private string[] Rows => _text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void AFile_StartsWithTheHeader_AndTheClockRowAtZero()
    {
        _ = new TimingFile(_text, "run1", "maui", Zero);

        Assert.Equal(TimingFile.Header, Rows[0]);
        Assert.Equal("run1;maui;;;;clock;2026-11-07T08:00:00.0000000Z;;;;0;0;;", Rows[1]);
    }

    [Fact]
    public void ARow_CarriesItsTestScenarioAndStep_AndKeepsSeparatorsAndLineBreaksOutOfItsCells()
    {
        var file = new TimingFile(_text, "run1", "maui", Zero);

        file.Write(new TestScope("Saves", "A;B", "Given\r\nthe list"), "call", "Click", "Edit", "Save", "Click", 120, 35, 2, "ok");

        Assert.Equal("run1;maui;Saves;A,B;Given  the list;call;Click;Edit;Save;Click;120;35;2;ok", Rows[2]);
        Assert.Equal(TimingFile.Header.Split(';').Length, Rows[2].Split(';').Length);
    }

    [Fact]
    public void ASpan_IsWrittenOnce_WithItsDurationFromTheClock()
    {
        var file = new TimingFile(_text, "run1", "maui", Zero);
        long now = 100;

        var span = file.Span("step", "Given the list", TestScope.Empty, () => now);
        now = 250;
        span.Dispose();
        now = 900;
        span.Dispose();

        Assert.Equal(3, Rows.Length);
        Assert.Equal("run1;maui;;;;step;Given the list;;;;100;150;;", Rows[2]);
    }
}
