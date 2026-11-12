using Android.OS;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using AView = Android.Views.View;

namespace Bruinet.Maui.AppSupport.Accessibility;

/// <summary>
/// An accessibility delegate that hands every call to the one it replaced, or to the view's own
/// behaviour when there was none.
/// </summary>
internal abstract class ForwardingAccessibilityDelegate : AccessibilityDelegateCompat
{
    private readonly AccessibilityDelegateCompat? _inner;

    protected ForwardingAccessibilityDelegate(AccessibilityDelegateCompat? inner) => _inner = inner;

    public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfoCompat? info)
    {
        if (_inner is null)
            base.OnInitializeAccessibilityNodeInfo(host, info);
        else
            _inner.OnInitializeAccessibilityNodeInfo(host, info);
    }

    public override bool PerformAccessibilityAction(AView? host, int action, Bundle? args)
        => _inner?.PerformAccessibilityAction(host, action, args)
           ?? base.PerformAccessibilityAction(host, action, args);

    public override void SendAccessibilityEvent(AView? host, int eventType)
    {
        if (_inner is null)
            base.SendAccessibilityEvent(host, eventType);
        else
            _inner.SendAccessibilityEvent(host, eventType);
    }

    public override bool DispatchPopulateAccessibilityEvent(AView? host, Android.Views.Accessibility.AccessibilityEvent? e)
        => _inner?.DispatchPopulateAccessibilityEvent(host, e)
           ?? base.DispatchPopulateAccessibilityEvent(host, e);

    public override void OnInitializeAccessibilityEvent(AView? host, Android.Views.Accessibility.AccessibilityEvent? e)
    {
        if (_inner is null)
            base.OnInitializeAccessibilityEvent(host, e);
        else
            _inner.OnInitializeAccessibilityEvent(host, e);
    }

    public override void OnPopulateAccessibilityEvent(AView? host, Android.Views.Accessibility.AccessibilityEvent? e)
    {
        if (_inner is null)
            base.OnPopulateAccessibilityEvent(host, e);
        else
            _inner.OnPopulateAccessibilityEvent(host, e);
    }

    public override AccessibilityNodeProviderCompat? GetAccessibilityNodeProvider(AView? host)
        => _inner is null ? base.GetAccessibilityNodeProvider(host) : _inner.GetAccessibilityNodeProvider(host);
}
