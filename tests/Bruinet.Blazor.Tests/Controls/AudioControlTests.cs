namespace Bruinet.Blazor.Tests.Controls;

public class AudioControlTests
{
    private readonly Mock<IHtmlTestContext> _mockContext;
    private readonly Mock<IHtmlElement> _mockElement;
    private readonly TestPage _page;

    public AudioControlTests()
    {
        _mockContext = MockHtmlFactory.CreateMockContext();
        _mockElement = MockHtmlFactory.CreateMockMediaElement();
        MockHtmlFactory.SetupFindElement(_mockContext, _mockElement);
        _page = new TestPage(_mockContext.Object);
    }

    [Fact]
    public void Play_CallsEvaluate()
    {
        _page.TestAudio.Play();
        _mockElement.Verify(e => e.Evaluate("el => el.play()"), Times.Once);
    }

    [Fact]
    public void Pause_CallsEvaluate()
    {
        _page.TestAudio.Pause();
        _mockElement.Verify(e => e.Evaluate("el => el.pause()"), Times.Once);
    }

    [Fact]
    public void IsPaused_WhenPaused_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "paused", "true");
        Assert.True(_page.TestAudio.IsPaused());
    }

    [Fact]
    public void IsPaused_WhenNotPaused_ReturnsFalse()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "paused", "false");
        Assert.False(_page.TestAudio.IsPaused());
    }

    [Fact]
    public void IsPlaying_WhenNotPausedOrEnded_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "paused", "false");
        MockHtmlFactory.SetupDomProperty(_mockElement, "ended", "false");
        Assert.True(_page.TestAudio.IsPlaying());
    }

    [Fact]
    public void IsEnded_WhenEnded_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "ended", "true");
        Assert.True(_page.TestAudio.IsEnded());
    }

    [Fact]
    public void GetCurrentTime_ReturnsParsedValue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "currentTime", "42.5");
        Assert.Equal(42.5, _page.TestAudio.GetCurrentTime());
    }

    [Fact]
    public void Seek_CallsEvaluateWithTime()
    {
        _page.TestAudio.Seek(30);
        _mockElement.Verify(e => e.Evaluate("el => el.currentTime = 30"), Times.Once);
    }

    [Fact]
    public void GetDuration_ReturnsParsedValue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "duration", "120.5");
        Assert.Equal(120.5, _page.TestAudio.GetDuration());
    }

    [Fact]
    public void GetVolume_ReturnsParsedValue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "volume", "0.8");
        Assert.Equal(0.8, _page.TestAudio.GetVolume());
    }

    [Fact]
    public void SetVolume_CallsEvaluate()
    {
        _page.TestAudio.SetVolume(0.5);
        _mockElement.Verify(e => e.Evaluate("el => el.volume = 0.5"), Times.Once);
    }

    [Fact]
    public void IsMuted_WhenMuted_ReturnsTrue()
    {
        MockHtmlFactory.SetupDomProperty(_mockElement, "muted", "true");
        Assert.True(_page.TestAudio.IsMuted());
    }

    [Fact]
    public void Mute_CallsEvaluate()
    {
        _page.TestAudio.Mute();
        _mockElement.Verify(e => e.Evaluate("el => el.muted = true"), Times.Once);
    }

    [Fact]
    public void Unmute_CallsEvaluate()
    {
        _page.TestAudio.Unmute();
        _mockElement.Verify(e => e.Evaluate("el => el.muted = false"), Times.Once);
    }

    [Fact]
    public void GetSource_ReturnsAttribute()
    {
        MockHtmlFactory.SetupDomAttribute(_mockElement, "src", "audio.mp3");
        Assert.Equal("audio.mp3", _page.TestAudio.GetSource());
    }

    [Fact]
    public void IsExists_WhenExists_ReturnsTrue()
    {
        Assert.True(_page.TestAudio.Exists());
    }

    private sealed class TestPage : BlazorPageObjectBase<TestPage>
    {
        public TestPage(IHtmlTestContext context) : base(context) { }
        public AudioControl<TestPage> TestAudio => new(this, "test-audio");
    }
}
