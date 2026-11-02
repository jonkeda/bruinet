using Microsoft.Maui.Controls;
using Xunit;

namespace MauiHeadless.Tests.Framework;

/// <summary>A popup page covers the page under it as an overlay: that page neither disappears nor appears again.</summary>
public sealed class PopupPageTests
{
    [Fact]
    public void The_page_under_a_popup_page_stays_shown()
    {
        var options = new MauiTestContextOptions();
        options.DialogPages.Add(typeof(Popup));
        using var ctx = new MauiTestContext(options);
        var (under, events) = Counted();
        var page = ctx.Render(under, o => o.WithNavigation());
        events.Clear();

        ctx.Dispatcher.Run(() => { _ = page.Instance.Navigation.PushModalAsync(new Popup()); });
        ctx.Dispatcher.Run(() => { _ = page.Instance.Navigation.PopModalAsync(); });

        Assert.Empty(events);
    }

    [Fact]
    public void An_ordinary_modal_page_hides_the_page_under_it()
    {
        using var ctx = new MauiTestContext();
        var (under, events) = Counted();
        var page = ctx.Render(under, o => o.WithNavigation());
        events.Clear();

        ctx.Dispatcher.Run(() => { _ = page.Instance.Navigation.PushModalAsync(new ContentPage()); });
        ctx.Dispatcher.Run(() => { _ = page.Instance.Navigation.PopModalAsync(); });

        Assert.Equal(["Disappearing", "Appearing"], events);
    }

    private static (ContentPage Page, List<string> Events) Counted()
    {
        var events = new List<string>();
        var page = new ContentPage();
        page.Appearing += (_, _) => events.Add("Appearing");
        page.Disappearing += (_, _) => events.Add("Disappearing");
        return (page, events);
    }

    private sealed class Popup : ContentPage;
}
