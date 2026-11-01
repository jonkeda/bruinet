namespace Bruinet.Samples.Shared.ViewModels;

/// <summary>
/// Interface for ViewModels that track view visibility and busy state.
/// Required for TargetAsyncCommand support.
/// </summary>
public interface IViewVisible
{
    /// <summary>
    /// Gets whether the view is currently visible.
    /// Commands check this before executing (protects against clicks during navigation).
    /// </summary>
    bool ViewVisible { get; }

    /// <summary>
    /// Gets whether the ViewModel is currently busy (one or more operations in progress).
    /// UI tests can use this to wait for operations to complete.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Increment busy counter (operation starting).
    /// Thread-safe for concurrent operations.
    /// </summary>
    void BeginBusy();

    /// <summary>
    /// Decrement busy counter (operation completing).
    /// Thread-safe for concurrent operations.
    /// </summary>
    void EndBusy();
}
