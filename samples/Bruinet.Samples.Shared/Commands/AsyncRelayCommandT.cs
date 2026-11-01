using System.ComponentModel;
using System.Diagnostics;
using Bruinet.Samples.Shared.ViewModels;

namespace Bruinet.Samples.Shared.Commands;

/// <summary>
/// Generic async command with parameter support and IViewVisible integration.
/// Platform-agnostic (no WPF CommandManager dependency).
/// </summary>
public class AsyncRelayCommand<T> : IAsyncRelayCommand<T>
{
    private readonly IViewVisible? _viewModel;
    private readonly Func<T?, Task> _execute;
    private readonly Predicate<T?>? _canExecute;
    private readonly AsyncRelayCommandOptions _options;
    private Task? _executionTask;
    private bool _hasExecuted;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Gets the last scheduled task, if available.
    /// </summary>
    public Task? ExecutionTask
    {
        get => _executionTask;
        private set
        {
            if (ReferenceEquals(_executionTask, value))
                return;
            _executionTask = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExecutionTask)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

    /// <summary>
    /// Gets whether the command is currently running.
    /// </summary>
    public bool IsRunning => ExecutionTask is { IsCompleted: false };

    /// <inheritdoc/>
    public bool CanBeCanceled => false;

    /// <inheritdoc/>
    public bool IsCancellationRequested => false;

    /// <summary>
    /// Creates an AsyncRelayCommand without IViewVisible integration.
    /// </summary>
    public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null,
        AsyncRelayCommandOptions options = AsyncRelayCommandOptions.None)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _options = options;
    }

    /// <summary>
    /// Creates an AsyncRelayCommand with IViewVisible integration.
    /// </summary>
    public AsyncRelayCommand(IViewVisible viewModel, Func<T?, Task> execute,
        Predicate<T?>? canExecute = null,
        AsyncRelayCommandOptions options = AsyncRelayCommandOptions.None)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _options = options;
    }

    public bool CanExecute(object? parameter)
    {
        // Handle null for value types
        if (parameter is null && default(T) is not null)
            return false;

        return CanExecute((T?)parameter);
    }

    public void Execute(object? parameter)
    {
        _ = ExecuteAsync((T?)parameter);
    }

    /// <inheritdoc/>
    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc/>
    public bool CanExecute(T? parameter)
    {
        if (IsRunning && !HasOption(AsyncRelayCommandOptions.AllowConcurrentExecutions))
            return false;

        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <inheritdoc/>
    public void Execute(T? parameter)
    {
        _ = ExecuteAsync(parameter);
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(object? parameter) => ExecuteAsync((T?)parameter);

    /// <inheritdoc/>
    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
            return;

        // ViewVisible check
        if (_viewModel != null && !_viewModel.ViewVisible)
            return;

        // OnceOnly check
        if (HasOption(AsyncRelayCommandOptions.OnceOnly) && _hasExecuted)
            return;

        _hasExecuted = true;

        var trackBusy = _viewModel != null && !HasOption(AsyncRelayCommandOptions.SkipBusyTracking);
        if (trackBusy)
            _viewModel!.BeginBusy();

        try
        {
            ExecutionTask = _execute(parameter);

            // After ExecutionTask is set, not before: see AsyncRelayCommand.ExecuteAsync.
            NotifyCanExecuteChanged();

            await ExecutionTask;
        }
        catch (Exception ex)
        {
            if (!HasOption(AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler))
                throw;
            Debug.WriteLine($"[AsyncRelayCommand<T>] Error: {ex.Message}");
        }
        finally
        {
            if (trackBusy)
                _viewModel!.EndBusy();
            NotifyCanExecuteChanged();
        }
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        // Cancellation not supported in this implementation
    }

    /// <summary>
    /// Resets the OnceOnly state, allowing the command to execute again.
    /// </summary>
    public void Reset()
    {
        _hasExecuted = false;
        NotifyCanExecuteChanged();
    }

    private bool HasOption(AsyncRelayCommandOptions option) => (_options & option) != 0;
}
