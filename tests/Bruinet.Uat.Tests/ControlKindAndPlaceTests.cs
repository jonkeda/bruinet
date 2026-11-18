using Bruinet.Bdd;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>
/// How a step names a control of a kind (<c>[BddKind]</c>): by its member's name alone, with its kind's word before or
/// after it, and, for a name that is on the page more than once, with the part it is in. Nothing of it is typed into
/// a <c>[BddControl]</c>.
/// </summary>
public sealed class ControlKindAndPlaceTests
{
    [Theory]
    [InlineData("Hourly rate")]
    [InlineData("the field Hourly rate")]
    [InlineData("the Hourly rate field")]
    [InlineData("hourly RATE")]
    public async Task A_member_of_a_kind_is_named_by_its_member_with_or_without_its_kinds_word(string said)
    {
        var fixture = new FormFixture();

        var result = await RunAsync(fixture, $"""
            Given I am on the Form page
            When I jot "45" into {said}
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["Financial.HourlyRate=45"], fixture.FormPage.Calls);
    }

    [Fact]
    public async Task A_part_of_a_part_is_looked_in()
    {
        var fixture = new FormFixture();

        var result = await RunAsync(fixture, """
            Given I am on the Form page
            When I jot "NL01" into Iban
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["Financial.Bank.Iban=NL01"], fixture.FormPage.Calls);
    }

    [Theory]
    [InlineData("Sales price in Financial", "Financial.SalesPrice=60")]
    [InlineData("Sales price under Financial", "Financial.SalesPrice=60")]
    [InlineData("the field Sales price under the section Financial", "Financial.SalesPrice=60")]
    [InlineData("Sales price in the Price per hour type section", "PricePerHourType.SalesPrice=60")]
    public async Task A_name_that_is_on_the_page_twice_is_said_with_its_part(string said, string call)
    {
        var fixture = new FormFixture();

        var result = await RunAsync(fixture, $"""
            Given I am on the Form page
            When I jot "60" into {said}
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal([call], fixture.FormPage.Calls);
    }

    [Fact]
    public void A_name_that_is_on_the_page_twice_is_refused_and_the_refusal_says_how()
    {
        var bind = Bind(new FormFixture(), """
            Given I am on the Form page
            When I jot "60" into Sales price
            """);

        Assert.False(bind.Success);
        var message = Assert.Single(bind.Diagnostics, diagnostic => diagnostic.Message.Contains("several controls", StringComparison.Ordinal)).Message;
        Assert.Contains("Sales price in Financial", message, StringComparison.Ordinal);
        Assert.Contains("Sales price in Price per hour type", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_kind_tells_two_controls_of_one_name_apart()
    {
        var fixture = new FormFixture();

        var result = await RunAsync(fixture, """
            Given I am on the Form page
            When I jot "a" into the tab Prices
            And I jot "b" into the Prices part
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["tab Prices=a", "part Prices=b"], fixture.FormPage.Calls);
    }

