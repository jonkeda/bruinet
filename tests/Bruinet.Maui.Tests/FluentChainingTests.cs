namespace Bruinet.Maui.Tests;

/// <summary>
/// Unit tests for fluent method chaining functionality.
/// Verifies that action methods return the parent page instance for method chaining.
/// </summary>
public class FluentChainingTests
{
    private readonly Mock<IMauiTestContext> _mockContext;
    private readonly TestPage _testPage;

    public FluentChainingTests()
    {
        _mockContext = new Mock<IMauiTestContext>();

        // Setup timeout settings
        var timeouts = new TimeoutSettings
        {
            DefaultWait = TimeSpan.FromMilliseconds(5000),
            PageLoad = TimeSpan.FromMilliseconds(30000),
            PollingInterval = TimeSpan.FromMilliseconds(100)
        };
        _mockContext.Setup(c => c.Timeouts).Returns(timeouts);
        _mockContext.Setup(c => c.DefaultLocatorStrategy).Returns(LocatorStrategy.AutomationId);

        var pageRoot = new Mock<IMauiElement>();
        pageRoot.Setup(e => e.Visible).Returns(true);
        pageRoot.Setup(e => e.TagName).Returns("Page");
        pageRoot.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 400, 800));
        pageRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((Locator locator) => _mockContext.Object.FindElement(locator));
        _mockContext.Setup(c => c.FindElements(It.Is<Locator>(l => l.Value == "Test Page")))
            .Returns([pageRoot.Object]);

        _testPage = new TestPage(_mockContext.Object);
    }

    #region Click Chaining Tests

    [Fact]
    public void Click_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestButton");

        // Act
        var result = _testPage.TestButton.Click();

        // Assert
        Assert.Same(_testPage, result);
    }

    [Fact]
    public void DoubleClick_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestButton");

        // Act
        var result = _testPage.TestButton.DoubleClick();

        // Assert
        Assert.Same(_testPage, result);
    }

    [Fact(Skip = "RightClick uses Selenium Actions which requires real IWebDriver/IWebElement - integration test only")]
    public void RightClick_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestButton");
        SetupActionsForRightClick(mockElement);

        // Act
        var result = _testPage.TestButton.RightClick();

        // Assert
        Assert.Same(_testPage, result);
    }

    #endregion

    #region Text Entry Chaining Tests

    [Fact]
    public void Enter_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestEntry");

        // Act
        var result = _testPage.TestEntry.Enter("test text");

        // Assert
        Assert.Same(_testPage, result);
    }

    [Fact]
    public void Enter_WithNullText_ReturnsPageWithoutAction()
    {
        // Arrange - no element setup needed since null should skip

        // Act
        var result = _testPage.TestEntry.Enter(null);

        // Assert
        Assert.Same(_testPage, result);
        // Verify no element lookup occurred
        _mockContext.Verify(c => c.TryFindElement(It.IsAny<Locator>()), Times.Never);
    }

    [Fact]
    public void Clear_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestEntry");

        // Act
        var result = _testPage.TestEntry.Clear();

        // Assert
        Assert.Same(_testPage, result);
    }

    [Fact]
    public void SetText_ReturnsPageInstance()
    {
        // Arrange
        var mockElement = SetupMockElement("TestEntry");

        // Act
        var result = _testPage.TestEntry.SetText("new text");

        // Assert
        Assert.Same(_testPage, result);
    }

    [Fact]
    public void SetText_WithNullText_ReturnsPageWithoutAction()
    {
        // Arrange - no element setup needed since null should skip

        // Act
        var result = _testPage.TestEntry.SetText(null);

        // Assert
        Assert.Same(_testPage, result);
        _mockContext.Verify(c => c.TryFindElement(It.IsAny<Locator>()), Times.Never);
    }

    /// <remarks>
    /// Submitting goes through the element's own <c>Submit</c>, not through an Enter keystroke.
    /// The distinction is the point rather than an implementation detail: a platform that can
    /// raise the control's completion command does so, and only falls back to Enter where it
    /// cannot. Asserting on <c>SendKeys(Keys.Enter)</c> here would pin the fallback as the
    /// contract and make the semantic route a regression.
    /// </remarks>
    [Fact]
    public void Submit_ReturnsPageInstanceAndSubmitsWithoutClick()
    {
        // Arrange
        var mockElement = SetupMockElement("TestEntry");

        // Act
        var result = _testPage.TestEntry.Submit();

        // Assert
        Assert.Same(_testPage, result);
        mockElement.Verify(e => e.Submit(), Times.Once);
        mockElement.Verify(e => e.Click(), Times.Never);
    }


    #endregion

    #region Method Chaining Tests


    [Fact]
    public void FluentChaining_AllowsMultipleEntriesAndClick()
    {
        // Arrange
        SetupMockElement("Username");
        SetupMockElement("Password");
        SetupMockElement("LoginButton");

        // This is the pattern we want to enable:
        // loginPage
        //     .Username.Enter("testuser")
        //     .Password.Enter("testpass")
        //     .LoginButton.Click();

        // Act
        var result = _testPage
            .Username.Enter("testuser")
            .Password.Enter("testpass")
            .LoginButton.Click();

        // Assert
        Assert.Same(_testPage, result);
    }

    #endregion


    #region Container Chaining Tests

    [Fact]
    public void ContainerControl_Button_ReturnsContainer()
    {
        // Arrange
        var containerMock = SetupMockContainerElement("TestContainer");
        SetupMockChildElement(containerMock, "ContainerButton");

        // Create button scoped to container using factory method
        var container = _testPage.TestContainer;
        var containerButton = container.ContainerButton;

        // Act
        var result = containerButton.Click();

        // Assert - Container's Button returns the container, not the page (scope-aware)
        Assert.Same(container, result);
    }

    [Fact]
    public void ContainerControl_Entry_ReturnsContainer()
    {
        // Arrange
        var containerMock = SetupMockContainerElement("TestContainer");
        SetupMockChildElement(containerMock, "ContainerEntry");

        // Create entry scoped to container using factory method
        var container = _testPage.TestContainer;
        var containerEntry = container.ContainerEntry;

        // Act
        var result = containerEntry.Enter("text");

        // Assert - Container's Entry returns the container, not the page (scope-aware)
        Assert.Same(container, result);
    }

    [Fact]
    public void ContainerControl_Parent_ReturnsPage()
    {
        // Arrange
        var containerMock = SetupMockContainerElement("TestContainer");

        // Act
        var container = _testPage.TestContainer;
        var parent = container.Parent;

        // Assert - Container's Parent returns the page
        Assert.Same(_testPage, parent);
    }

    #endregion

    #region Type Safety Tests

    [Fact]
    public void PageControl_ReturnsPage_ContainerControl_ReturnsContainer()
    {
        // Arrange
        SetupMockElement("TestButton");
        var containerMock = SetupMockContainerElement("TestContainer");
        SetupMockChildElement(containerMock, "ContainerButton");

        // Act - Page control returns page
        var pageResult = _testPage.TestButton.Click();

        // Act - Container control returns container
        var container = _testPage.TestContainer;
        var containerResult = container.ContainerButton.Click();

        // Assert
        Assert.Same(_testPage, pageResult);
        Assert.Same(container, containerResult);
    }

    #endregion

    #region Helper Methods

    private Mock<IMauiElement> SetupMockElement(string automationId, Action? onInteraction = null)
    {
        var mockElement = new Mock<IMauiElement>();
        mockElement.Setup(e => e.Visible).Returns(true);
        mockElement.Setup(e => e.Enabled).Returns(true);
        mockElement.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 100, 40));

        if (onInteraction != null)
        {
            mockElement.Setup(e => e.Click()).Callback(onInteraction);
            mockElement.Setup(e => e.Clear()).Callback(onInteraction);
            mockElement
                .Setup(e => e.SendKeys(It.IsAny<string>(), It.IsAny<TextInputMethod>()))
                .Callback<string, TextInputMethod>((_, _) => onInteraction());
        }

        _mockContext.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(mockElement.Object);
        _mockContext.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(mockElement.Object);

        return mockElement;
    }

    private Mock<IMauiElement> SetupMockContainerElement(string automationId)
    {
        var mockElement = new Mock<IMauiElement>();
        mockElement.Setup(e => e.Visible).Returns(true);
        mockElement.Setup(e => e.Enabled).Returns(true);
        mockElement.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(0, 0, 200, 120));

        _mockContext.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(mockElement.Object);
        _mockContext.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == automationId)))
            .Returns(mockElement.Object);

        return mockElement;
    }

    private void SetupMockChildElement(Mock<IMauiElement> containerMock, string childAutomationId)
    {
        var childMockElement = new Mock<IMauiElement>();
        childMockElement.Setup(e => e.Visible).Returns(true);
        childMockElement.Setup(e => e.Enabled).Returns(true);
        childMockElement.Setup(e => e.Rect).Returns(new System.Drawing.Rectangle(10, 10, 80, 32));

        // Set up the container element to find the child element
        containerMock.Setup(e => e.TryFindElement(It.Is<Locator>(l => l.Value.Contains(childAutomationId))))
            .Returns(childMockElement.Object);
    }

    private void SetupActionsForRightClick(Mock<IMauiElement> mockElement)
    {
        // Set up mock driver with UnwrapDriver
        var mockDriver = new Mock<IMauiDriver>();
        _mockContext.Setup(c => c.Driver).Returns(mockDriver.Object);

        // Note: RightClick uses Selenium Actions which require real IWebDriver/IWebElement
        // For unit tests, we skip RightClick or use integration tests
        // This test is expected to fail until we have real Appium instances
    }

    #endregion

    #region Test Page Object

    /// <summary>
    /// Test page object using the fluent CRTP pattern.
    /// </summary>
    private class TestPage : PageObjectBase<TestPage>
    {
        public TestPage(IMauiTestContext context) : base(context) { }

        public override string Name => "Test Page";
        public override bool IsLoaded() => true;

        // Controls with fluent chaining - return TestPage
        public Button<TestPage> TestButton => new(this, Locator.ByAutomationId("TestButton"));
        public Entry<TestPage> TestEntry => new(this, Locator.ByAutomationId("TestEntry"));
        public TestContainer TestContainer => new(this, Locator.ByAutomationId("TestContainer"));

        // Login form example controls
        public Entry<TestPage> Username => new(this, Locator.ByAutomationId("Username"));
        public Entry<TestPage> Password => new(this, Locator.ByAutomationId("Password"));
        public Button<TestPage> LoginButton => new(this, Locator.ByAutomationId("LoginButton"));
    }

    /// <summary>
    /// Test container using the new scope-aware pattern.
    /// </summary>
    private class TestContainer : ContainerObjectBase<TestPage, TestContainer>
    {
        public TestContainer(IMauiScope<TestPage> parentScope, Locator locator)
            : base(parentScope, locator) { }

        // Controls return TestContainer (the containing scope)
        public Button<TestContainer> ContainerButton => new(this, Locator.ByAutomationId("ContainerButton"));
        public Entry<TestContainer> ContainerEntry => new(this, Locator.ByAutomationId("ContainerEntry"));
    }

    #endregion
}
