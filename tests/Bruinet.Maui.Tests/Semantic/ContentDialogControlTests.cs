namespace Bruinet.Maui.Tests.Semantic;

public class ContentDialogControlTests : SemanticControlTestsBase
{
    private readonly Mock<IMauiElement> _app = new();

    public ContentDialogControlTests()
    {
        Context.Setup(c => c.AppElement).Returns(_app.Object);
    }

    [Fact]
    public void ContentDialog_DialogButton_UsesActiveDialogRoot()
    {
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        var deleteButton = CreateInvokableElement("DialogDelete", 10, 150, 80, 40);
        dialogRoot
            .Setup(e => e.TryFindElement(
                It.Is<Locator>(l => l.Strategy == LocatorStrategy.Name && l.Value == "Delete")))
            .Returns(deleteButton.Object);
        _app
            .Setup(a => a.TryFindActiveDialog())
            .Returns(dialogRoot.Object);

        var exists = Page.Dialog.DialogButton("Delete").Exists();

        Assert.True(exists);
        dialogRoot.Verify(e => e.TryFindElement(It.IsAny<Locator>()), Times.Once);
        Context.Verify(c => c.TryFindElement(It.IsAny<Locator>()), Times.Never);
    }

    [Fact]
    public void ContentDialog_DialogButton_DoesNotFallBackToParentScope()
    {
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        dialogRoot
            .Setup(e => e.TryFindElement(It.IsAny<Locator>()))
            .Returns((IMauiElement?)null);
        _app
            .Setup(a => a.TryFindActiveDialog())
            .Returns(dialogRoot.Object);
        Context.Setup(c => c.TryFindElement(It.IsAny<Locator>())).Returns(CreateElement("Delete", 0, 0, 80, 40).Object);

        var exists = Page.Dialog.DialogButton("Delete").Exists();

        Assert.False(exists);
        Context.Verify(c => c.TryFindElement(It.IsAny<Locator>()), Times.Never);
    }

    [Fact]
    public void ContentDialog_PromptInput_UsesActiveDialogRoot()
    {
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        var promptInput = CreateElement("PromptInput", 20, 80, 260, 40);
        dialogRoot
            .Setup(e => e.TryFindElement(
                It.Is<Locator>(l => l.Strategy == LocatorStrategy.ControlType && l.Value == "entry")))
            .Returns(promptInput.Object);
        _app
            .Setup(a => a.TryFindActiveDialog())
            .Returns(dialogRoot.Object);

        var exists = Page.Dialog.PromptInput.Exists();

        Assert.True(exists);
        dialogRoot.Verify(e => e.TryFindElement(It.IsAny<Locator>()), Times.Once);
    }

    [Fact]
    public void ContentDialog_ButtonClick_ReturnsDialogForDismissalWait()
    {
        var dismissed = false;
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        var okButton = CreateInvokableElement("DialogOk", 10, 150, 80, 40);
        okButton.Setup(e => e.Invoke()).Callback(() => dismissed = true);

        dialogRoot.Setup(e => e.TagName).Returns("ContentDialog");
        dialogRoot.Setup(e => e.TryFindElement(It.IsAny<Locator>())).Returns(okButton.Object);
        _app
            .Setup(a => a.TryFindActiveDialog())
            .Returns(() => dismissed ? null : dialogRoot.Object);

        var dismissedResult = Page.Dialog
            .DialogButton("OK")
            .Click(timeoutMs: 100)
            .WaitExists(false, timeoutMs: 100);

        Assert.True(dismissedResult);
        Assert.True(dismissed);
    }

    [Fact]
    public void ContentDialog_GetTitle_WhenTheRootHasNoName_ReadsTheAlertTitle()
    {
        // As an Android AppCompat alert publishes it: a nameless root with an alertTitle child.
        var dialogRoot = CreateElement("parentPanel", 0, 0, 300, 200);
        dialogRoot.Setup(e => e.Name).Returns(string.Empty);
        var title = CreateElement("alertTitle", 10, 10, 280, 30);
        title.Setup(e => e.Text).Returns("Delete todo?");
        dialogRoot
            .Setup(e => e.TryFindElement(
                It.Is<Locator>(l => l.Strategy == LocatorStrategy.XPath && l.Value.Contains(":id/alertTitle"))))
            .Returns(title.Object);
        _app.Setup(a => a.TryFindActiveDialog()).Returns(dialogRoot.Object);

        Assert.Equal("Delete todo?", Page.Dialog.GetTitle());
    }

    [Fact]
    public void ContentDialog_GetTitle_PrefersTheRootsName()
    {
        // As a WinUI ContentDialog publishes it: the title is the root's own name.
        var dialogRoot = CreateElement("ContentDialog", 0, 0, 300, 200);
        dialogRoot.Setup(e => e.Name).Returns("Discard changes?");
        _app.Setup(a => a.TryFindActiveDialog()).Returns(dialogRoot.Object);

        Assert.Equal("Discard changes?", Page.Dialog.GetTitle());
        dialogRoot.Verify(e => e.TryFindElement(It.IsAny<Locator>()), Times.Never);
    }
}
