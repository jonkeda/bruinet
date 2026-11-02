using Xunit;

namespace Bruinet.Bdd.Tests;

/// <summary>A suite's <c>## Dependencies</c> table: a mode per dependency, a run's override over a row, the hermetic check.</summary>
public sealed class BddDependenciesTests
{
    private const string Config = """
        # UAT Config

        ## Runtime

        | Field | Value |
        | --- | --- |
        | Head | headless |
        | Hermetic | true |
        | HttpRedact | x-functions-key |

        ## Dependencies

        | Name | Kind | Mode | Where |
        | --- | --- | --- | --- |
        | backend | http | replay | `https://api.example.com` |
        | maps | http | stub | `MapsStubs` |
        | db | sqlite | copy | `TestData/seed.db` |
        | model | http | replay | `https://*.openai.azure.com`, preset `model` |
        """;

    [Fact]
    public void The_table_gives_each_dependency_its_kind_mode_and_where()
    {
        var config = BddConfigParser.Parse(Config);

        Assert.Equal(
            [new BddDependency("backend", "http", "replay", "https://api.example.com"), new BddDependency("maps", "http", "stub", "MapsStubs"), new BddDependency("db", "sqlite", "copy", "TestData/seed.db"), new BddDependency("model", "http", "replay", "https://*.openai.azure.com, preset model")],
            config.Dependencies);
        Assert.True(BddDependencies.IsHermetic(config));
        Assert.Equal("x-functions-key", config.Runtime[BddReplayFields.HttpRedact]);
        Assert.Empty(BddDependencies.Problems(config, _ => null));
    }

    [Fact]
    public void A_runs_override_changes_one_row_and_a_hermetic_suite_refuses_a_real_dependency()
    {
        var config = BddConfigParser.Parse(Config);
        string? Environment(string name) => name == "BRUINET_DEP_BACKEND" ? "Real" : null;

        var backend = BddDependencies.Find(config, "Backend", Environment);

        Assert.Equal("real", backend!.Mode);
        Assert.Equal("copy", BddDependencies.Find(config, "db", Environment)!.Mode);
        Assert.Contains("backend is 'real' in a hermetic suite", Assert.Single(BddDependencies.Problems(config, Environment)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_mode_its_kind_does_not_take_is_named()
    {
        var config = BddConfigParser.Parse(Config.Replace("| db | sqlite | copy |", "| db | sqlite | stub |", StringComparison.Ordinal));

        Assert.Equal("Dependency db: a sqlite dependency takes real, copy, record, replay, not 'stub'.", Assert.Single(BddDependencies.Problems(config, _ => null)));
    }

    [Fact]
    public void A_scenario_that_reads_its_stub_back_is_skipped_only_while_replaying()
    {
        var config = BddConfigParser.Parse(Config);

        var replaying = config.EvaluateSkip(["@needs-stub"], name => name == BddReplayFields.HttpReplayVariable ? "Replay" : null);

        Assert.True(replaying.ShouldSkip);
        Assert.Contains("reads its backend stub back", replaying.Reason, StringComparison.Ordinal);
        Assert.False(config.EvaluateSkip(["@needs-stub"], name => name == BddReplayFields.HttpReplayVariable ? "Record" : null).ShouldSkip);
        Assert.False(config.EvaluateSkip(["@needs-stub"], _ => null).ShouldSkip);
    }

    [Fact]
    public void A_suite_without_the_table_has_no_dependencies()
    {
        var config = BddConfigParser.Parse("# UAT Config\n\n## Runtime\n\n| Field | Value |\n| --- | --- |\n| Head | web |\n");

        Assert.Empty(config.Dependencies);
        Assert.False(BddDependencies.IsHermetic(config));
    }
}
