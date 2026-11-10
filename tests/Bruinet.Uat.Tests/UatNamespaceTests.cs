using Bruinet.Bdd;
using System.Reflection;
using System.Reflection.Emit;
using Bruinet.Core.Composition;
using Bruinet.Core.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bruinet.Uat.Tests.NamespaceCases;

/// <summary>Folder namespaces: a scenario sees the phrases of its folder and the folders above it.</summary>
public sealed class UatNamespaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bruinet-namespaces-" + Guid.NewGuid().ToString("N"), "Features");

    public void Dispose()
    {
        if (Directory.Exists(Path.GetDirectoryName(_root)))
        {
            Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "Accounts", true)]
    [InlineData("", "", true)]
    [InlineData("Invoicing", "Invoicing", true)]
    [InlineData("Invoicing", "invoicing/Credit notes", true)]
    [InlineData("Invoicing/Credit notes", "Invoicing", false)]
    [InlineData("Invoicing", "Invoicing extra", false)]
    [InlineData("Invoicing", "", false)]
    [InlineData(@"Invoicing\", "Invoicing", true)]
    public void AFolder_SeesItsOwnPhrases_AndThoseOfTheFoldersAboveIt(string? owner, string from, bool visible)
        => Assert.Equal(visible, BddNamespaces.IsVisible(owner, from));

    [Fact]
    public void AScenario_BindsToThePhraseClassOfItsFolder_FromBelowIt()
    {
        var invocation = Assert.Single(BindOk("Invoicing/Credit notes/credit.feature.md", "When I add a line \"Rent\""));

        Assert.EndsWith("InvoicingPhrases.AddLine", invocation.Command.CommandId, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameTextInTwoSiblingFolders_IsNotAmbiguous_EachFileSeesItsOwn()
    {
        var invoicing = Assert.Single(BindOk("Invoicing/invoice.feature.md", "When I add a line \"Rent\""));
        var purchasing = Assert.Single(BindOk("Purchasing/order.feature.md", "When I add a line \"Rent\""));

        Assert.EndsWith("InvoicingPhrases.AddLine", invoicing.Command.CommandId, StringComparison.Ordinal);
        Assert.EndsWith("PurchasingPhrases.AddLine", purchasing.Command.CommandId, StringComparison.Ordinal);
    }

    [Fact]
    public void APhraseOfAnotherFolder_FailsBinding_NamingWhereItIs()
    {
        var error = BindError("Accounts/account.feature.md", "When I add a line \"Rent\"");

        Assert.Equal("UATB010", error.Code);
        Assert.Contains("'I add a line {text}' of the folder 'Invoicing'", error.Message, StringComparison.Ordinal);
        Assert.Contains("'I add a line {text}' of the folder 'Purchasing'", error.Message, StringComparison.Ordinal);
        Assert.Contains("which the folder 'Accounts' does not see", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderPhrase_AndAGlobalOne_ForOneStep_AreAmbiguousInThatFolder_AndNotAtTheRoot()
    {
        Assert.Single(BindOk("root.feature.md", "When I note \"paid\""));

        var error = BindError("Invoicing/invoice.feature.md", "When I note \"paid\"");

        Assert.Equal("UATB002", error.Code);
    }

    [Fact]
    public void PhraseFiles_AreSeenFromTheFilesFolderUpToTheRoot_RootFirst_NotFromSiblings()
    {
        Write("Common.steps.md", Phrases("a fresh ledger", "When I note \"fresh\""));
        Write("Invoicing/Invoicing.steps.md", Phrases("an invoice {title}", "When I add a line <title>"));
        Write("Accounts/Accounts.steps.md", Phrases("an account", "When I note \"account\""));
        var scenario = Write("Invoicing/Credit notes/credit.feature.md", Scenario("Given a fresh ledger"));

        var files = BddScenarioFiles.PhraseFilesFor(scenario);
        var loaded = BddScenarioFiles.Load(scenario);

        Assert.Equal(["Common.steps.md", "Invoicing.steps.md"], files.Select(Path.GetFileName));

        // Every folder's callables are loaded, each in its namespace; the binder picks those seen.
        Assert.Equal(
            [("a fresh ledger", ""), ("an account", "Accounts"), ("an invoice {title}", "Invoicing")],
            loaded.Callables.Callables.Select(c => (c.Phrase, c.Namespace)).OrderBy(c => c.Phrase, StringComparer.Ordinal));
    }

    [Fact]
    public void ABrokenPhraseFileInAnotherFolder_DoesNotFailTheScenario()
    {
        Write("Accounts/Accounts.steps.md", "## Step: headless\n\n```gherkin\nWhen I note \"x\"\n```\n");

        var there = BddScenarioFiles.Load(Write("Accounts/account.feature.md", Scenario("When I note \"paid\"")));
        var here = BddScenarioFiles.Load(Write("Invoicing/invoice.feature.md", Scenario("When I add a line \"Rent\"")));

        Assert.Contains(there.Diagnostics, d => d.Code == "UAT028");
        Assert.True(here.Success, BddDiagnosticsFormatter.FormatDiagnostics(here.Diagnostics));
    }

    [Fact]
    public void ACallableOfTheFolder_BindsItsStepsThere_AndASiblingsCallableIsNotSeen()
    {
        Write("Common.steps.md", Phrases("a fresh ledger", "When I note \"fresh\""));
        Write("Invoicing/Invoicing.steps.md", Phrases("an invoice {title}", "When I add a line <title>"));
        Write("Accounts/Accounts.steps.md", Phrases("an account", "When I note \"account\""));

        var bound = BindFile("Invoicing/invoice.feature.md", "Given a fresh ledger\nAnd an invoice \"Rent\"");
        var hidden = BindFile("Invoicing/other.feature.md", "Given an account");

        Assert.True(bound.Success, BddDiagnosticsFormatter.FormatDiagnostics(bound.Diagnostics));
        Assert.EndsWith("InvoicingPhrases.AddLine", bound.Document!.Scenarios[0].Invocations[^1].Command.CommandId, StringComparison.Ordinal);
        var error = Assert.Single(hidden.Diagnostics, d => d.Severity == BddDiagnosticSeverity.Error);
        Assert.Equal("UATB010", error.Code);
        Assert.Contains("'an account' of the folder 'Accounts'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallablesSteps_BindWhereItIsWritten_NotWhereItIsCalled()
    {
        // The root's callable cannot reach Invoicing's phrase, even when an Invoicing file calls it (D9).
        Write("Common.steps.md", Phrases("a lined ledger", "When I add a line \"Rent\""));

        var result = BindFile("Invoicing/invoice.feature.md", "Given a lined ledger");

        var error = Assert.Single(result.Diagnostics, d => d.Severity == BddDiagnosticSeverity.Error);
        Assert.Contains("UATB010", error.Code + error.Message, StringComparison.Ordinal);
        Assert.Contains("the scenario root does not see", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFilesOwnCallable_IsNotSeenFromAPhraseFilesCallable()
    {
        Write("Invoicing/Invoicing.steps.md", Phrases("a checked invoice", "Given my own start"));
        var scenario = Scenario("Given a checked invoice") + "\n\n" + Phrases("my own start", "When I note \"start\"").Replace("# Steps: Test\n\n", string.Empty, StringComparison.Ordinal);

        var result = BddScenarioFiles.Load(Write("Invoicing/invoice.feature.md", scenario)).Bind(Catalogs());

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("'my own start', written in invoice.feature.md for that file only", StringComparison.Ordinal));
    }

    [Fact]
    public void AFilesOwnCallable_BindsItsStepsInTheFilesFolder()
    {
        var scenario = Scenario("Given my own line") + "\n\n" + Phrases("my own line", "When I add a line \"Rent\"").Replace("# Steps: Test\n\n", string.Empty, StringComparison.Ordinal);

        var result = BddScenarioFiles.Load(Write("Invoicing/invoice.feature.md", scenario)).Bind(Catalogs());

        Assert.True(result.Success, BddDiagnosticsFormatter.FormatDiagnostics(result.Diagnostics));
    }

    [Fact]
    public void AnAssemblysNamespace_IsItsPhraseClassesNamespace_UnlessAClassNamesItsOwn()
    {
        // [assembly: BddNamespace] would namespace every phrase class of this test assembly, so the
        // assembly under test is built here.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Bruinet.Uat.Tests.Dynamic" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run,
            [new CustomAttributeBuilder(typeof(BddNamespaceAttribute).GetConstructor([typeof(string)])!, ["Invoicing"])]);
        var module = assembly.DefineDynamicModule("Dynamic");
        var inherits = PhraseClass(module, "Dyn.InheritsPhrases", "I file a line {text}", ownNamespace: null);
        PhraseClass(module, "Dyn.OwnPhrases", "I order a line {text}", ownNamespace: "Purchasing");
        var fixture = module.DefineType("Dyn.Fixture", TypeAttributes.Public | TypeAttributes.Class);
        fixture.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(TestModuleScanAttribute).GetConstructor([typeof(Type)])!,
            [inherits],
            [typeof(TestModuleScanAttribute).GetProperty(nameof(TestModuleScanAttribute.NamespacePrefix))!],
            ["Dyn"]));

        // The scan attribute names its marker type by assembly name, which only this handler finds.
        Assembly? Resolve(object? sender, ResolveEventArgs args) => args.Name == assembly.FullName ? assembly : null;
        var catalog = new BddCommandCatalog();
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try
        {
            UatReflectionRuntime.RegisterCompositionPhrases(catalog, fixture.CreateType());
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
        }

        Assert.Equal("Invoicing", Assert.Single(catalog.Patterns, p => p.Phrase == "I file a line {text}").Namespace);
        Assert.Equal("Purchasing", Assert.Single(catalog.Patterns, p => p.Phrase == "I order a line {text}").Namespace);
    }

    [Fact]
    public void ThePhraseList_NamesTheNamespace()
    {
        var lines = UatScenarioCheck.DescribePhrases(Catalogs().Default, BddEffectiveStepKeyword.When);

        Assert.Contains(lines, line => line.Contains("InvoicingPhrases.AddLine  in Invoicing", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("GlobalPhrases.Note", StringComparison.Ordinal) && !line.Contains("  in ", StringComparison.Ordinal));
    }

    private IReadOnlyList<BddStepInvocation> BindOk(string path, string steps)
    {
        var result = BindFile(path, steps);
        Assert.True(result.Success, BddDiagnosticsFormatter.FormatDiagnostics(result.Diagnostics));
        return result.Document!.Scenarios[0].Invocations;
    }

    private BddDiagnostic BindError(string path, string steps)
        => Assert.Single(BindFile(path, steps).Diagnostics, d => d.Severity == BddDiagnosticSeverity.Error);

    private BddBindResult BindFile(string path, string steps)
    {
        var file = BddScenarioFiles.Load(Write(path, Scenario(steps)));
        Assert.True(file.Success, BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
        return file.Bind(Catalogs());
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private static BddCatalogSet Catalogs()
    {
        var fixture = new NamespaceFixture();
        return new BddCatalogSet(UatReflectionRuntime.FromComposition(fixture, fixture.Composition).CreateCommandCatalog());
    }

    // public sealed class {name} : BddStepDefinitionsBase { [BddWhen(phrase)] public void Step(string text) { } }
    private static Type PhraseClass(ModuleBuilder module, string name, string phrase, string? ownNamespace)
    {
        var type = module.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class, typeof(BddStepDefinitionsBase));
        type.SetCustomAttribute(ownNamespace is null
            ? new CustomAttributeBuilder(typeof(BddStepDefinitionsAttribute).GetConstructor(Type.EmptyTypes)!, [])
            : new CustomAttributeBuilder(
                typeof(BddStepDefinitionsAttribute).GetConstructor(Type.EmptyTypes)!,
                [],
                [typeof(BddStepDefinitionsAttribute).GetProperty(nameof(BddStepDefinitionsAttribute.Namespace))!],
                [ownNamespace]));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var step = type.DefineMethod("Step", MethodAttributes.Public, typeof(void), [typeof(string)]);
        step.DefineParameter(1, ParameterAttributes.None, "text");
        step.SetCustomAttribute(new CustomAttributeBuilder(typeof(BddWhenAttribute).GetConstructor([typeof(string)])!, [phrase]));
        step.GetILGenerator().Emit(OpCodes.Ret);
        return type.CreateType();
    }

    private static string Scenario(string steps) => $"# Feature: Namespaces\n\n## Scenario: Namespaces\n```gherkin\n{steps}\n```\n";

    private static string Phrases(string phrase, string steps) => $"# Steps: Test\n\n## Step: {phrase}\n```gherkin\n{steps}\n```\n";
}

[TestModuleScan(typeof(NamespaceFixture), NamespacePrefix = "Bruinet.Uat.Tests.NamespaceCases")]
[BddIgnore]
public sealed class NamespaceFixture
{
    public NamespaceFixture()
    {
        Composition = TestComposition.ForFixture(this, services => services.AddSingleton(new List<string>()));
    }

    public TestComposition Composition { get; }
}

[BddStepDefinitions]
[BddIgnore]
public sealed class GlobalPhrases(List<string> notes) : BddStepDefinitionsBase
{
    [BddWhen("I note {text}")]
    public void Note(string text) => notes.Add(text);
}

[BddStepDefinitions(Namespace = "Invoicing")]
[BddIgnore]
public sealed class InvoicingPhrases(List<string> notes) : BddStepDefinitionsBase
{
    [BddWhen("I add a line {text}")]
    public void AddLine(string text) => notes.Add($"invoice {text}");

    [BddWhen("I note {text}")]
    public void Note(string text) => notes.Add($"invoice note {text}");
}

[BddStepDefinitions(Namespace = "Purchasing")]
[BddIgnore]
public sealed class PurchasingPhrases(List<string> notes) : BddStepDefinitionsBase
{
    [BddWhen("I add a line {text}")]
    public void AddLine(string text) => notes.Add($"order {text}");
}
