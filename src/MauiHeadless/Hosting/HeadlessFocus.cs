using System.Runtime.CompilerServices;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiHeadless.Inspection;
using MauiHeadless.Internals;

namespace MauiHeadless.Hosting;

/// <summary>
/// The app's own <c>Focus()</c> and <c>Unfocus()</c>, answered as a platform answers them: MAUI asks the
/// handler, and without one the call returns false and nothing is focused (an app that focuses a field after
/// clearing it, measured with MAUI 10.0.110). Each element of a rendered window, and each one added to it
/// later, has its focus request (<c>FocusChangeRequested</c>) answered here: focus leaves whatever had it
/// in the window and lands on the element, or leaves it.
/// </summary>
/// <remarks>
/// <c>FocusChangeRequested</c> is obsolete in MAUI ("handled through VisualElement.MapFocus") and still
/// raised by <c>Focus()</c> when someone listens; if MAUI removes it, this stops compiling rather than
/// silently stopping.
/// </remarks>
internal static class HeadlessFocus
{
    private static readonly ConditionalWeakTable<VisualElement, object> Hooked = new();
    private static readonly object Marker = new();

    /// <summary>Answers the focus requests of <paramref name="window"/>'s elements, now and later.</summary>
    public static void Attach(Window window)
    {
        HookAll(window);
        window.DescendantAdded += (_, added) => HookAll(added.Element);
    }

    private static void HookAll(Element root)
    {
        if (root is VisualElement visual)
            Hook(visual);
        foreach (var below in root.GetVisualTreeDescendants().OfType<VisualElement>())
            Hook(below);
    }

    private static void Hook(VisualElement element)
    {
        lock (Hooked)
        {
            if (Hooked.TryGetValue(element, out _))
                return;
            Hooked.Add(element, Marker);
        }

#pragma warning disable CS0618 // FocusChangeRequested: see the remarks.
        element.FocusChangeRequested += OnFocusRequested;
#pragma warning restore CS0618
    }

    private static void OnFocusRequested(object? sender, VisualElement.FocusRequestArgs request)
    {
        if (sender is not VisualElement element)
            return;

        if (request.Focus)
        {
            if (!element.IsEnabled || !element.IsVisible)
                return;

            var top = element.Window as Element ?? element;
            foreach (var other in VisualTree.Read(top).DescendantsAndSelf().Select(node => node.Element)
                         .OfType<VisualElement>().Where(other => other.IsFocused && !ReferenceEquals(other, element)).ToList())
                MauiInternals.SetFocused(other, false);
            MauiInternals.SetFocused(element, true);
        }
        else
        {
            MauiInternals.SetFocused(element, false);
        }

        request.Result = true;
    }
}
