namespace Bruinet.Samples.Maui.App.Views2.TestViews;

public partial class ContainerView : ContentView
{
    public ContainerView()
    {
        InitializeComponent();
    }

    // The swipe's direction: a finger's on a device; an Open request's when the swipe has no platform view.
    private void OnContractSwipeEnded(object? sender, SwipeEndedEventArgs e)
        => ContractSwipeDirection.Text = e.SwipeDirection.ToString().ToLowerInvariant();

    private void OnContractOpenRequested(object? sender, OpenRequestedEventArgs e)
        => ContractSwipeDirection.Text = e.OpenSwipeItem switch
        {
            OpenSwipeItem.LeftItems => "right",
            OpenSwipeItem.RightItems => "left",
            OpenSwipeItem.TopItems => "down",
            _ => "up",
        };
}
