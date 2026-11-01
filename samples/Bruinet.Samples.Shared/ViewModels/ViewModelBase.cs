using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bruinet.Samples.Shared.ViewModels;

/// <summary>
/// Base class for all ViewModels providing INotifyPropertyChanged and IViewVisible implementation.
/// Supports busy state tracking and view visibility for command protection.
/// </summary>
public class ParentViewModel : INotifyPropertyChanged, INotifyPropertyChanging, IViewVisible
{
    private bool _viewVisible = true;
    private int _busyCount;
    private readonly object _busyLock = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;

    // ============================================
    // IViewVisible Implementation
    // ============================================

    /// <summary>
    /// Gets or sets whether the view is currently visible.
    /// Commands check this before executing.
    /// </summary>
    public bool ViewVisible
    {
        get => _viewVisible;
        set => SetProperty(ref _viewVisible, value);
    }

    /// <summary>
    /// Gets whether the ViewModel is currently busy.
    /// </summary>
    public bool IsBusy
    {
        get { lock (_busyLock) { return _busyCount > 0; } }
    }

    /// <summary>
    /// Increment busy counter (operation starting).
    /// </summary>
    public void BeginBusy()
    {
        lock (_busyLock)
        { _busyCount++; }
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>
    /// Decrement busy counter (operation completing).
    /// </summary>
    public void EndBusy()
    {
        lock (_busyLock)
        { _busyCount = Math.Max(0, _busyCount - 1); }
        OnPropertyChanged(nameof(IsBusy));
    }

    // ============================================
    // View Lifecycle
    // ============================================

    /// <summary>
    /// Called when the view appears. Sets ViewVisible to true.
    /// </summary>
    public virtual void OnViewAppearing() => ViewVisible = true;

    /// <summary>
    /// Called when the view disappears. Sets ViewVisible to false.
    /// </summary>
    public virtual void OnViewDisappearing() => ViewVisible = false;

    // ============================================
    // INotifyPropertyChanged Implementation
    // ============================================

    /// <summary>
    /// Sets the property value and raises property change notifications.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;

        OnPropertyChanging(propertyName);
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Raises PropertyChanging event.
    /// </summary>
    protected virtual void OnPropertyChanging([CallerMemberName] string? propertyName = null)
    {
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
    }

    /// <summary>
    /// Raises PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
