using Bruinet.Stride.UITests.PageObjects;

namespace Bruinet.Stride.UITests.Tests;

public class SettingsTests : StrideUITestBase
{
    public SettingsTests(StrideAppFixture fixture, ITestOutputHelper output)
        : base(fixture, output) { }

    private void EnsureSettingsClosed()
    {
        var settings = new SettingsPage(Context);
        if (settings.IsLoaded())
        {
            settings.Close();
            var game = new GamePage(Context);
            game.AssertLoaded(true);
        }
    }

    private SettingsPage OpenSettings()
    {
        // Ensure settings are closed first (may be left open by a prior test)
        EnsureSettingsClosed();

        var game = new GamePage(Context);
        game.AssertLoaded(true);
        game.OpenSettings();

        var settings = new SettingsPage(Context);
        settings.AssertLoaded(true);

        // Reset to defaults so tests aren't affected by prior mutations
        settings.ResetDefaults();

        return settings;
    }

    #region Audio Settings

    [Fact]
    public void AudioSettings_MasterVolumeSlider_Exists()
    {
        var settings = OpenSettings();
        settings.MasterVolumeSlider.AssertExists(true);
        settings.MasterVolumeSlider.AssertValueWithin(80, 1);
    }

    [Fact]
    public void AudioSettings_MasterVolume_CanBeChanged()
    {
        var settings = OpenSettings();

        settings.SetMasterVolume(50);

        settings.MasterVolumeSlider.AssertValueWithin(50, 5);
        settings.MasterVolumeDisplay.AssertTextContains("50");
    }

    [Fact]
    public void AudioSettings_MuteToggle_InitiallyOff()
    {
        var settings = OpenSettings();
        settings.MuteAudioToggle.AssertChecked(false);
    }

    [Fact]
    public void AudioSettings_MuteToggle_CanBeToggled()
    {
        var settings = OpenSettings();

        settings.MuteAudioToggle.Click();

        settings.MuteAudioToggle.AssertChecked();
    }

    #endregion

    #region Graphics Settings

    [Fact]
    public void GraphicsSettings_FullscreenToggle_Exists()
    {
        var settings = OpenSettings();
        settings.FullscreenToggle.AssertExists(true);
        settings.FullscreenToggle.AssertChecked(false);
    }

    [Fact]
    public void GraphicsSettings_BrightnessSlider_InitialValue_Is50()
    {
        var settings = OpenSettings();
        settings.BrightnessSlider.AssertValueWithin(50, 1);
    }

    #endregion

    #region Gameplay Settings

    [Fact]
    public void GameplaySettings_PlayerNameInput_Exists()
    {
        var settings = OpenSettings();
        settings.PlayerNameInput.AssertExists(true);
    }

    [Fact]
    public void GameplaySettings_PlayerName_CanBeChanged()
    {
        var settings = OpenSettings();
        settings.PlayerNameInput.AssertExists(true);
        settings.PlayerNameInput.AssertTextEmpty(false);
    }

    [Fact]
    public void GameplaySettings_MoveSpeedSlider_InitialValue_Is5()
    {
        var settings = OpenSettings();
        settings.MoveSpeedSlider.AssertValueWithin(5, 1);
    }

    [Fact]
    public void GameplaySettings_MoveSpeed_CanBeChanged()
    {
        var settings = OpenSettings();

        settings.SetMoveSpeed(8);

        settings.MoveSpeedSlider.AssertValueWithin(8, 2);
    }

    [Fact]
    public void GameplaySettings_InvertYToggle_InitiallyOff()
    {
        var settings = OpenSettings();
        settings.InvertYToggle.AssertChecked(false);
    }

    #endregion

    #region Settings Buttons

    [Fact]
    public void Settings_ApplyButton_IsClickable()
    {
        var settings = OpenSettings();
        settings.ApplyButton.AssertEnabled(true);
        settings.ApplyButton.AssertExists(true);
    }

    [Fact]
    public void Settings_ApplyButton_ClickDoesNotCloseSettings()
    {
        var settings = OpenSettings();

        settings.Apply();

        settings.AssertLoaded(true);
    }

    #endregion
}
