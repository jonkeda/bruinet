using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>Extension verbs (<c>[BddVerbs]</c>): written once for a base type, bound and run on every control derived from it.</summary>
public sealed class ExtensionVerbTests
{
    [Fact]
    public async Task A_generic_extension_verb_binds_to_a_control_whose_base_it_takes_and_runs_with_the_control_first()
    {
        var fixture = new ShelfFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Shelf page
            Then the books holds only "Dune", "Emma" and "Ulysses"
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["holds Dune|Emma|Ulysses"], fixture.ShelfPage.Books.Calls);
    }

    [Fact]
    public async Task An_extension_verb_without_control_runs_on_the_one_control_of_its_type()
    {
        var fixture = new ShelfFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Shelf page
            When I dust the shelf
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["dusted"], fixture.ShelfPage.Books.Calls);
    }

    [Fact]
    public void An_extension_verb_on_a_control_it_does_not_take_is_a_bind_error()
    {
        var catalog = UatReflectionRuntime.FromRoot(new ShelfFixture()).CreateCommandCatalog();
        var parse = BddMarkdownParser.Parse("""
            # Feature: Shelf

            ## Scenario: Lamp
            ```gherkin
            Given I am on the Shelf page
            Then the lamp holds only "Dune"
            ```
            """);

        var bind = BddBinder.Bind(parse.Document!, catalog);

        Assert.False(bind.Success);
        Assert.Contains(bind.Diagnostics, diagnostic => diagnostic.Message.Contains("cannot", StringComparison.Ordinal));
    }

    private static async Task<BddScenarioRunResult> RunAsync(BddCommandCatalog catalog, string steps)
    {
        var parse = BddMarkdownParser.Parse($"""
            # Feature: Shelf

            ## Scenario: Shelf steps
            ```gherkin
            {steps}
            ```
            """);
        Assert.True(parse.Success);
        var bind = BddBinder.Bind(parse.Document!, catalog);
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
        return await new BddScenarioRunner().RunAsync(Assert.Single(bind.Document!.Scenarios));
    }

    private static string Format(BddScenarioRunResult result) =>
        string.Join(Environment.NewLine, result.Steps.Select(x => $"{x.Status}: {x.Invocation.Step.Text} {x.Message}"));
}

/// <summary>A base of collections, as <c>TableControl&lt;TScope&gt;</c> is.</summary>
/// <typeparam name="T">What it holds.</typeparam>
public class ShelfBase<T>
{
    /// <summary>What the verbs did.</summary>
    public List<string> Calls { get; } = [];
}

/// <summary>A control derived from the base, as a suite's own table is.</summary>
public sealed class BookShelf : ShelfBase<string>
{
}

/// <summary>A control the verbs do not take.</summary>
public sealed class LampControl
{
    /// <summary>A verb of its own, so the page counts it as a control.</summary>
    [BddWhen("I switch {control}")]
    public void Switch()
    {
    }
}

/// <summary>Extension verbs written once for every shelf.</summary>
[BddVerbs]
public static class ShelfVerbs
{
    /// <summary>The shelf holds these.</summary>
    [BddThen("{control} holds only {values}")]
    public static void HoldsOnly<T>(this ShelfBase<T> shelf, string[] values) => shelf.Calls.Add($"holds {string.Join('|', values)}");

    /// <summary>The page's one shelf is dusted.</summary>
    [BddWhen("I dust the shelf")]
    public static void Dust(this BookShelf shelf) => shelf.Calls.Add("dusted");
}

internal sealed class ShelfFixture
{
    public ShelfPage ShelfPage { get; } = new();
}

internal sealed class ShelfPage
{
    public BookShelf Books { get; } = new();

    public LampControl Lamp { get; } = new();

    public bool WaitReady(int? timeoutMs = null) => true;
}
