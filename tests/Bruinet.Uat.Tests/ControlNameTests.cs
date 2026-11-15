using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>
/// Control names (<c>[BddControl]</c> on a method): a step names a control that has no fixed name, such as the filter
/// above a column, and every verb of that control's type works on it.
/// </summary>
public sealed class ControlNameTests
{
    [Fact]
    public async Task A_components_named_method_gives_the_control_and_any_verb_of_its_type_runs_on_it()
    {
        var fixture = new GridFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Grid page
            When I fill the filter above City with "delft"
            And I fill the filter above "Contact Type" with "Supplier"
            And I wipe the filter above Name
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["City=delft", "Contact Type=Supplier", "Name wiped"], fixture.GridPage.Results.Calls);
    }

    [Fact]
    public async Task A_fixed_name_on_a_components_property_names_its_control()
    {
        var fixture = new GridFixture();
        var catalog = UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog();

        var result = await RunAsync(catalog, """
            Given I am on the Grid page
            When I fill the footer with "1-14"
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["footer=1-14"], fixture.GridPage.Results.Calls);
    }

    [Fact]
    public void A_name_no_control_has_is_a_bind_error_that_lists_the_declared_names()
    {
        var catalog = UatReflectionRuntime.FromRoot(new GridFixture()).CreateCommandCatalog();
        var parse = BddMarkdownParser.Parse("""
            # Feature: Grid

            ## Scenario: Nothing there
            ```gherkin
            Given I am on the Grid page
            When I fill the sorter below Name with "x"
            ```
            """);

        var bind = BddBinder.Bind(parse.Document!, catalog);

        Assert.False(bind.Success);
        Assert.Contains(bind.Diagnostics, diagnostic => diagnostic.Message.Contains("the filter above {column}", StringComparison.Ordinal));
    }

    private static async Task<BddScenarioRunResult> RunAsync(BddCommandCatalog catalog, string steps)
    {
        var parse = BddMarkdownParser.Parse($"""
            # Feature: Grid

            ## Scenario: Grid steps
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

/// <summary>A filter box: a control with verbs of its own, but no fixed name on the page.</summary>
public sealed class FilterBox(List<string> calls, string column)
{
    /// <summary>Types a text.</summary>
    [BddWhen("I fill {control} with {value}")]
    public void Fill(string text) => calls.Add($"{column}={text}");

    /// <summary>Empties it.</summary>
    [BddWhen("I wipe {control}")]
    public void Wipe() => calls.Add($"{column} wiped");
}

/// <summary>A results grid that names the controls inside it.</summary>
public sealed class ResultsGrid
{
    /// <summary>What its controls did.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The filter above a column, by its header.</summary>
    [BddControl("the filter above {column}")]
    public FilterBox FilterAbove(string column) => new(Calls, column);

    /// <summary>The footer, a fixed name on a component's property.</summary>
    [BddControl("the footer")]
    public FilterBox Footer => new(Calls, "footer");

    /// <summary>A verb, so the page counts the grid as a control.</summary>
    [BddWhen("I refresh {control}")]
    public void Refresh() => Calls.Add("refreshed");
}

internal sealed class GridFixture
{
    public GridPage GridPage { get; } = new();
}

internal sealed class GridPage
{
    public ResultsGrid Results { get; } = new();

    public bool WaitReady(int? timeoutMs = null) => true;
}
