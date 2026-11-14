using Bruinet.Scraper.Explore;
using Bruinet.Scraper.Projects;

namespace Bruinet.Scraper.Tests.Explore;

/// <summary>Within: set per run, refused when it is no path, and a start outside it is said before the run.</summary>
public sealed class WithinTests
{
    private static ExploreSettings Read(string within)
        => ScraperConfig.Parse($"## Explore\n\n| Field | Value |\n| --- | --- |\n| Within | {within} |\n").Explore;

    [Theory]
    [InlineData("/contact")]
    [InlineData("/#/orders")]
    [InlineData("https://app.example/contact")]
    public void A_path_a_route_or_an_address_is_a_Within(string within)
        => Assert.Equal(within, Read(within).Within);

    [Fact]
    public void A_folder_a_shell_made_of_a_path_is_refused_saying_why()
    {
        var error = Assert.Throws<FormatException>(() => Read("C:/Program Files/Git/contact"));

        Assert.StartsWith("Line 5: Within is a path", error.Message, StringComparison.Ordinal);
        Assert.Contains("MSYS_NO_PATHCONV", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_run_s_Within_overrides_the_config_s()
        => Assert.Equal("/project", new ExploreOptions { Within = "/project" }.Apply(Read("/contact")).Within);

    [Fact]
    public void A_start_outside_Within_is_said_in_the_summary()
    {
        var summary = ExploreReport.Summary("Shop", "https://app.example/dashboard", Read("/contact"), dryRun: false);

        Assert.Contains("within /contact: the start is OUTSIDE it", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("OUTSIDE", ExploreReport.Summary("Shop", "https://app.example/contact#/", Read("/contact"), dryRun: false), StringComparison.Ordinal);
    }
}