    [Fact]
    public void A_name_of_two_kinds_without_its_kind_is_refused_and_the_refusal_offers_both()
    {
        var bind = Bind(new FormFixture(), """
            Given I am on the Form page
            When I jot "a" into Prices
            """);

        Assert.False(bind.Success);
        var message = Assert.Single(bind.Diagnostics, diagnostic => diagnostic.Message.Contains("several controls", StringComparison.Ordinal)).Message;
        Assert.Contains("the tab Prices", message, StringComparison.Ordinal);
        Assert.Contains("the part Prices", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declared_words_still_mean_what_they_meant_when_a_member_has_the_same_name()
    {
        var fixture = new FormFixture();

        // "Cancel" is declared on one part and is only a member's name on another: the declared one is meant.
        var result = await RunAsync(fixture, """
            Given I am on the Form page
            When I jot "x" into Cancel
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["declared Cancel=x"], fixture.FormPage.Calls);
    }

    [Fact]
    public async Task A_declared_name_gives_a_label_a_members_name_cannot()
    {
        var fixture = new FormFixture();

        var result = await RunAsync(fixture, """
            Given I am on the Form page
            When I jot "123" into the field CoC no.
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["Financial.CocNumber=123"], fixture.FormPage.Calls);
    }

    [Fact]
    public void A_name_no_control_has_lists_a_members_name_with_its_part()
    {
        var bind = Bind(new FormFixture(), """
            Given I am on the Form page
            When I jot "45" into Hourly rat
            """);

        Assert.False(bind.Success);
        Assert.Contains(bind.Diagnostics, diagnostic => diagnostic.Message.Contains("Hourly rate in Financial", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Another_page_a_page_can_give_is_not_a_part_of_it()
    {
        var fixture = new FormFixture();

        // The Form page has a property that gives the Other page, which has an Hourly rate of its own: on the Form
        // page the name is still one control, the form's.
        var result = await RunAsync(fixture, """
            Given I am on the Form page
            When I jot "45" into Hourly rate
            """);

        Assert.True(result.Passed, Format(result));
        Assert.Equal(["Financial.HourlyRate=45"], fixture.FormPage.Calls);
    }

    [Theory]
    [InlineData("HourlyRate", "Hourly rate")]
    [InlineData("Iban", "Iban")]
    [InlineData("VATNumber", "Vat number")]
    [InlineData("Reminder1", "Reminder 1")]
    [InlineData("AdditionToSubject", "Addition to subject")]
    public void A_members_name_reads_as_a_label(string member, string label)
        => Assert.Equal(label, BddControlNames.FromMember(member));

    private static BddBindResult Bind(FormFixture fixture, string steps)
    {
        var parse = BddMarkdownParser.Parse($"""
            # Feature: Form

            ## Scenario: Form steps
            ```gherkin
            {steps}
            ```
            """);
        Assert.True(parse.Success);
        return BddBinder.Bind(parse.Document!, UatReflectionRuntime.FromRoot(fixture).CreateCommandCatalog());
    }

    private static async Task<BddScenarioRunResult> RunAsync(FormFixture fixture, string steps)
    {
        var bind = Bind(fixture, steps);
        Assert.True(bind.Success, string.Join(Environment.NewLine, bind.Diagnostics.Select(x => $"{x.Code} {x.Message}")));
        return await new BddScenarioRunner().RunAsync(Assert.Single(bind.Document!.Scenarios));
    }

    private static string Format(BddScenarioRunResult result) =>
        string.Join(Environment.NewLine, result.Steps.Select(x => $"{x.Status}: {x.Invocation.Step.Text} {x.Message}"));
}

/// <summary>What the kinds below share: they take a value.</summary>
public abstract class Enterable(List<string> calls, string what)
{
    /// <summary>Takes a value.</summary>
    public void Take(string text) => calls.Add($"{what}={text}");
}

/// <summary>The one verb of the kinds below, declared once for all of them.</summary>
[BddVerbs]
public static class EnterableVerbs
{
    /// <summary>Types a value.</summary>
    [BddWhen("I jot {value} into {control}")]
    public static void Enter(this Enterable control, string value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Take(value);
    }
}

/// <summary>A form's row: a kind of control, so a member of it needs no name of its own.</summary>
[BddKind("field")]
public sealed class Row(List<string> calls, string what) : Enterable(calls, what);

/// <summary>A tab's header.</summary>
[BddKind("tab")]
public sealed class Tab(List<string> calls, string what) : Enterable(calls, what);

/// <summary>A card on a tab.</summary>
[BddKind("part")]
public sealed class Card(List<string> calls, string what) : Enterable(calls, what);

/// <summary>A section of the form that holds rows, and a part of its own.</summary>
[BddKind("section")]
public sealed class FinancialSection(List<string> calls)
{
    public Row HourlyRate => new(calls, "Financial.HourlyRate");

    public Row SalesPrice => new(calls, "Financial.SalesPrice");

    [BddControl("CoC no.")]
    public Row CocNumber => new(calls, "Financial.CocNumber");

    public BankPart Bank => new(calls);

    /// <summary>A member that is only named by its member: another part declares the same words.</summary>
    public Row Cancel => new(calls, "member Cancel");
}

/// <summary>A part of a section: two levels under the page.</summary>
public sealed class BankPart(List<string> calls)
{
    public Row Iban => new(calls, "Financial.Bank.Iban");
}

/// <summary>A second section with a row of the same name as one of the first's.</summary>
[BddKind("section")]
public sealed class PricePerHourTypeSection(List<string> calls)
{
    public Row SalesPrice => new(calls, "PricePerHourType.SalesPrice");
}

/// <summary>A part that declares its control's words.</summary>
public sealed class Buttons(List<string> calls)
{
    [BddControl("Cancel")]
    public Row CancelButton => new(calls, "declared Cancel");
}

internal sealed class FormFixture
{
    public FormPage FormPage { get; } = new();

    public OtherPage OtherPage { get; } = new();
}

internal sealed class OtherPage
{
    public List<string> Calls { get; } = [];

    public Row HourlyRate => new(Calls, "Other.HourlyRate");

    public bool WaitReady(int? timeoutMs = null) => true;
}

internal sealed class FormPage
{
    public List<string> Calls { get; } = [];

    public FinancialSection Financial => new(Calls);

    public PricePerHourTypeSection PricePerHourType => new(Calls);

    public Buttons Buttons => new(Calls);

    [BddControl("Prices")]
    public Tab PricesTab => new(Calls, "tab Prices");

    [BddControl("Prices")]
    public Card PricesPart => new(Calls, "part Prices");

    /// <summary>The page a tab of this one opens: a page, not a part of this one.</summary>
    public OtherPage Other => new();

    public bool WaitReady(int? timeoutMs = null) => true;
}
