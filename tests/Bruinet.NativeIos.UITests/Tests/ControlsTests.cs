namespace Bruinet.NativeIos.UITests.Tests;

/// <summary>Each input control acts once and confirms its effect; the app's status line says what it saw.</summary>
[Collection(GalleryCollection.Name)]
public sealed class ControlsTests(GalleryFixture gallery)
{
    private ControlsPage Open()
    {
        gallery.Open("controls");
        var page = new ControlsPage(gallery.Context);
        page.AssertIdle();
        return page;
    }

    [Fact]
    public void ASwitch_ChecksAndUnchecks()
    {
        var page = Open();

        page.Agree.Check();
        page.Status.AssertText("Agree on");
        page.Agree.Uncheck();
        page.Status.AssertText("Agree off");
        page.Agree.AssertChecked(false);
        page.Notifications.Check();
        page.Status.AssertText("Notifications on");
    }

    [Fact]
    public void ASegmentedControl_SelectsASegment()
    {
        var page = Open();

        page.Size.AssertSelectedText("Small");
        page.Size.SelectByText("Large");
        page.Status.AssertText("Size Large");
        page.Size.AssertSelectedText("Large");
        Assert.Equal(["Small", "Medium", "Large"], page.Size.GetItemTexts());
    }

    [Fact]
    public void APickerWheel_TurnsToAnOption()
    {
        var page = Open();

        page.Color.SelectByText("Navy");
        page.Status.AssertText("Color Navy");
        page.Color.AssertSelectedText("Navy");
    }

    /// <summary>XCUITest places a slider's thumb only roughly: SetValue lands within 5% of the range.</summary>
    [Fact]
    public void ASlider_IsSetToAValue()
    {
        var page = Open();

        page.Volume.AssertValue(20);
        page.Volume.SetValue(65);

        Assert.InRange(page.Volume.GetValue() ?? 0, 60, 70);
        page.Status.AssertTextStartsWith("Volume ");
    }

    [Fact]
    public void AStepper_StepsUp()
    {
        var page = Open();

        page.Quantity.Increment.Click();

        page.Status.AssertText("Quantity 2");
    }

    [Fact]
    public void AProgressView_ReadsItsProgress()
    {
        var page = Open();

        page.Progress.AssertValue(40);
    }

    [Fact]
    public void ATextField_TakesText()
    {
        var page = Open();

        page.Notes.SetText("Weekly check");

        page.Notes.AssertText("Weekly check");
    }

    [Fact]
    public void AnImage_IsDescribed()
    {
        var page = Open();

        page.Logo.AssertAccessibilityLabel("Bruinet logo");
    }

    /// <summary>A scroll view keeps its content in the tree off the screen: the call scrolls it into view, then taps it once.</summary>
    [Fact]
    public void AControlBelowTheFold_IsScrolledIntoView()
    {
        var page = Open();

        page.BottomButton.Click();

        page.Status.AssertText("Bottom tapped");
    }

    [Fact]
    public void Hover_HasNoRouteOnIos()
    {
        var page = Open();

        Assert.Throws<RouteUnavailableException>(() => page.Agree.Hover());
    }
}
