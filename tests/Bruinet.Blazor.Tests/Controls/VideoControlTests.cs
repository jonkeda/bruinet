namespace Bruinet.Blazor.Tests.Controls;

public class VideoControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public VideoControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockMediaElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void Play_CallsEvaluate()
    {
        _page.TestVideo.Play();
        _mockElement.Verify(e => e.Evaluate("el => el.play()"), Times.Once);
    }

    [Fact]
    public void Pause_CallsEvaluate()
    {
        _page.TestVideo.Pause();
        _mockElement.Verify(e => e.Evaluate("el => el.pause()"), Times.Once);
    }

    [Fact]
    public void IsPaused_WhenPaused_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "paused", "true");
        Assert.True(_page.TestVideo.IsPaused());
    }

    [Fact]
    public void GetCurrentTime_ReturnsParsedValue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "currentTime", "42.5");
        Assert.Equal(42.5, _page.TestVideo.GetCurrentTime());
    }

    [Fact]
    public void Seek_CallsEvaluateWithTime()
    {
        _page.TestVideo.Seek(30);
        _mockElement.Verify(e => e.Evaluate("el => el.currentTime = 30"), Times.Once);
    }

    [Fact]
    public void GetSource_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "video.mp4");
        Assert.Equal("video.mp4", _page.TestVideo.GetSource());
    }

    [Fact]
    public void GetPoster_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "poster", "poster.jpg");
        Assert.Equal("poster.jpg", _page.TestVideo.GetPoster());
    }

    [Fact]
    public void IsPlaying_WhenNotPausedOrEnded_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "paused", "false");
        MockHtmlFactory.SetupDomProperty(_mockElement, "ended", "false");
        Assert.True(_page.TestVideo.IsPlaying());
    }

    [Fact]
    public void GetVolume_ReturnsParsedValue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "volume", "0.8");
        Assert.Equal(0.8, _page.TestVideo.GetVolume());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
        public VideoControl<TestPage> TestVideo => new(this, "test-video");
    }
}
