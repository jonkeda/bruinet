namespace Bruinet.Maui.Tests;

public sealed class PageScopingTests
{
    [Fact]
    public void Page_DoesNotImplementChildContainerContract()
    {
        Assert.DoesNotContain(
            typeof(ScopedPage).GetInterfaces(),
            type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IMauiContainerObject<,>));
    }

    [Fact]
    public void ChildLookup_IsScopedToPageRoot_WithoutGlobalFallback()
    {
        var context = new Mock<IMauiTestContext>();
        context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(100),
            PageLoad = TimeSpan.FromMilliseconds(100),
            ElementFind = TimeSpan.FromMilliseconds(100),
            PollingInterval = TimeSpan.FromMilliseconds(1)
        });
        context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);

        var pageRoot = new Mock<IMauiElement>();
        pageRoot.Setup(e => e.TagName).Returns("ContentView");
        pageRoot.Setup(e => e.Visible).Returns(true);
        pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);

        var globalDuplicate = new Mock<IMauiElement>();
        context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Save")))
            .Returns(globalDuplicate.Object);
        pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Save")))
            .Returns((IMauiElement?)null);

        var page = new ScopedPage(context.Object);

        Assert.False(page.Save.IsExists());
        context.Verify(c => c.TryFindElement(It.Is<Locator>(l => l.Value == "Save")), Times.Never);
        pageRoot.Verify(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Save")), Times.Once);
    }

    [Fact]
    public void FindElements_IsScopedToPageRoot()
    {
        var context = CreateContext();
        var pageRoot = CreateUsableElement();
        var pageChildren = new[] { CreateUsableElement().Object, CreateUsableElement().Object };
        var globalChild = CreateUsableElement();

        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "Row")))
            .Returns([globalChild.Object]);
        pageRoot.Setup(e => e.FindElements(It.Is<Locator>(l => l.Value == "Row")))
            .Returns(pageChildren);

        var actual = new ScopedPage(context.Object).FindElements(Locator.ByAutomationId("Row"));

        Assert.Equal(pageChildren, actual);
        context.Verify(c => c.FindElements(It.Is<Locator>(l => l.Value == "Row")), Times.Never);
    }

    [Fact]
    public void HiddenCachedPageRoot_IsReplacedByCurrentRoot()
    {
        var context = CreateContext();
        var firstRootVisible = true;
        var firstRoot = CreateUsableElement();
        firstRoot.Setup(e => e.Visible).Returns(() => firstRootVisible);
        var secondRoot = CreateUsableElement();

        context.SetupSequence(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([firstRoot.Object])
            .Returns([secondRoot.Object]);

        var page = new ScopedPage(context.Object);
        Assert.True(page.IsLoaded());

        firstRootVisible = false;

        Assert.True(page.IsLoaded());
        context.Verify(
            c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")),
            Times.Exactly(2));
    }

    [Fact]
    public void Readiness_UsesPageLocalBusySignal_AndIgnoresGlobalDuplicate()
    {
        var context = CreateContext();
        var pageRoot = CreateUsableElement();
        var sentinel = CreateUsableElement();
        sentinel.Setup(e => e.Text).Returns("False");
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);
        pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")))
            .Returns(sentinel.Object);

        var actual = new RequiredBusyPage(context.Object).ProbeReadiness();

        Assert.Equal(ScopeReadinessState.Ready, actual.State);
        context.Verify(c => c.FindElement(It.Is<Locator>(l => l.Value == "Busy")), Times.Never);
        pageRoot.Verify(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")), Times.Once);
    }

    [Fact]
    public void MissingRequiredBusySignal_IsConfigurationFailure()
    {
        var context = CreateContext();
        var pageRoot = CreateUsableElement();
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);
        pageRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((IMauiElement?)null);

        var exception = Assert.Throws<ScopeNotReadyException>(
            () => new RequiredBusyPage(context.Object).IsBusy());

        Assert.Contains("requires a page-local busy signal", exception.Message);
    }

    [Fact]
    public void DisabledBusyPolicy_UsesRootReadinessOnly()
    {
        var context = CreateContext();
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([CreateUsableElement().Object]);

        var snapshot = new ScopedPage(context.Object).ProbeReadiness();

        Assert.Equal(ScopeReadinessState.Ready, snapshot.State);
    }

    [Fact]
    public void IsLoaded_IsAnInstantaneousRootProbe()
    {
        var context = CreateContext();
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([]);

        var loaded = new ScopedPage(context.Object).IsLoaded();

        Assert.False(loaded);
        context.Verify(
            c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")),
            Times.Once);
    }

    [Fact]
    public void WaitBusy_ObservesBusyStartingAndCompleting()
    {
        var context = CreateContext();
        var pageRoot = CreateUsableElement();
        var signal = CreateUsableElement();
        var values = new Queue<string>(["False", "True", "True", "False"]);
        signal.Setup(e => e.Text).Returns(() => values.Count > 0 ? values.Dequeue() : "False");
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);
        pageRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")))
            .Returns(signal.Object);
        var page = new RequiredBusyPage(context.Object);

        Assert.True(page.WaitBusy(true));
        Assert.True(page.WaitBusy(false));
    }

    [Fact]
    public void StaleBusySignal_ReacquiresCurrentPageRootOnce()
    {
        var context = CreateContext();
        var firstRoot = CreateUsableElement();
        var secondRoot = CreateUsableElement();
        var currentSignal = CreateUsableElement();
        currentSignal.Setup(e => e.Text).Returns("False");
        context.SetupSequence(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([firstRoot.Object])
            .Returns([secondRoot.Object]);
        firstRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Throws(new StaleElementException());
        secondRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Busy")))
            .Returns(currentSignal.Object);

        var snapshot = new RequiredBusyPage(context.Object).ProbeReadiness();

        Assert.Equal(ScopeReadinessState.Ready, snapshot.State);
        Assert.True(snapshot.RootReacquired);
        context.Verify(
            c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")),
            Times.Exactly(2));
    }

    [Fact]
    public void PopupPage_UsesDeclaredRootWithoutGlobalChildFallback()
    {
        var context = CreateContext();
        var popupRoot = CreateUsableElement();
        var popupChild = CreateUsableElement();
        context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == "PopupRoot")))
            .Returns(popupRoot.Object);
        popupRoot.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value == "Accept")))
            .Returns(popupChild.Object);

        var actual = new PopupPage(context.Object).Accept.IsExists();

        Assert.True(actual);
        context.Verify(c => c.FindElement(It.Is<Locator>(l => l.Value == "Accept")), Times.Never);
    }

    [Fact]
    public void AbsenceAssertion_PassesWhenItsContainerIsAbsentFromAPresentPage()
    {
        var context = CreateContext();
        var pageRoot = CreateUsableElement();
        context.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "ScopedPage")))
            .Returns([pageRoot.Object]);

        var page = new ScopedPage(context.Object);

        page.Actions.Done.AssertExists(false, timeoutMs: 100);
        Assert.Throws<ScopeNotReadyException>(() => page.Actions.Done.AssertExists(true, timeoutMs: 100));
    }

    [Fact]
    public void AbsenceAssertion_StillFailsWhenThePageIsMissing()
    {
        var context = CreateContext();
        context.Setup(c => c.FindElements(It.IsAny<Locator>())).Returns([]);
        var page = new ScopedPage(context.Object);

        Assert.Throws<ScopeNotReadyException>(() => page.Actions.Done.AssertExists(false, timeoutMs: 100));
    }

    private static Mock<IMauiTestContext> CreateContext()
    {
        var context = new Mock<IMauiTestContext>();
        context.Setup(c => c.Timeouts).Returns(new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(100),
            PageLoad = TimeSpan.FromMilliseconds(100),
            ElementFind = TimeSpan.FromMilliseconds(100),
            PollingInterval = TimeSpan.FromMilliseconds(1)
        });
        context.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);
        return context;
    }

    private static Mock<IMauiElement> CreateUsableElement()
    {
        var element = new Mock<IMauiElement>();
        element.Setup(e => e.TagName).Returns("ContentView");
        element.Setup(e => e.Visible).Returns(true);
        element.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        return element;
    }

    private sealed class ScopedPage(IMauiTestContext context) : PageObjectBase<ScopedPage>(context)
    {
        public override string Name => "ScopedPage";

        public Button<ScopedPage> Save => new(this, "Save");

        public HeaderActions Actions => new(this);
    }

    private sealed class HeaderActions(ScopedPage page) : ContainerObjectBase<ScopedPage, HeaderActions>(page, "HeaderActions")
    {
        public Button<HeaderActions> Done => new(this, "Done");
    }

    private sealed class RequiredBusyPage(IMauiTestContext context) : PageObjectBase<RequiredBusyPage>(context)
    {
        public override string Name => "ScopedPage";

        protected override BusySignalPolicy BusySignalPolicy => BusySignalPolicy.Required;
    }

    private sealed class PopupPage(IMauiTestContext context) : PageObjectBase<PopupPage>(context)
    {
        public override string Name => "PopupPage";

        public Button<PopupPage> Accept => new(this, "Accept");

        protected override IMauiElement FindContainerRootElement()
            => Context.FindElement(Locator.ByAutomationId("PopupRoot"));
    }
}
