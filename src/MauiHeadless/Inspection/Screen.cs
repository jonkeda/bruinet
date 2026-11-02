using Microsoft.Maui.Controls;

namespace MauiHeadless.Inspection;

/// <summary>What a window shows: the top modal page, else its page, down through the containers.</summary>
public static class Screen
{
    /// <summary>The page a user is looking at in <paramref name="window"/>.</summary>
    public static Page? CurrentPage(Window window)
        => window.Page is { } page ? Descend(VisiblePage(page, window)) : null;

    /// <summary>
    /// The page a Shell shows: its current page, else the current content's page, which Shell makes
    /// from a <c>ContentTemplate</c> only when the platform asks for it, so MauiHeadless asks.
    /// </summary>
    public static Page? CurrentPage(Shell shell)
        => shell.CurrentPage
           ?? (shell.CurrentItem?.CurrentItem?.CurrentItem is IShellContentController content
               ? content.GetOrCreateContent()
               : null);

    internal static Page VisiblePage(Page page, Window window)
        => window.Navigation.ModalStack.LastOrDefault() ?? page;

    /// <summary>
    /// The pages <paramref name="window"/> draws, top first: each modal, then the page under them, which a
    /// modal covers without taking it away, as on Windows (a page under a modal stays in the tree, found
    /// and ready; measured with the Maui sample's covered page). A page a NavigationPage pushed over is
    /// gone, as there.
    /// </summary>
    public static IReadOnlyList<Page> ShownPages(Window window)
        => window.Page is { } root
            ? [.. window.Navigation.ModalStack.Reverse().Append(root).Select(Descend)]
            : [];

    /// <summary>The pages under the window in the tree, top first: each modal, then the page under them.</summary>
    internal static IEnumerable<Page> Layers(Window window)
        => window.Page is { } root ? window.Navigation.ModalStack.Reverse().Append(root) : [];

    private static Page Descend(Page page) => page switch
    {
        NavigationPage { CurrentPage: { } current } => Descend(current),
        Shell shell when CurrentPage(shell) is { } current => Descend(current),
        TabbedPage { CurrentPage: { } current } => Descend(current),
        _ => page,
    };
}
