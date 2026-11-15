using Bruinet.Presenter.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Bruinet.Presenter.Bdd.Tests.PageObjects;

namespace Bruinet.Presenter.Bdd.Tests.Runtime;

[Collection(PresenterUatCollection.CollectionName)]
[Trait("Category", "UAT")]
[Trait("Target", "Presenter")]
public sealed class PresenterWorkspaceTreeTests : IDisposable
{
    private readonly IServiceScope _scope;
    private readonly PresenterPage _page;

    public PresenterWorkspaceTreeTests(PresenterFixture fixture)
    {
        _scope = fixture.Composition.CreateScope();
        _page = _scope.ServiceProvider.GetRequiredService<PresenterPage>();
    }

    [Fact]
    public void WorkspaceTree_ShowsMarkdownOnly()
    {
        ReloadWorkspace();

        var allTree = _page.AllWorkspaceTree.GetText(timeoutMs: 10000) ?? string.Empty;
        Assert.Contains("bdd.config.md", allTree, StringComparison.Ordinal);
        Assert.Contains("main-page-greeting.feature.md", allTree, StringComparison.Ordinal);
        Assert.Contains("main-page-validation.feature.md", allTree, StringComparison.Ordinal);
        Assert.DoesNotContain(".csproj", allTree, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".dll", allTree, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bin", allTree, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("obj", allTree, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pick in a tree, step in a list: the tree stops at scenarios, and the step list holds the
    /// selected scenario's steps.
    /// </summary>
    [Fact]
    public void StepList_HoldsTheSelectedScenariosSteps_AndTheTreeNone()
    {
        ReloadWorkspace();

        var steps = (_page.StepList.GetText(timeoutMs: 10000) ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.NotEmpty(steps);
        var firstStep = steps[0].TrimStart('>', ' ')[1..].Trim();
        Assert.DoesNotContain(firstStep, _page.AllWorkspaceTree.GetText(timeoutMs: 10000) ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reload rebuilds every node, so it has to carry expansion and selection across. Saving the
    /// config reloads, and a save that drops your place is worse than no reload at all.
    /// </summary>
    [Fact]
    public void Reload_KeepsExpandedNodesExpanded()
    {
        ReloadWorkspace();

        const string collapsedFile = "main-page-validation.feature.md";
        const string revealedSuite = "MAUI Main Page Greeting Validation";

        _page.ExpandTreeNode(BddWorkspaceNodeKind.MarkdownFile, collapsedFile, revealedSuite);
        Assert.Contains(revealedSuite, _page.WorkspaceTree.GetText(timeoutMs: 10000) ?? string.Empty, StringComparison.Ordinal);

        _page.ReloadButton.Click();
        _page.StatusSummary.AssertTextContains("Ready", timeoutMs: 30000);

        Assert.Contains(
            revealedSuite,
            _page.WorkspaceTree.GetText(timeoutMs: 10000) ?? string.Empty,
            StringComparison.Ordinal);
    }

    private void ReloadWorkspace()
    {
        Assert.True(_page.IsLoaded(), "Presenter page was not loaded.");
        _page.ReloadButton.Click();
        _page.StatusSummary.AssertTextContains("Ready", timeoutMs: 30000);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
