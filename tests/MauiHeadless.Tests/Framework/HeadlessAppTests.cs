using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using MauiHeadless.Hosting;
using MauiHeadless.Interaction;
using Xunit;

namespace MauiHeadless.Tests.Framework;

/// <summary>What a real app needs on a plain net10.0 head: taps routed as a touch is, list rows, platform behaviors and Essentials.</summary>
public sealed class HeadlessAppTests
{
    [Fact]
    public void A_tap_reaches_the_content_a_view_wraps()
    {
        using var ctx = new MauiTestContext();
        var taps = 0;
        var inner = new Grid();
        inner.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => taps++) });
        var page = ctx.Render(new ContentPage { Content = new ContentView { AutomationId = "Button", Content = inner } });

        page.Find<ContentView>("Button").Tap();

        Assert.Equal(1, taps);
    }

    [Fact]
    public void A_tap_bubbles_to_the_nearest_parent_that_takes_it()
    {
        using var ctx = new MauiTestContext();
        var taps = 0;
        var card = new VerticalStackLayout { new Label { AutomationId = "Caption", Text = "Open" } };
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => taps++) });
        var page = ctx.Render(new ContentPage { Content = card });

        page.Find<Label>("Caption").Tap();

        Assert.Equal(1, taps);
    }

    [Fact]
    public void A_tap_nothing_takes_is_refused()
    {
        using var ctx = new MauiTestContext();
        var page = ctx.Render(new ContentPage { Content = new Label { AutomationId = "Plain" } });

        Assert.Throws<ElementNotInteractableException>(() => page.Find<Label>("Plain").Tap());
    }

    [Fact]
    public void A_tap_inside_a_row_selects_the_row()
    {
        using var ctx = new MauiTestContext();
        var list = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsSource = new[] { "One", "Two" },
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label();
                label.SetBinding(Label.TextProperty, ".");
                label.SetBinding(Element.AutomationIdProperty, ".");
                return new Border { Content = label };
            }),
        };
        var page = ctx.Render(new ContentPage { Content = list });

        page.Find<Label>("Two").Tap();

        Assert.Equal("Two", list.SelectedItem);
    }

    [Fact]
    public void A_grouped_list_shows_each_group_header_before_its_rows()
    {
        using var ctx = new MauiTestContext();
        var list = new CollectionView
        {
            IsGrouped = true,
            ItemsSource = new[] { new Group("Fruit", ["Apple", "Pear"]), new Group("Veg", ["Leek"]) },
            GroupHeaderTemplate = new DataTemplate(() => Bound(nameof(Group.Name))),
            ItemTemplate = new DataTemplate(() => Bound(".")),
        };
        var page = ctx.Render(new ContentPage { Content = list });

        var texts = page.FindAll<Label>().Select(label => label.Text).ToList();

        Assert.Equal(["Fruit", "Apple", "Pear", "Veg", "Leek"], texts);
    }

    [Fact]
    public void Select_finds_an_item_inside_a_group_of_a_grouped_list()
    {
        using var ctx = new MauiTestContext();
        var list = new CollectionView
        {
            IsGrouped = true,
            SelectionMode = SelectionMode.Single,
            ItemsSource = new[] { new Group("Fruit", ["Apple", "Pear"]), new Group("Veg", ["Leek"]) },
            GroupHeaderTemplate = new DataTemplate(() => Bound(nameof(Group.Name))),
            ItemTemplate = new DataTemplate(() => Bound(".")),
        };
        ctx.Render(new ContentPage { Content = list });

        list.Select("Leek");

        Assert.Equal("Leek", list.SelectedItem);
        var refused = Assert.Throws<ElementNotInteractableException>(() => list.Select("Kale"));
        Assert.Contains("none of the groups", refused.Message, StringComparison.Ordinal);
    }

#pragma warning disable CS0618 // ListView is obsolete, but apps still show it.
    [Fact]
    public void A_ListView_cell_tap_raises_ItemTapped()
    {
        using var ctx = new MauiTestContext();
        object? tapped = null;
        var list = new ListView
        {
            ItemsSource = new[] { "One", "Two" },
            ItemTemplate = new DataTemplate(() => new ViewCell { View = Bound(".", automationId: true) }),
        };
        list.ItemTapped += (_, e) => tapped = e.Item;
        var page = ctx.Render(new ContentPage { Content = list });

        page.Find<Label>("Two").Tap();

        Assert.Equal("Two", tapped);
    }
#pragma warning restore CS0618

    [Fact]
    public void A_platform_behavior_is_removed_and_its_command_becomes_a_tap()
    {
        using var ctx = new MauiTestContext();
        var taps = 0;
        var target = new Grid { AutomationId = "Touch" };
        target.Behaviors.Add(new FakeTouchBehavior { Command = new Command(() => taps++) });
        var page = ctx.Render(new ContentPage { Content = target });

        page.Find<Grid>("Touch").Tap();

        Assert.Empty(target.Behaviors);
        Assert.Equal(1, taps);
    }

    [Fact]
    public void Essentials_answer_from_the_options()
    {
        var folder = Path.Combine(Path.GetTempPath(), "MauiHeadless.Tests", nameof(Essentials_answer_from_the_options));
        HeadlessEssentials.Install(new HeadlessEssentialsOptions { AppDataDirectory = folder, AppName = "Sample" });

        Preferences.Default.Set("key", "value");

        Assert.Equal(folder, FileSystem.AppDataDirectory);
        Assert.Equal("Sample", AppInfo.Name);
        Assert.Equal("value", Preferences.Default.Get("key", ""));
    }

    [Fact]
    public async Task The_camera_can_capture_as_on_Windows_and_a_capture_is_cancelled()
    {
        HeadlessEssentials.Install();

        Assert.True(Microsoft.Maui.Media.MediaPicker.Default.IsCaptureSupported);
        Assert.Null(await Microsoft.Maui.Media.MediaPicker.Default.CapturePhotoAsync());

        HeadlessEssentials.Install(new HeadlessEssentialsOptions { CaptureSupported = false });
        Assert.False(Microsoft.Maui.Media.MediaPicker.Default.IsCaptureSupported);
        HeadlessEssentials.Install();
    }

    private static Label Bound(string path, bool automationId = false)
    {
        var label = new Label();
        label.SetBinding(Label.TextProperty, path);
        if (automationId)
            label.SetBinding(Element.AutomationIdProperty, path);
        return label;
    }

    private sealed class Group(string name, IEnumerable<string> items) : List<string>(items)
    {
        public string Name { get; } = name;
    }

    // Stands for a toolkit TouchBehavior: a platform behavior that takes a command.
    private sealed class FakeTouchBehavior : PlatformBehavior<View, object>
    {
        public ICommand? Command { get; set; }
    }
}
