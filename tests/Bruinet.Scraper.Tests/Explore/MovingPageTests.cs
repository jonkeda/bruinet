using Bruinet.Scraper.Explore;

namespace Bruinet.Scraper.Tests.Explore;

public sealed class MovingPageTests
{
    [Fact]
    public async Task A_page_that_moved_twice_is_read_the_third_time()
    {
        var reads = 0;
        var pauses = 0;

        var read = await MovingPage.ReadAsync(
            _ => ++reads < 3 ? throw new PageMovedException("moved") : Task.FromResult("the page"),
            _ => { pauses++; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.Equal("the page", read);
        Assert.Equal(3, reads);
        Assert.Equal(2, pauses);
    }

    [Fact]
    public async Task A_page_that_keeps_moving_is_given_up_without_an_error()
    {
        var reads = 0;

        var read = await MovingPage.ReadAsync<string>(
            _ => { reads++; throw new PageMovedException("moved"); },
            _ => Task.CompletedTask,
            CancellationToken.None);

        Assert.Null(read);
        Assert.Equal(MovingPage.Attempts, reads);
    }

    [Fact]
    public async Task Another_error_is_not_swallowed()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => MovingPage.ReadAsync<string>(
            _ => throw new InvalidOperationException("the browser closed"),
            _ => Task.CompletedTask,
            CancellationToken.None));
}
