using System.Text.Json;
using Bruinet.Core.Configuration;

namespace Bruinet.Core.Tests;

/// <summary>
/// Time settings are TimeSpans (public API review, proposal 9); settings files written when they
/// were int milliseconds still load.
/// </summary>
public sealed class TimeoutSettingsJsonTests
{
    [Fact]
    public void A_number_is_milliseconds()
    {
        var settings = JsonSerializer.Deserialize<TimeoutSettings>("""{ "PollingInterval": 250, "Animation": 0 }""")!;

        Assert.Equal(TimeSpan.FromMilliseconds(250), settings.PollingInterval);
        Assert.Equal(TimeSpan.Zero, settings.Animation);
    }

    [Theory]
    [InlineData("\"00:00:02\"", 2000)]
    [InlineData("\"1500\"", 1500)]
    public void A_string_is_a_TimeSpan_or_milliseconds(string json, int expectedMs)
    {
        var settings = JsonSerializer.Deserialize<TimeoutSettings>($$"""{ "PollingInterval": {{json}} }""")!;

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), settings.PollingInterval);
    }

    [Fact]
    public void Anything_else_says_what_it_takes()
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TimeoutSettings>("""{ "PollingInterval": true }"""));

        Assert.Contains("number of milliseconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void It_writes_milliseconds()
    {
        var json = JsonSerializer.Serialize(new TimeoutSettings { PollingInterval = TimeSpan.FromMilliseconds(75) });

        Assert.Contains("\"PollingInterval\":75", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ToMs_rounds_to_whole_milliseconds()
        => Assert.Equal(3, TimeSpan.FromMilliseconds(2.6).ToMs());
}
