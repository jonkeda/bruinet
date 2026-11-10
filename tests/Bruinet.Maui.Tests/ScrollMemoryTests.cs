using Bruinet.Core.Locators;
using Bruinet.Maui.Appium;

namespace Bruinet.Maui.Tests;

public sealed class ScrollMemoryTests
{
    private static readonly Locator Toolbox = Locator.ByAutomationId("Toolbox");

    [Fact]
    public void AViewThePageMovedDownPast_IsSearchedUpFirst()
    {
        var memory = new ScrollMemory();
        memory.Seen(Toolbox);
        memory.Moved("down");
        memory.Moved("down");
        memory.Moved("up");

        Assert.Equal(["up", "down"], memory.Directions(Toolbox));
    }

    [Fact]
    public void AnUnseenViewOrNoNetMove_KeepsDownFirst()
    {
        var memory = new ScrollMemory();
        memory.Moved("down");
        Assert.Equal(["down", "up"], memory.Directions(Toolbox));

        memory.Seen(Toolbox);
        memory.Moved("down");
        memory.Moved("up");
        memory.Moved("left");
        Assert.Equal(["down", "up"], memory.Directions(Toolbox));
    }

    [Fact]
    public void OnlyMovesSinceTheLastSightingCount()
    {
        var memory = new ScrollMemory();
        memory.Seen(Toolbox);
        memory.Moved("down");
        memory.Seen(Toolbox);
        memory.Moved("up");

        Assert.Equal(["down", "up"], memory.Directions(Toolbox));
    }
}
