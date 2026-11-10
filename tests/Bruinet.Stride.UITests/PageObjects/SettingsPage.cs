namespace Bruinet.Stride.UITests.PageObjects;

/// <summary>
/// Page object for the settings overlay.
/// </summary>
public class SettingsPage : PageObjectBase<SettingsPage>
{
    public override string Name => "Settings Page";
    protected override Locator ReadyLocator => Locator.ByAutomationId("SettingsPanel");

    public SettingsPage(IStrideTestContext context) : base(context) { }

    // Audio Controls
    public Slider<SettingsPage> MasterVolumeSlider => Slider("MasterVolumeSlider");
    public TextBlock<SettingsPage> MasterVolumeDisplay => TextBlock("MasterVolumeDisplay");
    public Slider<SettingsPage> MusicVolumeSlider => Slider("MusicVolumeSlider");
    public TextBlock<SettingsPage> MusicVolumeDisplay => TextBlock("MusicVolumeDisplay");
    public Slider<SettingsPage> SFXVolumeSlider => Slider("SFXVolumeSlider");
    public TextBlock<SettingsPage> SFXVolumeDisplay => TextBlock("SFXVolumeDisplay");
    public ToggleButton<SettingsPage> MuteAudioToggle => ToggleButton("MuteAudioToggle");

    // Graphics Controls
    public ToggleButton<SettingsPage> FullscreenToggle => ToggleButton("FullscreenToggle");
    public ToggleButton<SettingsPage> VSyncToggle => ToggleButton("VSyncToggle");
    public Slider<SettingsPage> BrightnessSlider => Slider("BrightnessSlider");
    public TextBlock<SettingsPage> BrightnessDisplay => TextBlock("BrightnessDisplay");

    // Gameplay Controls
    public EditText<SettingsPage> PlayerNameInput => EditText("PlayerNameInput");
    public Slider<SettingsPage> MoveSpeedSlider => Slider("MoveSpeedSlider");
    public TextBlock<SettingsPage> MoveSpeedDisplay => TextBlock("MoveSpeedDisplay");
    public Slider<SettingsPage> SensitivitySlider => Slider("SensitivitySlider");
    public TextBlock<SettingsPage> SensitivityDisplay => TextBlock("SensitivityDisplay");
    public ToggleButton<SettingsPage> InvertYToggle => ToggleButton("InvertYToggle");
    public ToggleButton<SettingsPage> ShowFpsToggle => ToggleButton("ShowFPSToggle");

    // Buttons
    public Button<SettingsPage> ApplyButton => Button("ApplyButton");
    public Button<SettingsPage> ResetButton => Button("SettingsResetButton");
    public Button<SettingsPage> CloseButton => Button("CloseButton");

    // Actions
    public SettingsPage Close()
    {
        PressKey(VirtualKey.Escape);
        return this;
    }

    public SettingsPage SetMasterVolume(double value)
    {
        MasterVolumeSlider.SetValue(value);
        return this;
    }

    public SettingsPage SetMusicVolume(double value)
    {
        MusicVolumeSlider.SetValue(value);
        return this;
    }

    public SettingsPage SetSFXVolume(double value)
    {
        SFXVolumeSlider.SetValue(value);
        return this;
    }

    public SettingsPage SetBrightness(double value)
    {
        BrightnessSlider.SetValue(value);
        return this;
    }

    public SettingsPage SetPlayerName(string name)
    {
        PlayerNameInput.SetText(name);
        return this;
    }

    public SettingsPage SetMoveSpeed(double value)
    {
        MoveSpeedSlider.SetValue(value);
        return this;
    }

    public SettingsPage SetSensitivity(double value)
    {
        SensitivitySlider.SetValue(value);
        return this;
    }

    public SettingsPage Apply()
    {
        ApplyButton.Click();
        return this;
    }

    public SettingsPage ResetDefaults()
    {
        ResetButton.Click();
        return this;
    }
}
