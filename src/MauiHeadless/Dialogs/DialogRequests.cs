using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using MauiHeadless.Inspection;
using MauiHeadless.Internals;

namespace MauiHeadless.Dialogs;

/// <summary>
/// The dialogs a window shows: what its pages asked with <c>DisplayAlertAsync</c>,
/// <c>DisplayPromptAsync</c> and <c>DisplayActionSheetAsync</c>, oldest first. MAUI holds each
/// request on its page until a platform appears; MauiHeadless takes it from there when the tree is read.
/// </summary>
public static class DialogRequests
{
    private static readonly ConditionalWeakTable<Window, List<TestDialog>> Open = new();

    /// <summary>The window's open dialogs, oldest first; the last is on top.</summary>
    public static IReadOnlyList<TestDialog> Of(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Collect(window);
        lock (Open)
            return Open.TryGetValue(window, out var open) ? [.. open] : [];
    }

    /// <summary>The dialog on top of the window, or null when none is open.</summary>
    public static TestDialog? Top(Window window) => Of(window).LastOrDefault();

    internal static void Remove(TestDialog dialog)
    {
        lock (Open)
        {
            if (dialog.Parent is Window window && Open.TryGetValue(window, out var open))
                open.Remove(dialog);
        }
    }

    private static void Collect(Window window)
    {
        foreach (var page in PagesOf(window))
        {
            foreach (var request in MauiInternals.TakeDialogRequests(page))
            {
                var dialog = TestDialog.For(page, request);
                dialog.Parent = window;
                lock (Open)
                    Open.GetValue(window, _ => []).Add(dialog);
            }
        }
    }

    private static IEnumerable<Page> PagesOf(Window window)
    {
        var pages = new HashSet<Page>(ReferenceEqualityComparer.Instance);
        // Every page in the window's stacks, the covered ones too: an alert a page raised before a modal
        // covered it is still the app's open question, and shows when the modal goes.
        if (window.Page is { } root)
        {
            pages.Add(root);
            pages.UnionWith(root.Navigation.NavigationStack.OfType<Page>());
        }

        foreach (var modal in window.Navigation.ModalStack.OfType<Page>())
        {
            pages.Add(modal);
            pages.UnionWith(modal.Navigation.NavigationStack.OfType<Page>());
        }
        if (Screen.CurrentPage(window) is { } current)
        {
            for (Element? walker = current; walker is not null and not Window; walker = walker.Parent)
            {
                if (walker is Page level)
                    pages.Add(level);
            }

            // A Shell section's stack starts with null, for the root it shows itself.
            foreach (var stacked in current.Navigation.NavigationStack.OfType<Page>())
                pages.Add(stacked);
        }

        return pages;
    }
}
