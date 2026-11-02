using Microsoft.Maui.Controls;
using MauiHeadless.Assertions;
using MauiHeadless.Interaction;
using MauiHeadless.Samples.App.Pages;
using Xunit;

namespace MauiHeadless.Tests.Framework;

/// <summary>The context's dispatcher: a queue drained at every MauiHeadless call, a virtual clock, handler failures surfaced.</summary>
public sealed class DispatcherTests
{
    [Fact]
    public void A_timer_ticks_only_when_virtual_time_advances()
    {
        using var ctx = new MauiTestContext();
        var page = ctx.Render<CountdownPage>();
        page.Find<Button>("Start").Click();

        ctx.AdvanceTime(TimeSpan.FromMilliseconds(999));
        page.Find<Label>("Left").Should().HaveText("3 s");

        ctx.AdvanceTime(TimeSpan.FromSeconds(1));
        page.Find<Label>("Left").Should().HaveText("2 s");

        ctx.AdvanceTime(TimeSpan.FromMinutes(1));
        page.Find<Label>("Left").Should().HaveText("Go!");
    }

    [Fact]
    public void A_clock_that_follows_the_wall_clock_runs_delayed_work_once_that_much_real_time_has_passed()
    {
        using var ctx = new MauiTestContext(new MauiTestContextOptions { TimeFollowsWallClock = true });
        var ran = false;
        ctx.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () => ran = true);

        ctx.Dispatcher.RunPending();
        Assert.False(ran);

        Assert.True(ctx.Dispatcher.WaitFor(() => ran, TimeSpan.FromSeconds(2)));
        Assert.True(ctx.Dispatcher.Now >= TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public void By_default_real_time_moves_nothing()
    {
        using var ctx = new MauiTestContext();
        var ran = false;
        ctx.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () => ran = true);

        Assert.False(ctx.Dispatcher.WaitFor(() => ran, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(TimeSpan.Zero, ctx.Dispatcher.Now);
    }

    [Fact]
    public void Delayed_work_runs_in_due_order()
    {
        using var ctx = new MauiTestContext();
        var order = new List<string>();
        ctx.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () => order.Add("late"));
        ctx.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(1), () => order.Add("early"));
        ctx.Dispatcher.Dispatch(() => order.Add("now"));

        ctx.AdvanceTime(TimeSpan.FromSeconds(5));

        Assert.Equal(["now", "early", "late"], order);
        Assert.Equal(TimeSpan.FromSeconds(5), ctx.Dispatcher.Now);
    }

    [Fact]
    public void An_async_void_handler_that_throws_fails_the_test()
    {
        using var ctx = new MauiTestContext();
        var button = new Button { Text = "Boom" };
        button.Clicked += async (_, _) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("handler failed");
        };
        var page = ctx.Render(new ContentPage { Content = button });

        var failure = Assert.Throws<InvalidOperationException>(() => page.Find<Button>().Click());

        Assert.Equal("handler failed", failure.Message);
    }

    [Fact]
    public async Task Work_finished_by_a_real_thread_is_awaited_with_WaitForAssertion()
    {
        using var ctx = new MauiTestContext();
        var label = new Label { Text = "waiting" };
        var button = new Button();
        button.Clicked += async (_, _) =>
        {
            await Task.Delay(30);
            label.Text = "done";
        };
        var page = ctx.Render(new ContentPage { Content = new VerticalStackLayout { label, button } });

        page.Find<Button>().Click();
        Assert.Equal("waiting", label.Text);

        page.WaitForAssertion(() => page.Find<Label>().Should().HaveText("done"));
        await Task.CompletedTask;
    }

    [Fact]
    public void Contexts_in_parallel_keep_their_own_state()
    {
        var results = Enumerable.Range(0, 8).AsParallel().Select(i =>
        {
            using var ctx = new MauiTestContext();
            var page = ctx.Render(new GreetingView { Name = $"n{i}" });
            for (var w = 0; w <= i; w++)
                page.Find<Button>("Wave").Click();
            return (page.Find<Label>("Greeting").Text, page.Find<Label>("Waves").Text);
        }).ToList();

        for (var i = 0; i < 8; i++)
            Assert.Contains(($"Hello, n{i}", i == 0 ? "1 wave" : $"{i + 1} waves"), results);
    }
}
