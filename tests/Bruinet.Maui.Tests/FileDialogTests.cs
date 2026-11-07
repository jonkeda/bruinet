using Bruinet.Maui.Controls.Dialogs;

namespace Bruinet.Maui.Tests;

public class FileDialogTests
{
    [Fact]
    public void Choose_AnswersTheDialogWithTheFullPathAndReturnsTheScope()
    {
        var app = new Mock<IMauiElement>();
        var context = new Mock<IMauiTestContext>();
        context.Setup(c => c.AppElement).Returns(app.Object);
        context.Setup(c => c.Timeouts).Returns(new TimeoutSettings { DefaultWait = 4321 });
        var page = new TestPage(context.Object);

        var returned = new FileDialog<TestPage>(page).Choose("photo.png");

        Assert.Same(page, returned);
        app.Verify(a => a.AnswerFileDialog(Path.GetFullPath("photo.png"), 4321), Times.Once);
    }

    [Fact]
    public void Choose_OnAPlatformWithoutARoute_ThrowsRouteUnavailable()
    {
        var app = new Mock<IMauiElement> { CallBase = true };
        var context = new Mock<IMauiTestContext>();
        context.Setup(c => c.AppElement).Returns(app.Object);
        context.Setup(c => c.Timeouts).Returns(new TimeoutSettings());

        Assert.Throws<RouteUnavailableException>(
            () => new FileDialog<TestPage>(new TestPage(context.Object)).Choose("photo.png", 10));
    }

    private sealed class TestPage(IMauiTestContext context) : PageObjectBase<TestPage>(context)
    {
        public override string Name => "Test Page";

        public override bool IsLoaded() => true;
    }
}
