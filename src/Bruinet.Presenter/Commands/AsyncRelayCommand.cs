using System.Windows.Input;

namespace Bruinet.Presenter.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Func<Task> _execute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raised when a command's work throws. The shell shows it; without a handler the exception goes on, as an
    /// <c>async void</c> one did before (review 05, finding 14).
    /// </summary>
    public static event Action<Exception>? Failed;

    public bool CanExecute(object? parameter)
    {
        return !_isRunning && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            NotifyCanExecuteChanged();
            await _execute().ConfigureAwait(true);
        }
        catch (Exception error) when (Failed is not null)
        {
            // The top of a UI command: nothing awaits it, so an error is shown here or takes the app down.
            Failed(error);
        }
        finally
        {
            _isRunning = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
