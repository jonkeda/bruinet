using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Uat.Tests;

/// <summary>A <c>[BddScenarioTheory]</c> case: its <c>BddScenario</c> trait, through serialization too.</summary>
public sealed class UatScenarioTheoryTests
{
    private static readonly string Output = Path.GetDirectoryName(BddScenarioSource.GetConfigFilePath())!;

    [Fact]
    public void ACase_HasItsScenariosAddress_AsTheUatScenarioTrait()
    {
        var testCase = Case(Path.Combine(Output, "Features", "Users", "TOD.11-manage-users.feature.md"), "TOD.11.1 Users list");

        Assert.Equal(["Features/Users/TOD.11-manage-users.feature.md#TOD.11.1-Users-list"], testCase.Traits[BddScenarioAddress.Trait]);
    }

    [Fact]
    public void TheTrait_SurvivesSerialization()
    {
        var testCase = Case(Path.Combine(Output, "Features", "shelf.feature.md"), "Shelve a book [2]");

        var copy = SerializationHelper.Deserialize<IXunitTestCase>(SerializationHelper.Serialize(testCase))!;

        Assert.Equal(["Features/shelf.feature.md#Shelve-a-book-2"], copy.Traits[BddScenarioAddress.Trait]);
    }

    [Fact]
    public void AFileThatDoesNotParse_HasNoTrait_AndStillRuns()
    {
        var testCase = Case(Path.Combine(Output, "Features", "broken.feature.md"), string.Empty);

        Assert.False(testCase.Traits.ContainsKey(BddScenarioAddress.Trait));
    }

    [Fact]
    public void TheDiscoverer_IsNamedOnTheAttribute()
    {
        var discoverer = typeof(BddScenarioTheoryAttribute).GetCustomAttributes(typeof(XunitTestCaseDiscovererAttribute), inherit: false)
            .Cast<XunitTestCaseDiscovererAttribute>()
            .Single();

        Assert.NotNull(Type.GetType($"{typeof(UatScenarioTheoryDiscoverer).FullName}, {typeof(UatScenarioTheoryDiscoverer).Assembly.GetName().Name}"));
        Assert.Equal(typeof(TheoryAttribute), typeof(BddScenarioTheoryAttribute).BaseType);
        Assert.NotNull(discoverer);
    }

    private static UatScenarioTestCase Case(string file, string scenario)
    {
        var assembly = new TestAssembly(Reflector.Wrap(typeof(Scenarios).Assembly));
        var collection = new TestCollection(assembly, null, "Scenarios");
        var testClass = new TestClass(collection, Reflector.Wrap(typeof(Scenarios)));
        var method = new TestMethod(testClass, Reflector.Wrap(typeof(Scenarios).GetMethod(nameof(Scenarios.Scenario_Passes))!));
        return new UatScenarioTestCase(new NullMessageSink(), TestMethodDisplay.ClassAndMethod, TestMethodDisplayOptions.None, method, [file, scenario]);
    }

    public sealed class Scenarios
    {
        [BddScenarioTheory]
        [InlineData("x.feature.md", "x")]
        public void Scenario_Passes(string filePath, string scenarioName)
        {
            Assert.NotNull(filePath);
            Assert.NotNull(scenarioName);
        }
    }
}
