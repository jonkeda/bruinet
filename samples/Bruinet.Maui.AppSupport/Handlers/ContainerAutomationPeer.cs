using Microsoft.Maui;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Bruinet.Maui.AppSupport.Handlers;

/// <summary>
/// A platform panel that knows the MAUI view it renders.
/// </summary>
public interface IAutomationPanel
{
    /// <summary>The MAUI view this panel renders, or <c>null</c> after disconnect.</summary>
    IView? VirtualView { get; }
}

/// <summary>
/// Shared behaviour of the container peers: the enabled state of the MAUI view, and the
/// Invoke pattern for containers that <see cref="BruinetAutomationOptions.InvokeCommand"/> marks
/// as buttons.
/// </summary>
public abstract class ContainerAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
{
    protected ContainerAutomationPeer(FrameworkElement owner)
        : base(owner)
    {
    }

    protected IView? VirtualView => (Owner as IAutomationPanel)?.VirtualView;

    protected override AutomationControlType GetAutomationControlTypeCore()
        => CommandOf(VirtualView) is null ? AutomationControlType.Group : AutomationControlType.Button;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    // A panel is not a Control, so the base always reports enabled.
    protected override bool IsEnabledCore()
        => VirtualView is { } view ? view.IsEnabled : base.IsEnabledCore();

    protected override object? GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Invoke && CommandOf(VirtualView) is not null
            ? this
            : base.GetPatternCore(patternInterface);

    public void Invoke()
    {
        if (VirtualView is not { } view || !CanRun(view, out _))
            return;

        if (!Owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (CanRun(view, out var command))
                command.Command.Execute(command.Parameter);
        }))
            throw new InvalidOperationException("The container's UI dispatcher is unavailable.");
    }

    private static bool CanRun(IView view, out AutomationCommand command)
    {
        command = CommandOf(view)!;
        return view.IsEnabled && command is not null && command.Command.CanExecute(command.Parameter);
    }

    private static AutomationCommand? CommandOf(IView? view)
        => view is not null ? BruinetAutomationOptions.Current.InvokeCommand?.Invoke(view) : null;
}
