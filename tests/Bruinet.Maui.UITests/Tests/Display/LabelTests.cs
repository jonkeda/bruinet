using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Display;

/// <summary>
/// UI tests for the Label control in the DisplayTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Label")]
public class LabelTests
{
    private readonly MauiFixture _fixture;

    public LabelTests(MauiFixture fixture)
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
    /// Verifies that the Label displays text correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetText")]
    public Task Label_DisplaysText_ReturnsExpectedText()
    {
        var page = GetPage();
        // Assert
        page.TestLabel.AssertTextContains("test label");
        return Task.CompletedTask;
    }
}
