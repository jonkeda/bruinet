using Bruinet.Maui.UITests.Pages;

namespace Bruinet.Maui.UITests.Tests.CommunityToolkit;

/// <summary>
/// UI tests for the CommunityToolkit RatingView.
/// </summary>
[Collection("Maui")]
[Trait("Category", "UITest")]
[Trait("Control", "RatingView")]
public class RatingViewTests
{
    private readonly MauiFixture _fixture;

    public RatingViewTests(MauiFixture fixture)
    {
        _fixture = fixture;
        _fixture.Open(SamplePage.CommunityToolkit);
    }

    private CommunityToolkitTestPage GetPage() => new(_fixture.Context);

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Exists")]
    public Task RatingView_IsExists_ReturnsTrue()
    {
        GetPage().TestRatingView.AssertExists();
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "GetValue")]
    public Task RatingView_ReadsInitialRangeFromApp()
    {
        var page = GetPage();

        Assert.Equal(2, page.TestRatingView.GetValue());
        page.TestRatingView.AssertValue(2)
            .TestRatingView.AssertMinimum(0)
            .TestRatingView.AssertMaximum(5)
            .TestRatingView.AssertStep(1)
            .TestRatingView.AssertReadOnly(false);
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetValue")]
    public Task RatingView_SetValue_UpdatesRatingAndLabel()
    {
        GetPage().TestRatingView.SetValue(4)
            .TestRatingView.AssertValue(4)
            .RatingStatusLabel.AssertText("rating: 4");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "TapStar")]
    public Task RatingView_TapStar_SetsRatingToThatStar()
    {
        GetPage().TestRatingView.TapStar(5)
            .RatingStatusLabel.AssertText("rating: 5")
            .TestRatingView.TapStar(1)
            .RatingStatusLabel.AssertText("rating: 1");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "Increment")]
    public Task RatingView_IncrementAndDecrement_MoveOneStar()
    {
        GetPage().TestRatingView.Increment()
            .TestRatingView.AssertValue(3)
            .TestRatingView.Decrement()
            .TestRatingView.Decrement()
            .TestRatingView.AssertValue(1)
            .RatingStatusLabel.AssertText("rating: 1");
        return Task.CompletedTask;
    }

    [Fact(Timeout = TestConstants.DefaultTestTimeoutMs)]
    [Trait("Method", "SetValue")]
    public Task RatingView_SetValue_OutsideStars_Throws()
    {
        var page = GetPage();

        Assert.Throws<ArgumentOutOfRangeException>(() => page.TestRatingView.SetValue(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => page.TestRatingView.SetValue(6));
        page.TestRatingView.AssertValue(2);
        return Task.CompletedTask;
    }
}
