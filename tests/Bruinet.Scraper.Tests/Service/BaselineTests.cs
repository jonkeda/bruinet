using Bruinet.Scraper.Factory;
using Bruinet.Scraper.Testing;

namespace Bruinet.Scraper.Tests.Service;

// What passed before a change must pass after it: the guard of code nobody protects any more.
public sealed class BaselineTests
{
    private static TestOutcome T(string name, bool passed, string? message = null) => new("ATests", name, passed, message, 1);

    [Fact]
    public void A_test_that_passed_and_fails_now_is_broken_with_what_it_says_now()
    {
        var broken = Baseline.Broken([T("A", true), T("B", true)], [T("A", true), T("B", false, "The form does not show 'Name'.")]);

        var one = Assert.Single(broken);
        Assert.Equal(("B", "The form does not show 'Name'."), (one.Name, one.Message));
    }

    [Fact]
    public void A_test_that_no_longer_runs_is_broken_and_one_that_failed_before_is_not_the_change_s()
    {
        var broken = Baseline.Broken([T("Gone", true), T("Red before", false, "it was red")], [T("Red before", false, "still red")]);

        var one = Assert.Single(broken);
        Assert.Equal("Gone", one.Name);
        Assert.Contains("did not run after the change", one.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_broken_when_what_passed_still_passes_and_a_new_test_is_not_held_against_the_baseline()
    {
        Assert.Empty(Baseline.Broken([T("A", true)], [T("A", true), T("New", false, "a new test's own failure")]));
        Assert.Empty(Baseline.Broken([], [T("A", false)]));
    }
}
