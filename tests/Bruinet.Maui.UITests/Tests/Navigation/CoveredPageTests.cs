using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Navigation;

/// <summary>
/// A busy page under a pushed page and a modal: it is ready once its load ends; a modal covers it
/// without taking it away, a pushed page takes it away; and it is ready again when either closes, on
/// every head.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Pattern", "Readiness")]
public class CoveredPageTests
{
    private readonly CoveredTestPage _page;
    private readonly MauiFixture _fixture;

    public CoveredPageTests(MauiFixture fixture)
    {
        _fixture = fixture;
        fixture.Open(SamplePage.Covered);
        _page = new CoveredTestPage(fixture.Context);
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task BusyPage_IsReadyOnceItsLoadEnds()
    {
        Assert.True(_page.WaitReady());

        _page.Status.AssertText("loaded");
        Assert.False(_page.IsBusy());

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task PushedPage_Closed_ShowsTheCoveredPageReadyAgain()
    {
        _page.Status.AssertText("loaded");
        _page.PushDetailButton.Click();
        var detail = new CoveringTestPage(_fixture.Context, "CoveredDetailPage");
        detail.Title.AssertText("Detail");
        // A page a NavigationPage pushed over leaves the tree (Windows a moment later).
        Assert.True(new CoveredTestPage(_fixture.Context).WaitLoaded(false, TestConstants.DefaultTestTimeoutMs / 3));

        detail.CloseButton.Click();

        _page.Status.AssertText("loaded");
        Assert.True(_page.WaitReady());

        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    public Task Modal_Closed_ShowsTheCoveredPageReadyAgain()
    {
        _page.Status.AssertText("loaded");
        _page.OpenModalButton.Click();
        var modal = new CoveringTestPage(_fixture.Context, "CoveredModalPage");
        modal.Title.AssertText("Modal");
        // On Windows and MauiHeadless a modal covers the page without taking it away: it is still found, and ready.
        // Android leaves the covered page out of the accessibility tree while the modal is up, as a user cannot
        // reach it either (R2-T112).
        if (Contracts.SkipOnFactAttribute.Current != Contracts.ContractHeads.Mobile)
        {
            var covered = new CoveredTestPage(_fixture.Context);
            Assert.True(covered.IsLoaded());
            covered.Status.AssertText("loaded");
        }

        modal.CloseButton.Click();

        _page.Status.AssertText("loaded");
        Assert.True(_page.WaitReady());

        return Task.CompletedTask;
    }
}
