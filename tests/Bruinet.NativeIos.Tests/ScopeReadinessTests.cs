using Bruinet.Core.Scopes;
using Bruinet.NativeIos.Controls.Dialogs;

namespace Bruinet.NativeIos.Tests;

/// <summary>Scope readiness on native iOS (plan step 7): the app in front, the page, the busy view, alerts.</summary>
public class ScopeReadinessTests
{
    private readonly FakeApp _app = new();

    private void GivenBusy(Func<bool> shown)
        => _app.Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value.Contains(PageObjectBase<TestPage>.BusyViewName))))
            .Returns(() => shown() ? FakeApp.View().Object : null);

    /// <summary>A covered app is not ready, and the failure says what covers it.</summary>
    [Fact]
    public void ACoveredApp_FailsTheCall_NamingWhatCoversIt()
    {
        _app.InFront = false;
        _app.Given("Target", () => FakeApp.View().Object);

        var error = Record.Exception(() => _app.Page().Target.Click(timeoutMs: 300));

        Assert.IsType<ScopeNotReadyException>(error);
        Assert.Contains("system alert", error!.Message);
    }

    /// <summary>A busy page delays a control's call; the call acts once the app is idle.</summary>
    [Fact]
    public void ABusyPage_DelaysTheCall_UntilTheAppIsIdle()
    {
        var probes = 0;
        GivenBusy(() => ++probes < 4);
        var button = FakeApp.View();
        _app.Given("Target", () => button.Object);

        _app.Page().Target.Click();

        button.Verify(e => e.Click(), Times.Once);
        Assert.True(probes >= 4);
    }

    /// <summary>A page that stays busy fails the call as busy, and is never tapped.</summary>
    [Fact]
    public void APageThatStaysBusy_FailsTheCallAsBusy()
    {
        GivenBusy(() => true);
        var button = FakeApp.View();
        _app.Given("Target", () => button.Object);

        var error = Record.Exception(() => _app.Page().Target.Click(timeoutMs: 300));

        var notReady = Assert.IsType<ScopeNotReadyException>(error);
        Assert.Equal(ScopeReadinessState.Busy, notReady.Readiness.State);
        button.Verify(e => e.Click(), Times.Never);
    }

    /// <summary>A page with a ready element is loaded only while that element is shown.</summary>
    [Fact]
    public void APageWithAReadyElement_IsLoadedOnlyWhileItIsShown()
    {
        var page = new LoginPage(_app.Context.Object);

        Assert.False(page.IsLoaded());

        _app.Given("LoginForm", () => FakeApp.View().Object);
        Assert.True(page.IsLoaded());
    }

    /// <summary>AutoFill's "Save Password?" leaves the app in front but covers the page: the readiness names it.</summary>
    [Fact]
    public void APageCoveredByASystemAlert_SaysWhichAlert()
    {
        _app.Given("LoginForm", () => FakeApp.View(visible: false).Object);
        _app.Driver.Setup(d => d.SystemAlertText).Returns("Save Password?\nSecurely store your password.");

        var readiness = new LoginPage(_app.Context.Object).ProbeReadiness();

        Assert.Equal(ScopeReadinessState.NotLoaded, readiness.State);
        Assert.Contains("covered by a system alert: \"Save Password? Securely store your password.\"", readiness.Detail, StringComparison.Ordinal);
    }

    /// <summary>The privacy prompt is usable while it covers the app: it does not ask the app.</summary>
    [Fact]
    public void ThePermissionAlert_IsReady_WhileItCoversTheApp()
    {
        _app.InFront = false;
        var alert = FakeApp.View();
        _app.Driver.Setup(d => d.FindSystemElements(It.Is<Locator>(l => l.Value == "XCUIElementTypeAlert"))).Returns([alert.Object]);

        var prompt = new PermissionAlert<AppRoot>(new AppRoot(_app.Context.Object));

        Assert.True(prompt.IsReady());
    }

    private sealed class LoginPage(INativeIosTestContext context) : PageObjectBase<LoginPage>(context)
    {
        protected override Locator? ReadyLocator => Locator.ById("LoginForm");
    }
}
