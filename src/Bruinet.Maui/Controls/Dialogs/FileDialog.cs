namespace Bruinet.Maui.Controls.Dialogs;

/// <summary>
/// The platform's open-file dialog, shown when the app asks its file picker for a file.
/// </summary>
/// <typeparam name="TScope">The scope to return to after the dialog closes.</typeparam>
/// <remarks>
/// The dialog is native, outside the MAUI tree, so it has no locator. Answering it goes through
/// <see cref="IMauiElement.AnswerFileDialog"/> on the app element: on Windows through UI Automation patterns, with no
/// keyboard or mouse. Platforms without a route throw <see cref="RouteUnavailableException"/>.
/// </remarks>
public sealed class FileDialog<TScope>
    where TScope : IMauiScope<TScope>
{
    private readonly IMauiScope<TScope> _scope;

    /// <summary>Creates the dialog for the scope that opens it.</summary>
    /// <param name="scope">The page or component whose action opens the dialog.</param>
    public FileDialog(IMauiScope<TScope> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scope = scope;
    }

    /// <summary>
    /// Waits for the dialog, chooses <paramref name="path"/> and waits for the dialog to close.
    /// </summary>
    /// <param name="path">The full path of an existing file.</param>
    /// <param name="timeoutMs">How long to wait; the context's default wait when null.</param>
    /// <returns>The scope, for chaining.</returns>
    public TScope Choose(string path, int? timeoutMs = null)
    {
        _scope.Context.AppElement.AnswerFileDialog(
            Path.GetFullPath(path),
            timeoutMs ?? _scope.Context.Timeouts.DefaultWait);
        return _scope.Self;
    }
}
