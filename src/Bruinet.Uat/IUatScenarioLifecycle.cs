namespace Bruinet.Uat;

/// <summary>
/// What a fixture does before a file's scenarios and before each scenario: its test set, a clean
/// start. <see cref="BddScenarioTestBase{TFixture}"/> calls it, and so does a host that runs
/// scenarios without the suite's test class (Presenter), so both start a scenario the same way.
/// </summary>
public interface IUatScenarioLifecycle
{
    /// <summary>Before the first scenario of a file: load what its metadata asks for, such as its <c>TestData</c>.</summary>
    /// <param name="document">The file, as parsed.</param>
    void BeforeFile(BddDocument document);

    /// <summary>Before each scenario: a clean start.</summary>
    /// <param name="scenario">The scenario about to run.</param>
    void BeforeScenario(BddBoundScenario scenario);

    /// <summary>
    /// After each scenario, whether it passed or failed, once its recording is finished: put back
    /// what the scenario changed that the next one, or a test sharing the fixture, must not see.
    /// </summary>
    /// <param name="scenario">The scenario that ran.</param>
    void AfterScenario(BddBoundScenario scenario)
    {
    }

    /// <summary>
    /// Whether <see cref="BeforeScenario"/> starts a scenario clean on the app as it is, with no new
    /// launch, as a test class's scenarios share the fixture: a host's runner may then keep the
    /// fixture and its app between runs (AD-025). False unless the fixture says so.
    /// </summary>
    bool StartsCleanWithoutLaunch => false;
}
