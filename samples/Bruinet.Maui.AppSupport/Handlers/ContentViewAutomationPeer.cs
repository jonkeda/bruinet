using Microsoft.UI.Xaml;

namespace Bruinet.Maui.AppSupport.Handlers;

/// <summary>
/// AutomationPeer for content containers (ContentView, Border, ContentPage) that exposes the
/// control to UI Automation.
/// </summary>
/// <remarks>
/// Standard MAUI ContentView, Border and ContentPage use <c>ContentPanel</c>, which has no
/// AutomationPeer, so their AutomationId is never visible to UIA tools. This peer
/// surfaces it as a Group, or as a Button when <see cref="BruinetAutomationOptions.InvokeCommand"/>
/// returns a command. The class name is the MAUI type it renders: ContentView, Border or Page.
/// </remarks>
public class ContentViewAutomationPeer : ContainerAutomationPeer
{
    private readonly string _className;

    public ContentViewAutomationPeer(FrameworkElement owner)
        : this(owner, "ContentView")
    {
    }

    public ContentViewAutomationPeer(FrameworkElement owner, string className)
        : base(owner)
    {
        _className = className;
    }

    protected override string GetClassNameCore() => _className;
}
