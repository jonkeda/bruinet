using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using MauiHeadless.Internals;

namespace MauiHeadless.Hosting;

/// <summary>
/// A popup page (<see cref="MauiTestContextOptions.DialogPages"/>) covers the page under it as a platform draws a
/// popup: over it, without taking it away. A popup library (Mopups) shows its page in an overlay, so the page under
/// it gets no <c>Disappearing</c> when the popup opens and no <c>Appearing</c> when it closes. Headless the popup is a
/// modal page, and MAUI's modal push and pop send both; an app that ends its work on <c>Disappearing</c> (cancels
/// the token the popup's answer is awaited with) would otherwise lose the popup's answer.
/// </summary>
internal static class HeadlessPopups
{
    private static readonly ConditionalWeakTable<Page, object> Covered = new();
    private static readonly object Marker = new();

    /// <summary>Keeps the page under each of <paramref name="window"/>'s popup pages shown, as an overlay does.</summary>
    public static void Attach(Window window, IList<Type> dialogPages)
    {
        window.ModalPushing += (_, pushing) =>
        {
            if (!IsPopup(pushing.Modal, dialogPages) || Top(window) is not { } under || !MauiInternals.HasAppeared(under))
                return;

            // The push's SendDisappearing on the page under finds it not appeared, and does nothing.
            MauiInternals.SetHasAppeared(under, false);
            Covered.AddOrUpdate(under, Marker);
        };
        window.ModalPopping += (_, popping) =>
        {
            if (!IsPopup(popping.Modal, dialogPages) || Below(window, popping.Modal) is not { } under
                || !Covered.TryGetValue(under, out _))
                return;

            // The pop's SendAppearing on the page under finds it appeared, and does nothing.
            Covered.Remove(under);
            MauiInternals.SetHasAppeared(under, true);
        };
    }

    private static bool IsPopup(Page page, IList<Type> dialogPages)
        => dialogPages.Any(type => type.IsInstanceOfType(page));

    private static Page? Top(Window window)
        => window.Navigation.ModalStack.LastOrDefault() ?? window.Page;

    private static Page? Below(Window window, Page modal)
    {
        var stack = window.Navigation.ModalStack;
        var index = stack.ToList().IndexOf(modal);
        return index > 0 ? stack[index - 1] : index == 0 ? window.Page : null;
    }
}
