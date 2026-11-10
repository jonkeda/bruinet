using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Display;

/// <summary>
/// UI tests for the Image control in the DisplayTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Image")]
public class ImageTests
{
    private readonly MauiFixture _fixture;

    public ImageTests(MauiFixture fixture)
    {
        _fixture = fixture;
        // Navigate to Display tab
        _fixture.Open(SamplePage.Display);
    }

    private DisplayTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the Image exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task Image_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestImage.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Image is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task Image_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        // AssertVisibleAfterScroll, not AssertVisible: the page is a ScrollView and this control
        // sits below the fold, so "is it on screen right now" is false for a perfectly healthy
        // control. Which controls happen to start above the fold depends on window size, so
        // asserting that would encode this machine's screen rather than the app's behaviour.
        page.TestImage.AssertVisibleAfterScroll();
        return Task.CompletedTask;
    }
}
