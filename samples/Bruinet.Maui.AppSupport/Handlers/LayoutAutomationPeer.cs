using Microsoft.UI.Xaml;

namespace Bruinet.Maui.AppSupport.Handlers;

/// <summary>
/// AutomationPeer for layout containers (Grid, StackLayout, FlexLayout, …) that
/// exposes the control to UI Automation.
/// </summary>
/// <remarks>
/// Standard MAUI layouts use <c>LayoutPanel</c>, which derives from WinUI
/// <c>Panel</c>. Panel has no AutomationPeer, so a layout's AutomationId is invisible
/// to UIA tools (FlaUI, WinAppDriver, Appium). This peer surfaces it as a Group, or as a
/// Button when <see cref="BruinetAutomationOptions.InvokeCommand"/> returns a command.
/// </remarks>
public class LayoutAutomationPeer : ContainerAutomationPeer
{
    public LayoutAutomationPeer(FrameworkElement owner)
        : base(owner)
    {
    }

    protected override string GetClassNameCore() => "Layout";
}
