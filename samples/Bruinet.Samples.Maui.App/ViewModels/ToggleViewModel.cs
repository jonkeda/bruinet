namespace Bruinet.Samples.Maui.App.ViewModels;

public class ToggleViewModel : ParentViewModel
{
    private bool isCheckBoxChecked;
    private bool isOption1Selected;
    private bool isOption2Selected;
    private bool isOption3Selected;
    private bool isSwitchToggled;
    private string checkBoxStatusMessage = "CheckBox is unchecked";
    private string radioButtonStatusMessage = "No option selected";
    private string switchStatusMessage = "Switch is off";
    private string statusMessage = "Ready. Toggle controls to test.";

    public bool IsCheckBoxChecked
    {
        get => isCheckBoxChecked;
        set
        {
            if (SetProperty(ref isCheckBoxChecked, value))
            {
                UpdateCheckBoxStatus();
                UpdateOverallStatus();
            }
        }
    }

    public bool IsOption1Selected
    {
        get => isOption1Selected;
        set
        {
            if (SetProperty(ref isOption1Selected, value))
            {
                UpdateRadioButtonStatus();
                UpdateOverallStatus();
            }
        }
    }

    public bool IsOption2Selected
    {
        get => isOption2Selected;
        set
        {
            if (SetProperty(ref isOption2Selected, value))
            {
                UpdateRadioButtonStatus();
                UpdateOverallStatus();
            }
        }
    }

    public bool IsOption3Selected
    {
        get => isOption3Selected;
        set
        {
            if (SetProperty(ref isOption3Selected, value))
            {
                UpdateRadioButtonStatus();
                UpdateOverallStatus();
            }
        }
    }

    public bool IsSwitchToggled
    {
        get => isSwitchToggled;
        set
        {
            if (SetProperty(ref isSwitchToggled, value))
            {
                UpdateSwitchStatus();
                UpdateOverallStatus();
            }
        }
    }

    public string CheckBoxStatusMessage
    {
        get => checkBoxStatusMessage;
        set => SetProperty(ref checkBoxStatusMessage, value);
    }

    public string RadioButtonStatusMessage
    {
        get => radioButtonStatusMessage;
        set => SetProperty(ref radioButtonStatusMessage, value);
    }

    public string SwitchStatusMessage
    {
        get => switchStatusMessage;
        set => SetProperty(ref switchStatusMessage, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ICommand ResetCommand => new RelayCommand(Reset);

    private void UpdateCheckBoxStatus()
    {
        CheckBoxStatusMessage = isCheckBoxChecked
            ? "✓ CheckBox is checked"
            : "✗ CheckBox is unchecked";
    }

    private void UpdateRadioButtonStatus()
    {
        if (isOption1Selected)
            RadioButtonStatusMessage = "✓ Option 1 selected";
        else if (isOption2Selected)
            RadioButtonStatusMessage = "✓ Option 2 selected";
        else if (isOption3Selected)
            RadioButtonStatusMessage = "✓ Option 3 selected";
        else
            RadioButtonStatusMessage = "No option selected";
    }

    private void UpdateSwitchStatus()
    {
        SwitchStatusMessage = isSwitchToggled
            ? "✓ Switch is on (notifications enabled)"
            : "✗ Switch is off (notifications disabled)";
    }

    private void UpdateOverallStatus()
    {
        var statusParts = new List<string>();

        if (isCheckBoxChecked)
            statusParts.Add("checkbox enabled");

        if (isOption1Selected)
            statusParts.Add("Option 1 selected");
        else if (isOption2Selected)
            statusParts.Add("Option 2 selected");
        else if (isOption3Selected)
            statusParts.Add("Option 3 selected");

        if (isSwitchToggled)
            statusParts.Add("notifications enabled");

        if (statusParts.Count > 0)
            StatusMessage = $"✓ Active: {string.Join(", ", statusParts)}";
        else
            StatusMessage = "Ready. Toggle controls to test.";
    }

    private void Reset()
    {
        IsCheckBoxChecked = false;
        IsOption1Selected = false;
        IsOption2Selected = false;
        IsOption3Selected = false;
        IsSwitchToggled = false;
        StatusMessage = "Ready. Toggle controls to test.";
    }
}
