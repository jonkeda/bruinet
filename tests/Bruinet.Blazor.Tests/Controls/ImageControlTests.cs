using Bruinet.Core.Exceptions;

namespace Bruinet.Blazor.Tests.Controls;

public class ImageControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public ImageControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void GetSource_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "image.png");
        Assert.Equal("image.png", _page.TestImage.GetSource());
    }

    [Fact]
    public void GetAltText_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "alt", "A nice image");
        Assert.Equal("A nice image", _page.TestImage.GetAltText());
    }

    [Fact]
    public void IsLoaded_WhenComplete_ReturnsTrue()
    {
        MockHtmlFactory.SetupEvaluate(_mockElement, "img => img.complete && img.naturalWidth > 0", true);
        Assert.True(_page.TestImage.IsLoaded());
    }

    [Fact]
    public void IsLoaded_WhenNotComplete_ReturnsFalse()
    {
        MockHtmlFactory.SetupEvaluate(_mockElement, "img => img.complete && img.naturalWidth > 0", false);
        Assert.False(_page.TestImage.IsLoaded());
    }

    [Fact]
    public void GetNaturalWidth_ReturnsValue()
    {
        MockHtmlFactory.SetupEvaluate(_mockElement, "img => img.naturalWidth", 800);
        Assert.Equal(800, _page.TestImage.GetNaturalWidth());
    }

    [Fact]
    public void GetNaturalHeight_ReturnsValue()
    {
        MockHtmlFactory.SetupEvaluate(_mockElement, "img => img.naturalHeight", 600);
        Assert.Equal(600, _page.TestImage.GetNaturalHeight());
    }

    [Fact]
    public void AssertSource_WhenMatches_DoesNotThrow()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "image.png");
        _page.TestImage.AssertSource("image.png");
    }

    [Fact]
    public void AssertSource_WhenMismatch_Throws()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "other.png");
        Assert.Throws<ControlAssertionException>(() => _page.TestImage.AssertSource("image.png"));
    }

    [Fact]
    public void AssertSourceContains_WhenContains_DoesNotThrow()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "path/to/image.png");
        _page.TestImage.AssertSourceContains("image.png");
    }

    [Fact]
    public void AssertAltText_WhenMatches_DoesNotThrow()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "alt", "Nice image");
        _page.TestImage.AssertAltText("Nice image");
    }

    [Fact]
    public void AssertAltText_WhenMismatch_Throws()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "alt", "Wrong alt");
        Assert.Throws<ControlAssertionException>(() => _page.TestImage.AssertAltText("Nice image"));
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestImage.IsExists());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
        public ImageControl<TestPage> TestImage => new(this, "test-image");
    }
}
