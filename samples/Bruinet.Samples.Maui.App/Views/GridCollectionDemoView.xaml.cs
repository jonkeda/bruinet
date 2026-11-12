namespace Bruinet.Samples.Maui.App.Views2.TestViews;

public partial class GridCollectionDemoView : ContentView
{
    public GridCollectionDemoView()
    {
        InitializeComponent();
#if WINDOWS
        FitShortWindows();
#endif
    }

#if WINDOWS
    /// <summary>
    /// On Windows the window can be shorter than the page: there the product list's star row was squeezed to nothing
    /// by the shortcut list below it, and no row was realized (R2-T111). The rows become Auto, so the list keeps the
    /// height it asks for, and the page scrolls. Not on Android, where a page scroller around the two lists hid
    /// their rows from a lookup and the page fits as it is.
    /// </summary>
    private void FitShortWindows()
    {
        if (Content is not Grid page)
        {
            return;
        }

        foreach (var row in page.RowDefinitions)
        {
            row.Height = GridLength.Auto;
        }

        Content = new ScrollView { AutomationId = "GridCollectionRootScroll", Content = page };
    }
#endif
}
