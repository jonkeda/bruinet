using Microsoft.Maui.Controls;
using MauiHeadless.Assertions;
using MauiHeadless.Inspection;
using MauiHeadless.Interaction;
using MauiHeadless.Samples.App;
using Xunit;

namespace MauiHeadless.Tests.Framework;

/// <summary>What the platform draws and a user still touches: Shell's tabs and flyout, swipes, and refusals with their reasons.</summary>
public sealed class ChromeAndSwipeTests
{
    [Fact]
    public void A_Shell_shows_its_tab_bar_and_a_tab_navigates()
    {
        using var ctx = new MauiTestContext();
        var app = ctx.Render<AppShell>();

        var tabs = app.Find<ShellTabBar>().FindAll<ShellSection>();
        Assert.Equal(["Home", "About"], tabs.Select(tab => tab.Title));

        tabs[1].Select();

        app.Navigation.ShouldBeOn<AboutPage>();
        app.Find<Label>("Version").Should().HaveText("Version 1.0");
    }

    [Fact]
    public void The_flyout_is_in_the_tree_only_while_it_is_open()
    {
        using var ctx = new MauiTestContext();
        var app = ctx.Render<AppShell>();
        Assert.Empty(app.FindAll<ShellFlyoutList>());

        ctx.Dispatcher.Run(() => app.Instance.FlyoutIsPresented = true);

        Assert.Single(app.FindAll<ShellFlyoutList>());
    }

    [Fact]
    public void The_back_button_shows_while_there_is_a_page_to_go_back_to_and_pops_it()
    {
        using var ctx = new MauiTestContext();
        var second = new ContentPage { AutomationId = "Second", Content = new Label { Text = "second" } };
        var first = new Button { AutomationId = "Next", Text = "Next" };
        var root = new ContentPage { AutomationId = "First", Content = first };
        first.Clicked += async (_, _) => await root.Navigation.PushAsync(second);
        var app = ctx.Render(root, o => o.WithNavigation());
        Assert.Empty(app.FindAll<NavigationBackButton>());

        app.Find<Button>("Next").Click();
        app.Find<NavigationBackButton>().Should().HaveText("Back");
        app.Find<NavigationBackButton>().Click();

        Assert.Same(root, app.Navigation.ShouldBeOn<ContentPage>());
        Assert.Empty(app.FindAll<NavigationBackButton>());
    }

    [Fact]
    public void A_carousel_swipes_one_card_and_refuses_past_the_end()
    {
        using var ctx = new MauiTestContext();
        var carousel = new CarouselView { ItemsSource = new[] { "a", "b" }, Loop = false };
        var page = ctx.Render(new ContentPage { Content = carousel });

        page.Find<CarouselView>().Swipe(SwipeDirection.Left);
        Assert.Equal(1, carousel.Position);

        var refused = Assert.Throws<ElementNotInteractableException>(() => page.Find<CarouselView>().Swipe(SwipeDirection.Left));
        Assert.Equal(InteractionRefusal.NotOffered, refused.Reason);
    }

    [Fact]
    public void Only_the_current_card_of_a_carousel_is_visible()
    {
        using var ctx = new MauiTestContext();
        var carousel = new CarouselView { ItemsSource = new[] { "a", "b", "c" }, Loop = false };
        var page = ctx.Render(new ContentPage { Content = carousel });

        page.FindByText<Label>("a").Should().BeVisible();
        page.FindByText<Label>("b").Should().NotBeVisible();

        page.Find<CarouselView>().Swipe(SwipeDirection.Left);

        page.FindByText<Label>("a").Should().NotBeVisible();
        page.FindByText<Label>("b").Should().BeVisible();
    }

