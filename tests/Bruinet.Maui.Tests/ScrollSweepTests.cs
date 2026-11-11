using System.Drawing;
using Bruinet.Maui.Appium;

namespace Bruinet.Maui.Tests;

public sealed class ScrollSweepTests
{
    private static readonly IReadOnlyDictionary<string, object> Plain = new Dictionary<string, object> { ["elementId"] = "scroller" };
    private static readonly Rectangle Area = new(0, 0, 1000, 2000);

    private static bool IsPlain(IReadOnlyDictionary<string, object> swipe) => swipe.ContainsKey("elementId");

    [Fact]
    public void AListAtItsEnd_TurnsBeforeTheLaterStarts()
    {
        // At the bottom: nothing moves down; the view is one swipe up.
        var swipes = new List<(string Direction, bool Plain)>();
        var upSwipes = 0;
        bool? Swipe(IReadOnlyDictionary<string, object> parameters)
        {
            var direction = (string)parameters["direction"];
            swipes.Add((direction, IsPlain(parameters)));
            if (direction == "up")
                upSwipes++;
            return direction == "up";
        }

        var found = AppiumMauiDriver.Sweep(Swipe, _ => { }, Plain, ["down", "up"], () => upSwipes > 0,
            budgetMs: 60_000, atLeastOneSwipe: false, Area);

        Assert.True(found);
        Assert.Equal([("down", true), ("up", true)], swipes);
    }

    [Fact]
    public void APlainSwipeThatIsCaught_IsRetriedFromTheLaterStarts()
    {
        // A pad under the plain swipe keeps every drag; a later start moves the page down.
        var swipes = new List<(string Direction, bool Plain)>();
        var laterDown = 0;
        bool? Swipe(IReadOnlyDictionary<string, object> parameters)
        {
            var direction = (string)parameters["direction"];
            var plain = IsPlain(parameters);
            swipes.Add((direction, plain));
            if (direction == "down" && !plain)
            {
                laterDown++;
                return true;
            }

            return false;
        }

        var found = AppiumMauiDriver.Sweep(Swipe, _ => { }, Plain, ["down", "up"], () => laterDown > 0,
            budgetMs: 60_000, atLeastOneSwipe: false, Area);

        Assert.True(found);
        Assert.Equal([("down", true), ("up", true), ("down", false)], swipes);
    }

    [Fact]
    public void WithoutBounds_OnlyThePlainSwipeRuns()
    {
        var swipes = 0;
        var found = AppiumMauiDriver.Sweep(_ => { swipes++; return false; }, _ => { }, Plain, ["down", "up"], () => false,
            budgetMs: 60_000, atLeastOneSwipe: false);

        Assert.False(found);
        Assert.Equal(2, swipes);
    }
}
