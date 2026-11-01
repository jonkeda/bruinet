using System.Windows.Input;

namespace Bruinet.Samples.Maui.App.ViewModels;

/// <summary>
/// ViewModel for the navigation controls demo page.
/// </summary>
/// <remarks>
/// Every command records what fired into <see cref="LastAction"/> and bumps
/// <see cref="ActionCount"/>. Tests assert on those observed values rather than on
/// layout side effects, which keeps them free of fixed delays: a click either changed
/// the label or it did not.
/// </remarks>
public class NavigationDemoViewModel : ParentViewModel
{
    private const string NoAction = "none";
    private const string NoTab = "none";

    private string _lastAction = NoAction;
    private int _actionCount;
    private bool _isMenuOpen;
    private string _selectedTab = NoTab;

    public NavigationDemoViewModel()
    {
        ToolbarCommand = new RelayCommand<string>(RecordAction);
        MenuCommand = new RelayCommand<string>(SelectMenuItem);

        // Records like the others if it ever runs, and cannot run. MAUI drives a MenuItem's
        // enabled state from its command, so this - not IsEnabled in the markup - is what makes
        // the entry genuinely disabled; and because it would record, a test can tell "refused"
        // from "ran and had no visible effect".
        UnavailableMenuCommand = new RelayCommand<string>(SelectMenuItem, _ => false);
        TabCommand = new RelayCommand<string>(SelectTab);
        ToggleMenuCommand = new RelayCommand(() => IsMenuOpen = !IsMenuOpen);
        ResetCommand = new RelayCommand(Reset);
    }

    #region Observed state

    /// <summary>The most recent action, or "none".</summary>
    public string LastAction
    {
        get => _lastAction;
        private set => SetProperty(ref _lastAction, value);
    }

    /// <summary>How many actions have fired since the last reset.</summary>
    public int ActionCount
    {
        get => _actionCount;
        private set => SetProperty(ref _actionCount, value);
    }

    /// <summary>Whether the menu's item list is expanded.</summary>
    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        private set
        {
            if (SetProperty(ref _isMenuOpen, value))
            {
                OnPropertyChanged(nameof(MenuTriggerText));
            }
        }
    }

    /// <summary>The menu trigger's caption, which reflects open state.</summary>
    public string MenuTriggerText => IsMenuOpen ? "Actions (open)" : "Actions";

    /// <summary>The most recently selected tab, or "none".</summary>
    public string SelectedTab
    {
        get => _selectedTab;
        private set => SetProperty(ref _selectedTab, value);
    }

    #endregion

    #region Commands

    /// <summary>Records a toolbar item activation. Parameter is "{Bar}/{Item}".</summary>
    public ICommand ToolbarCommand { get; }

    /// <summary>Records a menu item activation and closes the menu.</summary>
    public ICommand MenuCommand { get; }

    /// <summary>The same, for an entry a user is not allowed to pick.</summary>
    public ICommand UnavailableMenuCommand { get; }

    /// <summary>Records a tab selection.</summary>
    public ICommand TabCommand { get; }

    /// <summary>Expands or collapses the menu's item list.</summary>
    public ICommand ToggleMenuCommand { get; }

    /// <summary>Restores the initial state.</summary>
    public ICommand ResetCommand { get; }

    #endregion

    private void RecordAction(string? action)
    {
        if (string.IsNullOrEmpty(action))
            return;

        LastAction = action;
        ActionCount++;
    }

    private void SelectMenuItem(string? item)
    {
        RecordAction($"Menu/{item}");

        // Selecting an item dismisses the menu, as a real menu would.
        IsMenuOpen = false;
    }

    private void SelectTab(string? tab)
    {
        if (string.IsNullOrEmpty(tab))
            return;

        SelectedTab = tab;
        RecordAction($"Tab/{tab}");
    }

    private void Reset()
    {
        LastAction = NoAction;
        ActionCount = 0;
        IsMenuOpen = false;
        SelectedTab = NoTab;
    }
}
