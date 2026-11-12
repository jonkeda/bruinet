using System.Globalization;
using Android.OS;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using Microsoft.Maui.Handlers;
using AView = Android.Views.View;

namespace Bruinet.Maui.AppSupport.Accessibility;

/// <summary>
/// Publishes a MAUI <c>Slider</c>'s and <c>Stepper</c>'s range through Android accessibility, in
/// the app's own units.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is needed.</b> A <c>Stepper</c> publishes nothing to read its value from: it is a
/// <c>LinearLayout</c> with two buttons, and the value exists only in whatever label the page
/// shows beside it. A <c>Slider</c> is a <c>SeekBar</c> whose range MAUI scales to
/// 0..<c>int.MaxValue</c>, so what it publishes is a raw fraction. TalkBack announces neither
/// usefully, and a UI test cannot read either.
/// </para>
/// <para>
/// <b>What it publishes</b>, on the node Android already has for the control, which is what
/// TalkBack reads:
/// </para>
/// <list type="bullet">
/// <item>range info (<c>RangeInfo</c>) with the MAUI minimum, maximum and value;</item>
/// <item>the value as the state description, so TalkBack says "5" rather than a percentage;</item>
/// <item>
/// the bounds again in the node's extras, under <see cref="MinimumKey"/> and
/// <see cref="MaximumKey"/>. A screen reader ignores extras. UI Automator does not publish range
/// info's bounds, so this is where a test reads them, and their presence is what says the range
/// is in app units rather than raw progress.
/// </item>
/// </list>
/// <para>
/// A <c>Slider</c> also takes <c>ACTION_SET_PROGRESS</c> in its own units, clamped to its bounds,
/// which is what TalkBack and UI Automator send to set a range. Nothing else changes: no content
/// description, no hidden label, no new action on the <c>Stepper</c>.
/// </para>
/// <para>
/// The delegate <b>wraps</b> whatever delegate the view already has (MAUI's semantics delegate
/// among them) and forwards to it, and it is put back on every mapped change in case MAUI
/// replaced it.
/// </para>
/// </remarks>
internal static class RangeAccessibility
{
    /// <summary>The node extra holding the range's minimum, in app units.</summary>
    public const string MinimumKey = "Bruinet.Range.Minimum";

    /// <summary>The node extra holding the range's maximum, in app units.</summary>
    public const string MaximumKey = "Bruinet.Range.Maximum";

    private static bool _registered;

    /// <summary>Adds the mappings to the Slider and Stepper handlers. Idempotent.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        foreach (var key in new[]
                 {
                     nameof(IRange.Value), nameof(IRange.Minimum), nameof(IRange.Maximum), nameof(IView.Semantics)
                 })
        {
            SliderHandler.Mapper.AppendToMapping(key, (handler, slider) =>
                Publish(handler.PlatformView, slider, settable: true));
            StepperHandler.Mapper.AppendToMapping(key, (handler, stepper) =>
                Publish(handler.PlatformView, stepper, settable: false));
        }
    }

    private static void Publish(AView? view, IRange range, bool settable)
    {
        if (view is null)
        {
            return;
        }

        if (ViewCompat.GetAccessibilityDelegate(view) is not RangeNodeDelegate)
        {
            ViewCompat.SetAccessibilityDelegate(
                view,
                new RangeNodeDelegate(ViewCompat.GetAccessibilityDelegate(view), range, settable));
        }

        // Also raises the content-change event, so an accessibility service holding the node
        // reads it again instead of answering from its cache.
        ViewCompat.SetStateDescription(view, Format(range.Value));
    }

    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// Publishes the range on the node and, for a Slider, sets it from <c>ACTION_SET_PROGRESS</c>;
    /// everything else goes to the delegate it wraps.
    /// </summary>
    private sealed class RangeNodeDelegate : ForwardingAccessibilityDelegate
    {
        private readonly IRange _range;
        private readonly bool _settable;

        public RangeNodeDelegate(AccessibilityDelegateCompat? inner, IRange range, bool settable)
            : base(inner)
        {
            _range = range;
            _settable = settable;
        }

        public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfoCompat? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null)
                return;

            info.RangeInfo = AccessibilityNodeInfoCompat.RangeInfoCompat.Obtain(
                AccessibilityNodeInfoCompat.RangeInfoCompat.RangeTypeFloat,
                (float)_range.Minimum,
                (float)_range.Maximum,
                (float)_range.Value);
            info.StateDescription = Format(_range.Value);
            info.Extras?.PutString(MinimumKey, Format(_range.Minimum));
            info.Extras?.PutString(MaximumKey, Format(_range.Maximum));
        }

        public override bool PerformAccessibilityAction(AView? host, int action, Bundle? args)
        {
            if (_settable
                && action == AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress?.Id
                && args is not null
                && args.ContainsKey(AccessibilityNodeInfoCompat.ActionArgumentProgressValue))
            {
                var requested = args.GetFloat(AccessibilityNodeInfoCompat.ActionArgumentProgressValue);
                _range.Value = Math.Clamp(requested, _range.Minimum, _range.Maximum);
                return true;
            }

            return base.PerformAccessibilityAction(host, action, args);
        }
    }
}
