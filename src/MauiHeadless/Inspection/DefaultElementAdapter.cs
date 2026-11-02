using System.Globalization;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MauiHeadless.Internals;

namespace MauiHeadless.Inspection;

/// <summary>
/// How MauiHeadless reads MAUI's own controls: the page a container shows, a page's toolbar, the rows an
/// items view would show, and each control's text.
/// </summary>
public sealed class DefaultElementAdapter : IElementAdapter
{
    public static readonly DefaultElementAdapter Instance = new();

    public bool CanAdapt(Element element) => true;

    public IEnumerable<Element> GetChildren(Element element)
    {
        switch (element)
        {
            case Window window:
                // An open dialog is over the page, and first in reading order as it is on screen;
                // the title bar's back button comes before the page it leaves.
                var back = ShellChromeNodes.BackButton(window);
                var title = ShellChromeNodes.Title(window);
                return Dialogs.DialogRequests.Of(window).Reverse<Element>()
                    .Concat(back.CanGoBack ? [back] : [])
                    .Concat(title.Text is not null ? [title] : [])
                    .Concat(window.Page is { } root ? [Screen.VisiblePage(root, window)] : []);
            case NavigationBackButton or NavigationTitle:
                return [];
            case NavigationPage navigation:
                return navigation.CurrentPage is { } current ? [current] : [];
            case Shell shell:
                return ShellChildren(shell);
            case ShellTabBar tabBar:
                return tabBar.Tabs;
            case ShellFlyoutList flyoutList:
                return flyoutList.Items;
            case BaseShellItem:
                // A tab or a flyout entry: its pages are reached through the Shell's current page.
                return [];
            case TabbedPage tabbed:
                return tabbed.CurrentPage is { } tab ? [tab] : [];
            case FlyoutPage flyout:
                return flyout.IsPresented ? [flyout.Flyout, flyout.Detail] : [flyout.Detail];
            case ItemsView items:
                return ItemRealizer.Realize(items);
#pragma warning disable CS0618 // ListView is obsolete, but apps still show it.
            case ListView { IsGroupingEnabled: false } list:
                return ListViewCells(list);
#pragma warning restore CS0618
            case Page page:
                return page.ToolbarItems.Cast<Element>().Concat(VisualChildren(page));
            default:
                return VisualChildren(element);
        }
    }

    public string? GetText(Element element) => element switch
    {
        Label { FormattedText: { } formatted } => string.Concat(formatted.Spans.Select(span => span.Text)),
        Label label => label.Text,
        Span span => span.Text,
        Button button => button.Text,
        RadioButton radio => radio.Content as string ?? radio.ContentAsString(),
        // As the field draws it: an app that sets Text in lower case on an upper-case field shows upper case.
        InputView input => TextTransforms.Apply(input.Text, input.TextTransform),
        Picker picker => picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.Items.Count
            ? picker.Items[picker.SelectedIndex]
            : null,
        DatePicker date => date.Date?.ToString(date.Format, CultureInfo.CurrentCulture),
        TimePicker time => time.Time is { } value
            ? DateTime.Today.Add(value).ToString(time.Format, CultureInfo.CurrentCulture)
            : null,
        MenuItem item => item.Text,
        BaseShellItem shellItem => shellItem.Title,
        NavigationBackButton => "Back",
        NavigationTitle title => title.Text,
        Page page => page.Title,
        IText text => text.Text,
        _ => null,
    };

    // What a user sees of a Shell: the flyout while it is open, the tab bar, then the page.
    private static IEnumerable<Element> ShellChildren(Shell shell)
    {
        if (shell.FlyoutIsPresented)
            yield return ShellChromeNodes.Flyout(shell);
        if (ShellChromeNodes.TabBar(shell).Tabs.Any())
            yield return ShellChromeNodes.TabBar(shell);
        if (Screen.CurrentPage(shell) is { } shown)
            yield return shown;
    }

    // A ListView's header, the view of each cell it has made, and its footer. Reading the cells makes
    // them, so the template is wrapped first to strip each cell before it joins the list.
#pragma warning disable CS0618 // ListView is obsolete, but apps still show it.
    private static IEnumerable<Element> ListViewCells(ListView list)
    {
        Hosting.HeadlessBehaviors.PrepareListView(list);
        if (list.Header is View header)
            yield return header;

        foreach (var cell in ((ITemplatedItemsView<Cell>)list).TemplatedItems.OfType<ViewCell>())
        {
            if (cell.View is { } view)
                yield return view;
        }

        if (list.Footer is View footer)
            yield return footer;
    }
#pragma warning restore CS0618

    private static IEnumerable<Element> VisualChildren(Element element)
        => ((IVisualTreeElement)element).GetVisualChildren().OfType<Element>();
}
