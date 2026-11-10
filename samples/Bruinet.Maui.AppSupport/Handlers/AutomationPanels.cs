using Microsoft.Maui;
using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Bruinet.Maui.AppSupport.Handlers;

/// <summary>
/// <c>LayoutPanel</c> subclass that supplies an AutomationPeer, so layout
/// AutomationId values appear in the Windows UIA tree.
/// </summary>
public class AutomationLayoutPanel : LayoutPanel, IAutomationPanel
{
    public IView? VirtualView { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer()
        => AutomationPanels.HasPeer(this) ? new LayoutAutomationPeer(this) : null!;
}

/// <summary>
/// <c>ContentPanel</c> subclass that supplies an AutomationPeer, so ContentView,
/// Border and ContentPage AutomationId values appear in the Windows UIA tree.
/// </summary>
public class AutomationContentPanel : ContentPanel, IAutomationPanel
{
    private readonly string _className;

    public AutomationContentPanel()
        : this("ContentView")
    {
    }

    public AutomationContentPanel(string className)
    {
        _className = className;
    }

    public IView? VirtualView { get; set; }

    protected override AutomationPeer OnCreateAutomationPeer()
        => AutomationPanels.HasPeer(this) ? new ContentViewAutomationPeer(this, _className) : null!;
}

internal static class AutomationPanels
{
    // A null peer leaves the panel out of the tree; its children are still exposed.
    public static bool HasPeer(Microsoft.UI.Xaml.FrameworkElement panel)
        => !BruinetAutomationOptions.Current.PeersRequireAutomationId
            || !string.IsNullOrEmpty(Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(panel));
}
