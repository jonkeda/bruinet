using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiHeadless.Internals;

namespace MauiHeadless.Hosting;

/// <summary>
/// A <see cref="NavigationPage"/>'s push and pop finished as a platform finishes them. Without a handler MAUI
/// changes the stack and stops there: no <c>Appearing</c> for the page shown again, no <c>Pushed</c>,
/// <c>Popped</c> or <c>NavigatedTo</c> (MAUI 10.0.110, <c>SendHandlerUpdateAsync</c>), so an app that ends a
/// page's work on <c>Popped</c> never ends it. Each navigation page of a rendered window, and each one added to it
/// later, gets a <see cref="HeadlessHandler"/>, which reports each navigation finished at once.
/// </summary>
internal static class HeadlessNavigation
{
    /// <summary>Gives <paramref name="window"/>'s navigation pages a handler, now and later.</summary>
    public static void Attach(Window window, IMauiContext mauiContext)
    {
        HookAll(window, mauiContext);
        window.DescendantAdded += (_, added) => HookAll(added.Element, mauiContext);
    }

    private static void HookAll(Element root, IMauiContext mauiContext)
    {
        if (root is NavigationPage { Handler: null } navigation)
            Hook(navigation, mauiContext);
        foreach (var below in root.GetVisualTreeDescendants().OfType<NavigationPage>().Where(page => page.Handler is null))
            Hook(below, mauiContext);
    }

    private static void Hook(NavigationPage navigation, IMauiContext mauiContext)
    {
        navigation.Handler = new HeadlessHandler(mauiContext);
        // Its alerts stay with the page, where the dialogs are read, as every other page's do.
        MauiInternals.ClearPlatformEnabled(navigation);
    }
}
