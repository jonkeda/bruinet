using Bruinet.Maui.Appium;

namespace Bruinet.Maui.Tests;

public sealed class SwipeSpanTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ATabStripsHeight_LeavesNoSwipe(bool android)
        => Assert.Null(AppiumMauiElement.SwipeSpan(115, 44, android));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AListsHeight_SwipesInsideItsBounds(bool android)
    {
        var span = AppiumMauiElement.SwipeSpan(251, 588, android);

        Assert.NotNull(span);
        Assert.InRange(span.Value.Near, 252, 838);
        Assert.InRange(span.Value.Far, span.Value.Near + 40, 838);
    }
}
