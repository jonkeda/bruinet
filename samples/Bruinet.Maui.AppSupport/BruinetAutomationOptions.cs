using System.Windows.Input;
using Microsoft.Maui;

namespace Bruinet.Maui.AppSupport;

/// <summary>
/// The command a container peer runs when UI Automation invokes it.
/// </summary>
/// <param name="Command">The command to execute.</param>
/// <param name="Parameter">The parameter passed to <c>CanExecute</c> and <c>Execute</c>.</param>
public sealed record AutomationCommand(ICommand Command, object? Parameter);

/// <summary>
/// Choices for the Windows container peers that <see cref="BruinetAutomationSupport.AddBruinetAutomationHandlers(Microsoft.Maui.Hosting.IMauiHandlersCollection, Action{BruinetAutomationOptions})"/>
/// registers.
/// </summary>
/// <remarks>
/// The defaults match the parameterless overload: every layout, content view, border and page
/// gets a Group peer, and none is invokable.
/// </remarks>
public sealed class BruinetAutomationOptions
{
    internal static BruinetAutomationOptions Current { get; set; } = new();

    /// <summary>
    /// When <c>true</c>, a container without an <c>AutomationId</c> gets no peer, so the UIA tree
    /// holds only the containers a test can address. Its children are still exposed.
    /// </summary>
    public bool PeersRequireAutomationId { get; set; }

    /// <summary>
    /// Returns the command a container runs when invoked, or <c>null</c> when it is not a button.
    /// </summary>
    /// <remarks>
    /// A container that returns a command reports the Button control type and the Invoke pattern.
    /// Invoke runs the command on the UI thread, and only when the view is enabled and
    /// <c>CanExecute</c> returns <c>true</c>.
    /// </remarks>
    public Func<IView, AutomationCommand?>? InvokeCommand { get; set; }
}
