using System.Globalization;
using Android.OS;
using Android.Views.Accessibility;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using System.Runtime.CompilerServices;
using Bruinet.Maui.AppSupport.Uia;
using Bruinet.Uia;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using AView = Android.Views.View;

namespace Bruinet.Maui.AppSupport.Accessibility;

/// <summary>
/// Publishes what Android's accessibility node leaves out of a Picker, ProgressBar, Entry, RefreshView and
/// ScrollView, as node extras a UI test reads (R2-T116): the same names the Windows bridge answers to
/// <c>GetState</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why.</b> UiAutomator2 sees a Picker as an EditText with its selected text only, a ProgressBar with no value
/// in MAUI's units, an Entry's read-only state not at all (MAUI keeps it focusable), a RefreshView's refreshing
/// state not at all, and a ScrollView with no position. The Android head's <c>ReadState</c> reads these extras.
/// </para>
/// <para>
/// <b>What it publishes</b>, under <see cref="Prefix"/> plus the state's name, each value escaped as a URI
/// component (UiAutomator2 joins extras with semicolons): a Picker's <c>Items</c>, <c>ItemCount</c>,
/// <c>SelectedIndex</c> and <c>SelectedItem</c>; a ProgressBar's <c>Progress</c>; an Entry's
/// <c>IsReadOnly</c>; a RefreshView's <c>IsRefreshing</c>; a ScrollView's <c>ScrollPosition</c> as the six numbers
/// the bridge's verb returns. A screen reader ignores extras.
/// </para>
/// <para>
/// A ScrollView also gets range info, its position from 0 to 100, and takes <c>ACTION_SET_PROGRESS</c> to scroll
/// there: the route a Slider's value takes, so a test can scroll to a position rather than swipe near it.
/// </para>
/// <para>
/// The values are read when the node is built, so they are current; the delegate wraps the view's own and is put
/// back on every mapped change, as <see cref="RangeAccessibility"/>'s is.
/// </para>
/// </remarks>
internal static class StateAccessibility
{
    /// <summary>What every published state's key starts with.</summary>
    public const string Prefix = "Bruinet.State.";

    private static bool _registered;

    /// <summary>Adds the mappings to the handlers. Idempotent.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        var semantics = nameof(IView.Semantics);
        Map(PickerHandler.Mapper, [nameof(IPicker.SelectedIndex), nameof(IPicker.Items), semantics]);
        Map(ProgressBarHandler.Mapper, [nameof(IProgress.Progress), semantics]);
        Map(EntryHandler.Mapper, [nameof(IEntry.IsReadOnly), semantics]);
        Map(RefreshViewHandler.Mapper, [nameof(IRefreshView.IsRefreshing), semantics]);
        Map(ScrollViewHandler.Mapper, [nameof(IScrollView.Content), semantics]);
        Map(CollectionViewHandler.Mapper, [nameof(ItemsView.ItemsSource), semantics]);
        Map(CarouselViewHandler.Mapper, [nameof(ItemsView.ItemsSource), nameof(CarouselView.Position), semantics]);
        Map(IndicatorViewHandler.Mapper, [nameof(IIndicatorView.Count), nameof(IIndicatorView.Position), semantics]);

