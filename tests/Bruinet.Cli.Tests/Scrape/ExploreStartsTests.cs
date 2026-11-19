using Bruinet.Scraper.Host;
using Xunit;

namespace Bruinet.Cli.Tests.Scrape;

/// <summary>Several starts of one explore command: each run is kept within its own start's path.</summary>
public sealed class ExploreStartsTests
{
    [Theory]
    [InlineData("https://app.example/contact#/", "/contact")]
    [InlineData("https://app.example/hour-log?week=3#/approval", "/hour-log")]
    [InlineData("https://app.example/report/liquidity", "/report/liquidity")]
    [InlineData("/planning/projects", "/planning/projects")]
    [InlineData("/kam#/qhse", "/kam")]
    [InlineData("https://app.example/", "/")]
    [InlineData("contact", null)]
    [InlineData(null, null)]
    public void A_start_is_kept_within_its_own_path(string? start, string? expected)
        => Assert.Equal(expected, ScrapeCommands.WithinOf(start));
}
