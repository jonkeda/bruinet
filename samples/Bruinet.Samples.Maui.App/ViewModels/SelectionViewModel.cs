using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Bruinet.Samples.Maui.App.ViewModels;

public class SelectionViewModel : ParentViewModel
{
    private ObservableCollection<string> pickerItems = new();
    private string selectedItem = string.Empty;
    private int selectedIndex = -1;
    private string statusMessage = "Ready. Select an item from the picker.";

    public ObservableCollection<string> PickerItems
    {
        get => pickerItems;
        set => SetProperty(ref pickerItems, value);
    }

    public string SelectedItem
    {
        get => selectedItem;
        set
        {
            if (selectedItem != value)
            {
                selectedItem = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (selectedIndex != value)
            {
                selectedIndex = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }
    }

    /// <summary>
    /// Items where two read alike, which MAUI cannot hold a selection on.
    /// </summary>
    /// <remarks>
    /// See the warning beside this picker in SelectionView.xaml: selecting the second of the two
    /// freezes the app, so the bridge refuses it. This list exists so that the refusal is
    /// exercised rather than believed in.
    /// </remarks>
    public ObservableCollection<string> DuplicateItems { get; } = ["Repeat", "Repeat", "Unique"];

    /// <summary>
    /// More items than a dropdown renders at once.
    /// </summary>
    /// <remarks>
    /// So that "what does this picker hold" has a wrong answer available. Reading the items out
    /// of the open popup returns only what the popup has realized, which is the visible handful;
    /// the count and the selected index derived from that list are then wrong for anything
    /// further down.
    /// </remarks>
    public ObservableCollection<string> ManyItems { get; }
        = new(Enumerable.Range(1, 200).Select(n => $"Item {n}"));

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public ICommand ResetCommand => new RelayCommand(Reset);

    public SelectionViewModel()
    {
        InitializePickerItems();

        // A choice to start from, as most pickers in an app have; Reset clears it.
        selectedIndex = 1;
        selectedItem = PickerItems[1];
        UpdateStatus();
    }

    private void InitializePickerItems()
    {
        PickerItems.Add("Option 1");
        PickerItems.Add("Option 2");
        PickerItems.Add("Option 3");
        PickerItems.Add("Option 4");
        PickerItems.Add("Option 5");
    }

    private void UpdateStatus()
    {
        if (SelectedIndex >= 0 && SelectedIndex < PickerItems.Count)
        {
            StatusMessage = $"✓ Selected: {SelectedItem} (index {SelectedIndex})";
        }
        else
        {
            StatusMessage = "Ready. Select an item from the picker.";
        }
    }

    private void Reset()
    {
        SelectedIndex = -1;
        SelectedItem = string.Empty;
        StatusMessage = "Ready. Select an item from the picker.";
    }
}
