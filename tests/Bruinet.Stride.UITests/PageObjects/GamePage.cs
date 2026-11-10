namespace Bruinet.Stride.UITests.PageObjects;

/// <summary>
/// Page object for the main game view (HUD).
/// </summary>
public class GamePage : PageObjectBase<GamePage>
{
    public override string Name => "Game Page";
    protected override Locator ReadyLocator => Locator.ByAutomationId("HUD");

    public GamePage(IStrideTestContext context) : base(context) { }

    // HUD Controls
    public TextBlock<GamePage> GameTitle => TextBlock("GameTitle");
    public TextBlock<GamePage> PositionDisplay => TextBlock("PositionDisplay");
    public TextBlock<GamePage> EscHint => TextBlock("EscHint");
    public TextBlock<GamePage> MovementHint => TextBlock("MovementHint");

    // Actions
    public GamePage OpenSettings()
    {
        PressKey(VirtualKey.Escape);
        return this;
    }

    public GamePage MoveNorth(int durationMs = 500)
    {
        HoldKey(VirtualKey.W, durationMs);
        return this;
    }

    public GamePage MoveSouth(int durationMs = 500)
    {
        HoldKey(VirtualKey.S, durationMs);
        return this;
    }

    public GamePage MoveEast(int durationMs = 500)
    {
        HoldKey(VirtualKey.D, durationMs);
        return this;
    }

    public GamePage MoveWest(int durationMs = 500)
    {
        HoldKey(VirtualKey.A, durationMs);
        return this;
    }
}