    [Fact]
    public void Focus_moves_from_one_field_to_the_next()
    {
        using var ctx = new MauiTestContext();
        var events = new List<string>();
        var first = new Entry { AutomationId = "First" };
        var second = new Entry { AutomationId = "Second" };
        first.Focused += (_, _) => events.Add("first focused");
        first.Unfocused += (_, _) => events.Add("first unfocused");
        second.Focused += (_, _) => events.Add("second focused");
        var page = ctx.Render(new ContentPage { Content = new VerticalStackLayout { first, second } });

        page.Find<Entry>("First").SetFocus();
        page.Find<Entry>("Second").SetFocus();

        Assert.Equal(["first focused", "first unfocused", "second focused"], events);
        Assert.True(second.IsFocused);
        Assert.False(first.IsFocused);
    }

    [Fact]
    public void A_view_that_keeps_a_RefreshView_deep_inside_is_pulled_through_it()
    {
        using var ctx = new MauiTestContext();
        var pulls = 0;
        var refresh = new RefreshView { Content = new CollectionView(), Command = new Command(() => pulls++) };
        var list = new ContentView { AutomationId = "List", Content = new Grid { new Border { Content = refresh } } };
        var page = ctx.Render(new ContentPage { Content = list });

        page.Find<ContentView>("List").Swipe(SwipeDirection.Down);

        Assert.Equal(1, pulls);
    }

    [Fact]
    public void A_view_with_two_RefreshViews_inside_is_not_pulled_by_guess()
    {
        using var ctx = new MauiTestContext();
        var both = new VerticalStackLayout { new RefreshView { AutomationId = "First" }, new RefreshView { AutomationId = "Second" } };
        var page = ctx.Render(new ContentPage { Content = new ContentView { AutomationId = "Two", Content = both } });

        var refused = Assert.Throws<ElementNotInteractableException>(() => page.Find<ContentView>("Two").Swipe(SwipeDirection.Down));

        Assert.Contains("2 RefreshViews", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_swipe_recognizer_runs_for_its_direction_only()
    {
        using var ctx = new MauiTestContext();
        var swipes = new List<SwipeDirection>();
        var label = new Label { Text = "swipe me" };
        var recognizer = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        recognizer.Swiped += (_, e) => swipes.Add(e.Direction);
        label.GestureRecognizers.Add(recognizer);
        var page = ctx.Render(new ContentPage { Content = label });

        page.Find<Label>().Swipe(SwipeDirection.Right);

        Assert.Equal([SwipeDirection.Right], swipes);
        Assert.Throws<ElementNotInteractableException>(() => page.Find<Label>().Swipe(SwipeDirection.Left));
    }

    [Fact]
    public void A_picker_refuses_an_item_equal_to_an_earlier_one()
    {
        using var ctx = new MauiTestContext();
        var picker = new Picker { ItemsSource = new[] { "same", "same", "other" } };
        var page = ctx.Render(new ContentPage { Content = picker });

        var refused = Assert.Throws<ElementNotInteractableException>(() => page.Find<Picker>().SelectIndex(1));

        Assert.Contains("an earlier item is equal to this one", refused.Message);
        page.Find<Picker>().SelectIndex(2).Should().HaveText("other");
    }

    [Fact]
    public void A_refusal_says_why()
    {
        using var ctx = new MauiTestContext();
        var page = ctx.Render(new ContentPage
        {
            Content = new VerticalStackLayout
            {
                new Button { AutomationId = "Off", IsEnabled = false },
                new Button { AutomationId = "Hidden", IsVisible = false },
                new Entry { AutomationId = "Locked", IsReadOnly = true },
            },
        });

        Assert.Equal(InteractionRefusal.Disabled, Assert.Throws<ElementNotInteractableException>(() => page.Find<Button>("Off").Click()).Reason);
        Assert.Equal(InteractionRefusal.Hidden, Assert.Throws<ElementNotInteractableException>(() => page.Find<Button>("Hidden").Click()).Reason);
        Assert.Equal(InteractionRefusal.ReadOnly, Assert.Throws<ElementNotInteractableException>(() => page.Find<Entry>("Locked").SetText("x")).Reason);
    }
}
