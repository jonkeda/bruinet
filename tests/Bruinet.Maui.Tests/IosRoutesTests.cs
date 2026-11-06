using Bruinet.Maui.Appium;

namespace Bruinet.Maui.Tests;

/// <summary>
/// The decisions inside the iOS routes, apart from the tree they read. Whether the locators find
/// that tree is for a simulator (<c>.my/ios/plan.md</c>).
/// </summary>
public class IosRoutesTests
{
    [Theory]
    [InlineData("Back")]
    [InlineData("Controls")]
    [InlineData("BackButton")]
    [InlineData(null)]
    public void ALeadingButton_IsTheBackButton_WhateverItsName(string? name)
        => Assert.True(IosPlatform.IsBackButton(name, offsetInBar: 8, barWidth: 430));

    [Theory]
    [InlineData("OK")]
    [InlineData("Menu")]
    [InlineData("FlyoutIcon")]
    public void TheFlyoutOpener_IsNotTheBackButton(string name)
        => Assert.False(IosPlatform.IsBackButton(name, offsetInBar: 8, barWidth: 430));

    [Fact]
    public void ATrailingToolbarItem_IsNotTheBackButton()
        => Assert.False(IosPlatform.IsBackButton("Save", offsetInBar: 370, barWidth: 430));

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    public void TheFlyout_IsOpen_OnlyWhenItsTableIsVisible(string? visible, bool open)
        => Assert.Equal(open, IosPlatform.IsVisible(visible));

    [Fact]
    public void AWheel_IsTurnedBackToItsFirstRow_ThenOnToTheIndex()
    {
        var wheel = new FakeWheel(rows: 5, at: 3);

        AppiumMauiElement.TurnWheel("Fruit", wheel.Step, index: 2, budgetMs: 1_000, () => 0);

        Assert.Equal(2, wheel.At);
    }

    [Fact]
    public void AnIndexPastTheWheelsEnd_IsOutOfRange()
    {
        var wheel = new FakeWheel(rows: 3, at: 0);

        var failure = Assert.Throws<ArgumentOutOfRangeException>(
            () => AppiumMauiElement.TurnWheel("Fruit", wheel.Step, index: 5, budgetMs: 1_000, () => 0));

        Assert.Contains("shows 3 item(s)", failure.Message);
    }

    [Fact]
    public void ASpentBudget_StopsTheTurn_AndSaysHowFarItGot()
    {
        var wheel = new FakeWheel(rows: 50, at: 0);
        var elapsed = 0L;

        var failure = Assert.Throws<TimeoutException>(() => AppiumMauiElement.TurnWheel(
            "Fruit", order => { elapsed += 100; return wheel.Step(order); }, index: 40, budgetMs: 1_000, () => elapsed));

        Assert.Contains("ran out at row 9", failure.Message);
        Assert.Equal(10, wheel.Steps);
    }

    [Fact]
    public void ScrollIntoView_OnASpentBudget_TriesOnlyTheFirstWay()
    {
        var tried = new List<int>();

        var scrolled = AppiumMauiElement.TryInOrder(
            [() => { tried.Add(1); return false; }, () => { tried.Add(2); return true; }],
            budgetMs: 0,
            () => 0);

        Assert.False(scrolled);
        Assert.Equal([1], tried);
    }

    [Fact]
    public void ScrollIntoView_StopsAtTheFirstWayThatWorks()
    {
        var tried = new List<int>();

        var scrolled = AppiumMauiElement.TryInOrder(
            [() => { tried.Add(1); return false; }, () => { tried.Add(2); return true; }, () => { tried.Add(3); return true; }],
            budgetMs: 1_000,
            () => 0);

        Assert.True(scrolled);
        Assert.Equal([1, 2], tried);
    }

    /// <summary>A wheel of <c>rows</c> rows standing at <c>at</c>.</summary>
    private sealed class FakeWheel(int rows, int at)
    {
        public int At { get; private set; } = at;

        public int Steps { get; private set; }

        public bool Step(string order)
        {
            Steps++;
            var next = order == "previous" ? At - 1 : At + 1;
            if (next < 0 || next >= rows)
            {
                return false;
            }

            At = next;
            return true;
        }
    }
}
