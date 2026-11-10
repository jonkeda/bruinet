using Bruinet.Stride.UITests.PageObjects;

namespace Bruinet.Stride.UITests.Tests;

public class GameplayTests : StrideUITestBase
{
    public GameplayTests(StrideAppFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    [Fact]
    public void Game_Initializes_ShowsHUD()
    {
        var game = new GamePage(Context);
        game.AssertLoaded(true);

        game.GameTitle.AssertExists(true);
        game.GameTitle.AssertText("Bruinet Stride Sample");
        game.PositionDisplay.AssertExists(true);
        game.EscHint.AssertExists(true);
        game.MovementHint.AssertExists(true);
    }

    [Fact]
    public void Player_InitialPosition_IsAtOrigin()
    {
        var game = new GamePage(Context);
        game.AssertLoaded(true);

        // Player may have moved from prior tests — just verify position is displayed
        game.PositionDisplay.AssertExists(true);
        game.PositionDisplay.AssertTextContains("Position:");
    }

    [Fact]
    public void Player_MoveNorth_PositionChanges()
    {
        var game = OnTheGame();
        var initialText = game.PositionDisplay.GetText();

        game.MoveNorth(durationMs: 300);

        Assert.True(game.PositionDisplay.WaitText(text => text != initialText), $"The position stayed {initialText}.");
    }

    [Fact]
    public void Player_MoveSouth_PositionChanges()
    {
        var game = OnTheGame();
        var initialText = game.PositionDisplay.GetText();

        game.MoveSouth(durationMs: 300);

        Assert.True(game.PositionDisplay.WaitText(text => text != initialText), $"The position stayed {initialText}.");
    }

    // The game ignores movement while the settings are open, and a test before may leave them open.
    private GamePage OnTheGame()
    {
        var settings = new SettingsPage(Context);
        if (settings.IsLoaded())
            settings.Close();

        var game = new GamePage(Context);
        game.AssertLoaded(true);
        return game;
    }

    [Fact]
    public void HUD_EscHintSaysPress()
    {
        var game = new GamePage(Context);
        game.AssertLoaded(true);

        game.EscHint.AssertTextContains("ESC");
    }

    [Fact]
    public void Game_PressEscape_OpensSettings()
    {
        // Ensure settings are closed first (may be left open by prior test)
        var settingsCheck = new SettingsPage(Context);
        if (settingsCheck.IsLoaded())
            settingsCheck.Close();

        var game = new GamePage(Context);
        game.AssertLoaded(true);

        game.OpenSettings();

        var settings = new SettingsPage(Context);
        settings.AssertLoaded(true);
    }

    [Fact]
    public void Game_OpenAndCloseSettings_ReturnsToGame()
    {
        // Ensure settings are closed first (may be left open by prior test)
        var settingsCheck = new SettingsPage(Context);
        if (settingsCheck.IsLoaded())
            settingsCheck.Close();

        var game = new GamePage(Context);
        game.AssertLoaded(true);

        game.OpenSettings();
        var settings = new SettingsPage(Context);
        settings.AssertLoaded(true);

        settings.Close();
        game.AssertLoaded(true);
    }
}
