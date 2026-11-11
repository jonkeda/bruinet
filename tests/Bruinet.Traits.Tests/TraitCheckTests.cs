using Bruinet.Traits;

namespace Bruinet.Traits.Tests;

public class TraitCheckTests
{
    private static readonly TraitCheckOptions Options = new() { KnownModules = ["Contacts"], KnownFeatures = ["Checkout"] };

    [Fact]
    public void Check_AcceptsOneTierAndAKnownOwner()
        => Assert.DoesNotContain(Problems(), p => p.Test.EndsWith($"{nameof(Fixtures)}.{nameof(Fixtures.Tagged)}", StringComparison.Ordinal));

    [Fact]
    public void Check_AcceptsAFeatureAsTheOwner()
        => Assert.Empty(ProblemsOf(nameof(Fixtures.FeatureOwned)));

    [Fact]
    public void Check_ReportsAnUnknownFeature()
        => Assert.Contains(ProblemsOf(nameof(Fixtures.UnknownFeature)), p => p.Contains("unknown feature 'Chekout'", StringComparison.Ordinal));

    [Fact]
    public void Check_ReadsPlainTraitsAndTheBaseClass()
        => Assert.DoesNotContain(Problems(), p => p.Test.EndsWith($"{nameof(DerivedFixtures)}.{nameof(DerivedFixtures.PlainTraits)}", StringComparison.Ordinal));

    [Fact]
    public void Check_ReportsAMissingTierAndOwner()
    {
        var problems = ProblemsOf(nameof(UntaggedFixtures.Untagged), typeof(UntaggedFixtures));

        Assert.Contains(problems, p => p.Contains("has no tier", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("has no owner", StringComparison.Ordinal));
    }

    [Fact]
    public void Check_ReportsTwoTiers()
        => Assert.Contains(ProblemsOf(nameof(Fixtures.TwoTiers)), p => p.Contains("more than one tier: Integration, Unit", StringComparison.Ordinal));

    [Fact]
    public void Check_CountsTheOldAndNewUiNameAsOneTier()
        => Assert.Empty(ProblemsOf(nameof(UiFixtures.OldAndNewName), typeof(UiFixtures)));

    [Fact]
    public void Check_AcceptsAPlainOldUiTier()
        => Assert.Empty(ProblemsOf(nameof(UiFixtures.PlainOldName), typeof(UiFixtures)));

    [Fact]
    public void Check_ReportsAnUnknownModule()
        => Assert.Contains(ProblemsOf(nameof(Fixtures.UnknownModule)), p => p.Contains("unknown module 'Contcts'", StringComparison.Ordinal));

    [Fact]
    public void Report_CountsTestsAndProblems()
        => Assert.EndsWith("1 test(s) with 2 trait problem(s).",
            TraitCheck.Report([new("A.B", "x"), new("A.B", "y")]), StringComparison.Ordinal);

    private static IReadOnlyList<TraitProblem> Problems() => TraitCheck.Check(typeof(TraitCheckTests).Assembly, Options);

    private static List<string> ProblemsOf(string method, Type? fixture = null)
        => [.. Problems().Where(p => p.Test == $"{(fixture ?? typeof(Fixtures)).FullName}.{method}").Select(p => p.Problem)];

    // The fixtures are real tests: TraitCheck finds tests the way xUnit does, by their [Fact].
    [Unit]
    public class Fixtures
    {
        [Fact, Module("Contacts")]
        public void Tagged() { }

        [Fact, Integration, Module("Contacts")]
        public void TwoTiers() { }

        [Fact, Module("Contcts")]
        public void UnknownModule() { }

        [Fact, Feature("Checkout")]
        public void FeatureOwned() { }

        [Fact, Feature("Chekout")]
        public void UnknownFeature() { }
    }

    [Module("Contacts")]
    public class UiFixtures
    {
        [Fact, Ui]
        public void OldAndNewName() { }

        [Fact, Trait("Pyramid", "Hermetic UI")]
        public void PlainOldName() { }
    }

    public class UntaggedFixtures
    {
        [Fact]
        public void Untagged() { }
    }

    [Trait("Pyramid", "Unit")]
    public class BaseFixtures;

    [Trait("Module", "Contacts")]
    public class DerivedFixtures : BaseFixtures
    {
        [Fact]
        public void PlainTraits() { }
    }
}
