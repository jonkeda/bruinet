using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.CommunityToolkit;

/// <summary>
/// UI tests for the CommunityToolkit Expander.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Expander")]
public class ExpanderTests
{
    private readonly MauiFixture _fixture;

    public ExpanderTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.CommunityToolkit);
    }

    private CommunityToolkitTestPage GetPage() => new(_fixture.Context);

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task Expander_IsExists_ReturnsTrue()
    {
        GetPage().TestExpander.AssertExists();
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsExpanded")]
    public Task Expander_StartsCollapsed()
    {
        GetPage().TestExpander.AssertExpanded(false)
            .ExpanderStatusLabel.AssertText("collapsed");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Expand")]
    public Task Expander_Expand_ShowsContent()
    {
        GetPage().TestExpander.Expand()
            .TestExpander.AssertExpanded(true)
            .ExpanderStatusLabel.AssertText("expanded");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Collapse")]
    public Task Expander_ExpandThenCollapse_HidesContent()
    {
        GetPage().TestExpander.Expand()
            .ExpanderStatusLabel.AssertText("expanded")
            .TestExpander.Collapse()
            .TestExpander.AssertExpanded(false)
            .ExpanderStatusLabel.AssertText("collapsed");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Expand")]
    public Task Expander_ExpandTwice_IsIdempotent()
    {
        var page = GetPage();

        page.TestExpander.Expand()
            .TestExpander.Expand()
            .TestExpander.AssertExpanded(true);

        Assert.True(page.TestExpander.WaitExpanded(true));
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Toggle")]
    public Task Expander_Toggle_FlipsState()
    {
        GetPage().TestExpander.Toggle()
            .ExpanderStatusLabel.AssertText("expanded")
            .TestExpander.Toggle()
            .ExpanderStatusLabel.AssertText("collapsed");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetExpanded")]
    public Task Expander_SetExpanded_Null_DoesNothing()
    {
        GetPage().TestExpander.SetExpanded(null)
            .TestExpander.AssertExpanded(false);
        return Task.CompletedTask;
    }
}
