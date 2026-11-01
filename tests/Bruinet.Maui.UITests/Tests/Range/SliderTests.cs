using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.Range;

/// <summary>
/// UI tests for the Slider control in the RangeTestView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "Slider")]
public class SliderTests
{
    private readonly MauiFixture _fixture;

    public SliderTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.Range);
    }

    private RangeTestPage GetPage()
    {
        return new(_fixture.Context);
    }

    /// <summary>
    /// Verifies that the Slider exists on the page.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsExists")]
    public Task Slider_IsExists_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestSlider.AssertExists();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider is visible.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsVisible")]
    public Task Slider_IsVisible_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestSlider.AssertVisible();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider is enabled.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "IsEnabled")]
    public Task Slider_IsEnabled_ReturnsTrue()
    {
        var page = GetPage();
        // Assert
        page.TestSlider.AssertEnabled();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider value updates when set.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetValue")]
    public Task Slider_SetValue_UpdatesDisplay()
    {
        var page = GetPage();
        // Act
        page.TestSlider.SetValue(75)
            .SliderValueLabel.AssertTextContains("75");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider respects minimum value bounds.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MinValue")]
    public Task Slider_SetValue_RespectsBounds_Min()
    {
        var page = GetPage();
        // Act & Assert
        page.TestSlider.SetValue(0)
            .SliderValueLabel.AssertTextContains("0");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider respects maximum value bounds.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MaxValue")]
    public Task Slider_SetValue_RespectsBounds_Max()
    {
        var page = GetPage();
        // Act & Assert
        page.TestSlider.SetValue(100)
            .SliderValueLabel.AssertTextContains("100");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider status message updates on value change.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "StatusUpdate")]
    public Task Slider_SetValue_UpdatesStatus()
    {
        var page = GetPage();
        // Act
        // Not 50: the slider starts there, and a value that does not change sends no status.
        page.TestSlider.SetValue(75)
            .StatusLabel.AssertTextContains("Slider value");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the Slider can be reset to its initial value.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Reset")]
    public Task Slider_Reset_RestoresInitialValue()
    {
        var page = GetPage();
        // Arrange - change value
        page.TestSlider.SetValue(75)
            .SliderValueLabel.AssertTextContains("75");

        // Act - reset
        page.ResetButton.Click()
            .SliderValueLabel.AssertTextContains("50");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that multiple slider value changes are reflected correctly.
    /// </summary>
    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "MultipleChanges")]
    public Task Slider_MultipleValueChanges_UpdatesDisplay()
    {
        var page = GetPage();
        // Act & Assert
        page.TestSlider.SetValue(25)
            .SliderValueLabel.AssertTextContains("25")
            .TestSlider.SetValue(75)
            .SliderValueLabel.AssertTextContains("75")
            .TestSlider.SetValue(50)
            .SliderValueLabel.AssertTextContains("50");

        return Task.CompletedTask;
    }
}
