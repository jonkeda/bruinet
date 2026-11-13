using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;

namespace Bruinet.Samples.Maui.ShellApp;

public partial class AppShell : Shell
{
    /// <summary>The route a detail page pushes, and the one the stack tests pop.</summary>
    public const string DetailSubRoute = "sub";

    public AppShell()
    {
        InitializeComponent();

        // Pushed rather than declared: a route registered here is not part of the tab or
        // flyout structure, which is what makes it a stack entry to pop.
        Routing.RegisterRoute(DetailSubRoute, typeof(Pages.DetailSubPage));

        // The flyout is one property on this object and a chrome button nothing can find: MAUI
        // gives the hamburger no AutomationId, so every route to it from outside was a guess -
        // by name, by control type, or by clicking where it usually is. Declared on the Shell
        // rather than on a page because the flyout belongs to the Shell. IsIdle is here as on the
        // main sample's hub: it asks about the app, so the one element always present answers it.
        AutomationId = "AppShell";
        GestureAutomation.SetVerbs(
            this,
            $"{nameof(BruinetVerb.OpenFlyout)},{nameof(BruinetVerb.CloseFlyout)},"
            + $"{nameof(BruinetVerb.GetState)},{nameof(BruinetVerb.IsIdle)}");

        HandlerChanged += (_, _) => PublishAutomationIdToThePlatformView();
    }

    /// <summary>
    /// Copies the Shell's <c>AutomationId</c> onto the view that draws it, where UI Automation looks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stage G step 32: all thirteen Shell tests failed before their bodies ran</b>, with
    /// <i>"Page 'ShellSamplePage' is not loaded"</i>. The page object scopes every lookup under a
    /// root element, and measured, no element in this app's tree carried that id - nor
    /// <c>AppShell</c>, though the Shell sets it above. MAUI does not copy a Shell's
    /// <c>AutomationId</c> to its platform view, so the tab strip, the flyout and every page were
    /// all findable from the window and none of them from the root the page object asked for.
    /// </para>
    /// <para>
    /// The WinUI view has an automation peer of its own; it only lacks the id. Setting it is the
    /// whole fix, and it does not replace any peer - which is the move that collapses the tree.
    /// </para>
    /// <para>
    /// <b>Android has the same gap</b> and was left out, so every Shell test failed there with
    /// "Page 'AppShell' ... MissingRoot".
    /// UiAutomator2 reports an element's <c>resource-id</c> from its accessibility node, which is
    /// where MAUI puts every other control's id; the Shell's view gets it the same way, through a
    /// delegate that wraps whatever delegate the view already had.
    /// </para>
    /// </remarks>
    private void PublishAutomationIdToThePlatformView()
    {
#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject view)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(view, AutomationId);
        }
#elif ANDROID
        if (Handler?.PlatformView is Android.Views.View view && view.Context?.PackageName is { } package)
        {
            AndroidX.Core.View.ViewCompat.SetAccessibilityDelegate(
                view,
                new ResourceIdDelegate(
                    $"{package}:id/{AutomationId}",
                    AndroidX.Core.View.ViewCompat.GetAccessibilityDelegate(view)));
        }
#endif
    }

#if ANDROID
    /// <summary>Names a view's accessibility node, leaving the rest to the delegate it replaced.</summary>
    /// <remarks>
    /// The Shell's view is a <c>DrawerLayout</c>, which installs a delegate of its own for the
    /// drawer's announcements, so every member is forwarded, not only the one that sets the id.
    /// </remarks>
    private sealed class ResourceIdDelegate(string resourceId, AndroidX.Core.View.AccessibilityDelegateCompat? inner)
        : AndroidX.Core.View.AccessibilityDelegateCompat
    {
        public override void SendAccessibilityEvent(Android.Views.View? host, int eventType)
        {
            if (inner is not null) inner.SendAccessibilityEvent(host, eventType);
            else base.SendAccessibilityEvent(host, eventType);
        }

        public override void SendAccessibilityEventUnchecked(
            Android.Views.View? host, Android.Views.Accessibility.AccessibilityEvent? e)
        {
            if (inner is not null) inner.SendAccessibilityEventUnchecked(host, e);
            else base.SendAccessibilityEventUnchecked(host, e);
        }

        public override bool DispatchPopulateAccessibilityEvent(
            Android.Views.View? host, Android.Views.Accessibility.AccessibilityEvent? e)
            => inner?.DispatchPopulateAccessibilityEvent(host, e) ?? base.DispatchPopulateAccessibilityEvent(host, e);

        public override void OnPopulateAccessibilityEvent(
            Android.Views.View? host, Android.Views.Accessibility.AccessibilityEvent? e)
        {
            if (inner is not null) inner.OnPopulateAccessibilityEvent(host, e);
            else base.OnPopulateAccessibilityEvent(host, e);
        }

        public override void OnInitializeAccessibilityEvent(
            Android.Views.View? host, Android.Views.Accessibility.AccessibilityEvent? e)
        {
            if (inner is not null) inner.OnInitializeAccessibilityEvent(host, e);
            else base.OnInitializeAccessibilityEvent(host, e);
        }

        public override bool OnRequestSendAccessibilityEvent(
            Android.Views.ViewGroup? host, Android.Views.View? child, Android.Views.Accessibility.AccessibilityEvent? e)
            => inner?.OnRequestSendAccessibilityEvent(host, child, e)
               ?? base.OnRequestSendAccessibilityEvent(host, child, e);

        public override AndroidX.Core.View.Accessibility.AccessibilityNodeProviderCompat? GetAccessibilityNodeProvider(
            Android.Views.View? host)
            => inner is not null ? inner.GetAccessibilityNodeProvider(host) : base.GetAccessibilityNodeProvider(host);

        public override bool PerformAccessibilityAction(Android.Views.View? host, int action, Android.OS.Bundle? args)
            => inner?.PerformAccessibilityAction(host, action, args) ?? base.PerformAccessibilityAction(host, action, args);

        public override void OnInitializeAccessibilityNodeInfo(
            Android.Views.View? host, AndroidX.Core.View.Accessibility.AccessibilityNodeInfoCompat? info)
        {
            if (inner is not null)
            {
                inner.OnInitializeAccessibilityNodeInfo(host, info);
            }
            else
            {
                base.OnInitializeAccessibilityNodeInfo(host, info);
            }

            info?.ViewIdResourceName = resourceId;
        }
    }
#endif
}
