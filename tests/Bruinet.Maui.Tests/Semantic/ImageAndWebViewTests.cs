using System.Drawing;
using Bruinet.Maui.Controls.Media;

namespace Bruinet.Maui.Tests.Semantic;

// What an image and a web view read from their element: when an image counts as loaded, which title and URL a web view gives.
public sealed class ImageAndWebViewTests : SemanticControlTestsBase
{
    private const string Id = "Shown";

    private Mock<IMauiElement> Given(Action<Mock<IMauiElement>> setup)
    {
        var element = CreateElement(Id, 0, 0, 0, 0);
        setup(element);
        Context.Setup(c => c.FindElement(It.Is<Locator>(l => l.Value == Id))).Returns(element.Object);
        Context.Setup(c => c.TryFindElement(It.Is<Locator>(l => l.Value == Id))).Returns(element.Object);
        return element;
    }

    private Image<TestPage> Image => new(Page, Id);

    private WebView<TestPage> WebView => new(Page, Id);

    [Theory]
    [InlineData("logo.png", "False", true)]
    [InlineData("logo.png", "True", false)]
    [InlineData("", null, false)]
    public void AnImageTheAppDescribes_IsLoaded_WhenItHasASourceAndIsNotLoadingIt(string source, string? loading, bool loaded)
    {
        Given(element =>
        {
            element.Setup(e => e.ReadState("Source")).Returns(source);
            element.Setup(e => e.ReadState("IsLoading")).Returns(loading);
            element.Setup(e => e.Size).Returns(new Size(64, 64));
        });

        Assert.Equal(loaded, Image.IsLoaded());
    }

    [Theory]
    [InlineData(64, 64, true)]
    [InlineData(64, 0, false)]
    public void AnImageTheAppDoesNotDescribe_IsLoaded_WhenItHasASize(int width, int height, bool loaded)
    {
        Given(element => element.Setup(e => e.Size).Returns(new Size(width, height)));

        Assert.Equal(loaded, Image.IsLoaded());
    }

    [Fact]
    public void AWebViewsTitle_IsItsName_OrItsTextWhenItHasNoName()
    {
        var element = Given(e => e.Setup(x => x.Text).Returns("https://example.test/Home"));

        Assert.Equal("https://example.test/Home", WebView.GetPageTitle());

        element.Setup(e => e.Name).Returns("Home");
        Assert.Equal("Home", WebView.GetPageTitle());
    }

    [Fact]
    public void AUrlCheckThatIgnoresCase_PassesWhereTheOrdinalOneFails()
    {
        Given(e => e.Setup(x => x.Text).Returns("https://example.test/Home"));

        WebView.AssertUrlContainsIgnoreCase("/home");
        Assert.ThrowsAny<Exception>(() => WebView.AssertUrlContains("/home", timeoutMs: 0));
        Assert.ThrowsAny<Exception>(() => WebView.AssertUrlContainsIgnoreCase("/about"));
    }
}