        // An item template's root (a layout, content view or border in a CollectionView or CarouselView) publishes
        // its place in the list, as Windows' PositionInSet does: Android shows no row number (R2-T122).
        MapItemRoots(LayoutHandler.Mapper, semantics);
        MapItemRoots(ContentViewHandler.Mapper, semantics);
        MapItemRoots(BorderHandler.Mapper, semantics);
    }

    private static void MapItemRoots<TVirtual, THandler>(IPropertyMapper<TVirtual, THandler> mapper, string key)
        where TVirtual : IElement
        where THandler : IElementHandler
        => mapper.AppendToMapping(key, (handler, view) =>
        {
            if (view is not VisualElement element)
            {
                return;
            }

            if (element.Parent is ItemsView)
            {
                Attach(handler.PlatformView as AView, element);
                return;
            }

            // A templated item can be connected before the list adopts it: attach once it does.
            void Adopted(object? sender, EventArgs e)
            {
                if (element.Parent is ItemsView)
                {
                    element.ParentChanged -= Adopted;
                    Attach(element.Handler?.PlatformView as AView, element);
                }
            }

            element.ParentChanged -= Adopted;
            element.ParentChanged += Adopted;
        });

    /// <summary>An item root's index in its list's items, or null when it is not one.</summary>
    private static (int Index, int Count)? ItemPlace(VisualElement element)
    {
        if (element.Parent is not ItemsView { ItemsSource: { } source } || element.BindingContext is not { } item)
        {
            return null;
        }

        var items = source.Cast<object>().ToList();
        var index = items.IndexOf(item);
        return index < 0 ? null : (index, items.Count);
    }

    private static readonly ConditionalWeakTable<VisualElement, IBruinetGestureSink> Sinks = new();

    /// <summary>
    /// Publishes the states <paramref name="sink"/> names (<see cref="IBruinetGestureSink.StateNames"/>) on the
    /// element's node, now and whenever its handler changes. Nothing when the sink names none.
    /// </summary>
    internal static void PublishSink(VisualElement element, IBruinetGestureSink? sink)
    {
        if (sink is null || sink.StateNames.Count == 0 || Sinks.TryGetValue(element, out _))
        {
            return;
        }

        Sinks.Add(element, sink);
        Attach(element.Handler?.PlatformView as AView, element);
        element.HandlerChanged += (_, _) => Attach(element.Handler?.PlatformView as AView, element);
    }

    private static void Map<TVirtual, THandler>(IPropertyMapper<TVirtual, THandler> mapper, string[] keys)
        where TVirtual : IElement
        where THandler : IElementHandler
    {
        foreach (var key in keys)
        {
            mapper.AppendToMapping(key, (handler, view) => Attach(handler.PlatformView as AView, view));
        }
    }

    private static void Attach(AView? view, IElement element)
    {
        if (view is null)
        {
            return;
        }

        if (ViewCompat.GetAccessibilityDelegate(view) is not StateNodeDelegate)
        {
            ViewCompat.SetAccessibilityDelegate(view, new StateNodeDelegate(ViewCompat.GetAccessibilityDelegate(view), element));
        }

        // A service holding the node reads it again instead of answering from its cache.
        if (view.IsAttachedToWindow)
        {
            view.SendAccessibilityEvent(EventTypes.WindowContentChanged);
        }
    }

    /// <summary>The states to publish for an element, read now.</summary>
    private static IEnumerable<(string Name, string Value)> States(IElement element)
    {
        if (element is VisualElement { Parent: ItemsView } row && ItemPlace(row) is { } place)
        {
            yield return ("PositionInSet", Number(place.Index + 1));
            yield return ("SizeOfSet", Number(place.Count));
        }

        if (element is VisualElement visual && Sinks.TryGetValue(visual, out var sink))
        {
            foreach (var name in sink.StateNames)
            {
                string? value;
                try
                {
                    value = sink.Exchange(visual, BruinetVerb.GetState, name);
                }
                catch (NotSupportedException)
                {
                    // Not one of this element's states.
                    value = null;
                }

                if (value is not null)
                {
                    yield return (name, value);
                }
            }
        }

        switch (element)
        {
            case Picker picker:
                yield return ("ItemCount", Number(picker.Items.Count));
                yield return ("Items", string.Join("\n", new[] { Number(picker.Items.Count) }.Concat(picker.Items)));
                yield return ("SelectedIndex", Number(picker.SelectedIndex));
                yield return ("SelectedItem",
                    picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count ? picker.Items[picker.SelectedIndex] : string.Empty);
                break;
            case ProgressBar bar:
                yield return ("Progress", Number(bar.Progress));
                break;
            case Entry entry:
                yield return ("IsReadOnly", entry.IsReadOnly.ToString());
                break;
            case RefreshView refresh:
                yield return ("IsRefreshing", refresh.IsRefreshing.ToString());
                break;
            case CarouselView carousel:
                yield return ("ItemCount", Number(Count(carousel.ItemsSource)));
                yield return ("Position", Number(carousel.Position));
                break;
            case ItemsView items:
                yield return ("ItemCount", Number(Count(items.ItemsSource)));
                break;
            case IndicatorView indicator:
                yield return ("Position", Number(indicator.Position));
                yield return ("Count", Number(indicator.Count));
                break;
            case ScrollView scroll:
                yield return ("ScrollPosition", string.Join(",",
                    Number(scroll.ScrollX), Number(scroll.ScrollY), Number(scroll.Width), Number(scroll.Height),
                    Number(scroll.ContentSize.Width), Number(scroll.ContentSize.Height)));
                break;
        }
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static int Count(System.Collections.IEnumerable? items)
        => items switch
        {
            null => 0,
            System.Collections.ICollection collection => collection.Count,
            _ => items.Cast<object>().Count(),
        };

    /// <summary>How far down a scroll view is, 0 to 100; 0 when it cannot scroll.</summary>
    private static double Percent(ScrollView scroll)
    {
        var range = scroll.ContentSize.Height - scroll.Height;
        return range <= 0 ? 0 : Math.Clamp(scroll.ScrollY / range * 100, 0, 100);
    }

    /// <summary>Writes the element's states into the node's extras; a ScrollView also scrolls on set-progress.</summary>
    private sealed class StateNodeDelegate : ForwardingAccessibilityDelegate
    {
        private readonly IElement _element;

        public StateNodeDelegate(AccessibilityDelegateCompat? inner, IElement element)
            : base(inner) => _element = element;

        public override void OnInitializeAccessibilityNodeInfo(AView? host, AccessibilityNodeInfoCompat? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null)
            {
                return;
            }

            foreach (var (name, value) in States(_element))
            {
                info.Extras?.PutString(Prefix + name, Uri.EscapeDataString(value));
            }

            if (_element is ScrollView scroll)
            {
                info.RangeInfo = AccessibilityNodeInfoCompat.RangeInfoCompat.Obtain(
                    AccessibilityNodeInfoCompat.RangeInfoCompat.RangeTypePercent, 0, 100, (float)Percent(scroll));

                // UiAutomator2 sets a value only on a node that lists set-progress, as a SeekBar does.
                info.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress);
            }
        }

        public override bool PerformAccessibilityAction(AView? host, int action, Bundle? args)
        {
            if (_element is ScrollView scroll
                && action == AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionSetProgress?.Id
                && args is not null
                && args.ContainsKey(AccessibilityNodeInfoCompat.ActionArgumentProgressValue))
            {
                var percent = Math.Clamp(args.GetFloat(AccessibilityNodeInfoCompat.ActionArgumentProgressValue), 0, 100);
                var range = Math.Max(0, scroll.ContentSize.Height - scroll.Height);
                _ = scroll.ScrollToAsync(scroll.ScrollX, range * percent / 100, animated: false);
                return true;
            }

            return base.PerformAccessibilityAction(host, action, args);
        }
    }
}
