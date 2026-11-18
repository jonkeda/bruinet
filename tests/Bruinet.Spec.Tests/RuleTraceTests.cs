using Bruinet.Spec;

namespace Bruinet.Spec.Tests;

/// <summary>
/// The work check on a small spec of three rules and test sources written here: a C# test names a
/// rule by a trait, a scenario by a tag.
/// </summary>
public sealed class RuleTraceTests
{
    private const string Spec = """
        # Spec: ABC.01 Do a thing

        ## Rules

        | Id | Rule |
        | --- | --- |
        | ABC.01.R1 | One. |
        | ABC.01.R2 | Two. |
        | ABC.01.R3 | Three. |
        """;

    private const string Code = """
        public sealed class ThingTests
        {
            [Fact]
            [Trait("Journey", "ABC.01.1")]
            [Trait("Rule", "ABC.01.R1")]
            public void One_Holds()
            {
            }

            [Theory]
            [Trait("Rule", "ABC.01.R1")]
            [Trait("Rule", "ABC.01.R2")]
            [InlineData(1)]
            public async Task OneAndTwo_Hold(int value)
            {
            }
        }
        """;

    private const string Scenario = """
        # Feature: ABC.01 Do a thing

        @ABC.01.1 @ABC.01.R3 @web
        ## Scenario: ABC.01.1 Three holds on the screen

        ```gherkin
        Then it holds
        ```
        """;

    [Fact]
    public void EveryRuleReached_HasNoFindings()
    {
        var result = RuleTrace.Trace(Spec, [("ThingTests.cs", Code), ("ABC.01.feature.md", Scenario)]);

        Assert.Empty(result.Findings);
        Assert.Equal(["One_Holds", "OneAndTwo_Hold"], result.Rules["ABC.01.R1"].Select(reach => reach.Name));
        Assert.Equal(["OneAndTwo_Hold"], result.Rules["ABC.01.R2"].Select(reach => reach.Name));
        Assert.Equal(["ABC.01.1 Three holds on the screen"], result.Rules["ABC.01.R3"].Select(reach => reach.Name));
    }

    [Fact]
    public void ARuleWithoutATest_IsFound()
    {
        var result = RuleTrace.Trace(Spec, [("ThingTests.cs", Code)]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("rule-untested", finding.Code);
        Assert.Contains("ABC.01.R3", finding.Message);
        Assert.Empty(result.Rules["ABC.01.R3"]);
    }

    [Fact]
    public void ATestNamingARuleTheSpecDoesNotHave_IsFound()
    {
        var code = Code.Replace("[Trait(\"Rule\", \"ABC.01.R2\")]", "[Trait(\"Rule\", \"ABC.01.R9\")]");

        var codes = RuleTrace.Trace(Spec, [("ThingTests.cs", code), ("ABC.01.feature.md", Scenario)]).Findings.Select(finding => finding.Code);

        Assert.Equal(["rule-unknown", "rule-untested"], codes);
    }

    [Fact]
    public void AnotherJourneysRules_AreLeftAlone()
    {
        var scenario = Scenario.Replace("@ABC.01.R3", "@ABC.01.R3 @ABC.02.R7");

        Assert.Empty(RuleTrace.Trace(Spec, [("ThingTests.cs", Code), ("ABC.01.feature.md", scenario)]).Findings);
    }

    [Fact]
    public void AReach_SaysItsFileAndLine()
    {
        var reach = RuleTrace.Trace(Spec, [("ABC.01.feature.md", Scenario)]).Rules["ABC.01.R3"].Single();

        Assert.Equal(("ABC.01.feature.md", 3), (reach.File, reach.Line));
    }

    private const string Trx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Ns.ThingTests.One_Holds" outcome="Passed" />
            <UnitTestResult testName="Ns.ThingTests.OneAndTwo_Hold(value: 1)" outcome="Failed" />
            <UnitTestResult testName="Ns.OtherTests.One_Holds" outcome="Failed" />
          </Results>
        </TestRun>
        """;

    [Fact]
    public void Results_GiveEachRuleAState()
    {
        var trace = RuleTrace.Trace(Spec, [("ThingTests.cs", Code), ("ABC.01.feature.md", Scenario)]);

        var states = RuleResults.States(trace, RuleResults.ParseTrx(Trx).ToList());

        Assert.Equal(RuleState.Failing, states["ABC.01.R1"]);
        Assert.Equal(RuleState.Failing, states["ABC.01.R2"]);
        Assert.Equal(RuleState.NotRun, states["ABC.01.R3"]);
    }

    [Fact]
    public void A_test_is_matched_by_its_class_not_only_its_method()
    {
        var trace = RuleTrace.Trace(Spec, [("ThingTests.cs", Code)]);
        var outcomes = new[]
        {
            new TestOutcome("Ns.ThingTests.One_Holds", "Passed"),
            new TestOutcome("Ns.ThingTests.OneAndTwo_Hold(value: 1)", "Passed"),
            new TestOutcome("Ns.OtherTests.One_Holds", "Failed"),
        };

        var reach = Assert.Single(trace.Rules["ABC.01.R1"], r => r.Name == "One_Holds");
        Assert.Equal("ThingTests", reach.Container);
        Assert.Equal(RuleState.Passing, RuleResults.States(trace, outcomes)["ABC.01.R1"]);
        Assert.Equal(RuleState.Passing, RuleResults.States(trace, outcomes)["ABC.01.R2"]);
        Assert.Equal(RuleState.NotRun, RuleResults.States(trace, outcomes.Take(1).ToList())["ABC.01.R1"]);
    }

    [Fact]
    public void A_scenario_is_matched_by_its_name_in_the_tests_arguments()
    {
        var trace = RuleTrace.Trace(Spec, [("ABC.01.feature.md", Scenario)]);
        var outcomes = new[] { new TestOutcome("Ns.UatTests.Scenario_Passes(filePath: \"ABC.01.feature.md\", scenarioName: \"ABC.01.1 Three holds on the screen\")", "Passed") };

        Assert.Equal(RuleState.Passing, RuleResults.States(trace, outcomes)["ABC.01.R3"]);
        Assert.Equal(RuleState.NotReached, RuleResults.States(trace, outcomes)["ABC.01.R1"]);
    }

    [Fact]
    public void A_runnable_specs_examples_are_its_tests_and_match_their_cases_by_rule_id()
    {
        const string runnable = Spec + """

            ## Examples

            ### Chapter: At a glance

            #### R1 One

            #### R10 Ten
            """;
        var trace = RuleTrace.Trace(runnable, [("ABC.01.spec.md", RuleTrace.SpecAsSource(runnable))]);
        var outcomes = new[]
        {
            new TestOutcome("Ns.UatTests.Scenario_Passes(filePath: \"Specs/ABC.01.spec.md\", scenarioName: \"ABC.01.R1 One\")", "Passed"),
        };

        Assert.Single(trace.Rules["ABC.01.R1"]);
        Assert.Equal(RuleState.Passing, RuleResults.States(trace, outcomes)["ABC.01.R1"]);
        Assert.Equal(RuleState.NotReached, RuleResults.States(trace, outcomes)["ABC.01.R2"]);
    }
}
