namespace Bruinet.Samples.Maui.App.Views2.TestViews;

public partial class GesturesView
{
    public GesturesView()
    {
        InitializeComponent();
#if WINDOWS
        if (BindingContext is not ViewModels.GesturesViewModel model)
            throw new InvalidOperationException("The gestures page needs GesturesViewModel.");

        var behavior = new CommunityToolkit.Maui.Behaviors.TouchBehavior();
        behavior.SetBinding(
            CommunityToolkit.Maui.Behaviors.TouchBehavior.CommandProperty,
            new Binding(nameof(model.TapCommand), source: model));
        behavior.SetBinding(
            CommunityToolkit.Maui.Behaviors.TouchBehavior.LongPressCommandProperty,
            new Binding(nameof(model.LongPressCommand), source: model));
        TouchBehaviorTarget.Behaviors.Add(behavior);
#endif
    }
}
