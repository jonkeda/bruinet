using Bruinet.Scraper.Code;

namespace Bruinet.Scraper.Tests.Code;

// A spec drafted from what the pages show: a rule and an example per page, in the suite's phrases.
public sealed class SpecDrafterTests
{
    private static readonly DraftTemplates Templates = DraftTemplates.Parse("""
        ## Spec head

        ```markdown
        # Spec: {Id} {Title}

        | Field | Value |
        | --- | --- |
        | Status | draft |

        ## Rules
        ```

        ## Spec opening /project/view

        ```gherkin
        Creator opens the seed project
        The seed project is never changed.
        Given Creator is signed in
        When I open the project "Seed"
        ```

        ## Spec page steps

        ```gherkin
        When I tap the tab {Tab}
        Then the {PageName} page is open
        ```

        ## Spec button step

        ```gherkin
        And the {Label} button is shown
        ```

        ## Spec page rule

        ```markdown
        The tab {Tab} offers {Buttons}.
        ```
        """);

    private static readonly SpecPage[] Pages =
    [
        new("/project/view#/complaints", "Complaints", "Project View Complaints", ["Edit", "+ Complaint"]),
        new("/project/view#/hours", "Hours", "Project View Hours", ["Edit"]),
        new("/project/view#/waste", "Waste", "Project View Waste", ["Export", "+ Log waste"]),
    ];

    [Fact]
    public void A_page_gets_a_rule_and_an_example_without_the_steps_another_spec_has()
    {
        var existing = SpecDrafter.StepsOf("```gherkin\nWhen I tap the Edit button\nThen the Edit button is shown\n  And the title says \"x\"\n```\nThe Export button is shown in prose.").ToHashSet(StringComparer.Ordinal);
        Assert.Equal(["I tap the Edit button", "the Edit button is shown", "the title says \"x\""], existing.Order(StringComparer.Ordinal));

        var draft = SpecDrafter.Draft("B7.PRJ.04", "A project's tabs", "/project/view", Pages, Templates, existing);

        Assert.Equal([("B7.PRJ.04.R1", "The tab Complaints offers \"+ Complaint\"."), ("B7.PRJ.04.R2", "The tab Waste offers \"Export\" and \"+ Log waste\".")], draft.Rules);
        Assert.Equal(["/project/view#/hours: nothing to say that another spec does not say already"], draft.LeftOut);
        Assert.StartsWith("# Spec: B7.PRJ.04 A project's tabs\n", draft.Text, StringComparison.Ordinal);
        Assert.Contains("## Rules\n\n| Id | Rule |\n| --- | --- |\n| B7.PRJ.04.R1 | The tab Complaints offers \"+ Complaint\". |", draft.Text, StringComparison.Ordinal);
        Assert.Contains("### Chapter: Creator opens the seed project\n\nThe seed project is never changed.\n\n```gherkin\nGiven Creator is signed in\nWhen I open the project \"Seed\"\n```", draft.Text, StringComparison.Ordinal);
        Assert.Contains("#### R1 Complaints\n\nThe tab Complaints offers \"+ Complaint\".\n\n```gherkin\nWhen I tap the tab Complaints\nThen the Project View Complaints page is open\nAnd the + Complaint button is shown\n```", draft.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("the Edit button is shown", draft.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_whose_example_failed_on_the_app_is_left_out_with_what_the_test_said()
    {
        var failed = new Dictionary<string, string> { ["/project/view#/complaints"] = "the + Complaint button is not shown" };

        var draft = SpecDrafter.Draft("B7.PRJ.04", "A project's tabs", "/project/view", Pages, Templates, new HashSet<string>(), failed);

        Assert.Contains("/project/view#/complaints: its example failed on the app: the + Complaint button is not shown", draft.LeftOut);
        // The rules that stay are numbered anew.
        Assert.Equal(["B7.PRJ.04.R1", "B7.PRJ.04.R2"], draft.Rules.Select(rule => rule.Id));
        Assert.StartsWith("The tab Hours offers", draft.Rules[0].Rule, StringComparison.Ordinal);
        Assert.DoesNotContain("Complaints", draft.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ProjectViewComplaintsPage", "Project View Complaints")]
    [InlineData("KamPage", "Kam")]
    [InlineData("MyAccountQhsePage", "My Account Qhse")]
    public void A_page_is_named_in_a_step_by_its_class_s_words(string className, string name)
        => Assert.Equal(name, SpecDrafter.PageName(className));

    [Fact]
    public void A_suite_whose_templates_lack_a_spec_block_cannot_draft_one()
    {
        Assert.True(SpecDrafter.CanDraft(Templates, "/project/view"));
        Assert.False(SpecDrafter.CanDraft(Templates, "/contact"));
    }
}
