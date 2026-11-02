namespace Bruinet.Samples.Maui.App.Views2.TestViews;

public partial class TextView
{
    public TextView()
    {
        InitializeComponent();
    }

    // As an app's form clears a field for the next entry: empty, and the cursor back in it.
    private void OnClearPlate(object? sender, EventArgs e)
    {
        PlateEntry.Text = string.Empty;
        PlateEntry.Focus();
    }
}
