using Bruinet.Stride.Infrastructure;

namespace Bruinet.Stride.Tests;

/// <summary>Starting a game: it shows its window, or the start fails by name (review 02, proposal 2).</summary>
public sealed class StrideGameStartTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task A_Game_ThatShowsItsWindow_IsStarted()
    {
        var looks = 0;

        await StrideGameDriver.WaitForWindowAsync("Game.exe", () => null, () => ++looks >= 2, Short, CancellationToken.None);

        Assert.Equal(2, looks);
    }

    [Fact]
    public async Task A_Game_ThatShowsNoWindow_FailsByName_AtStartupTimeout()
    {
        var error = await Assert.ThrowsAsync<TimeoutException>(
            () => StrideGameDriver.WaitForWindowAsync(@"C:\games\Game.exe", () => null, () => false, Short, CancellationToken.None));

        Assert.Contains("Game.exe", error.Message, StringComparison.Ordinal);
        Assert.Contains("StartupTimeout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Game_ThatExits_FailsWithItsCode()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => StrideGameDriver.WaitForWindowAsync("Game.exe", () => 3, () => false, Short, CancellationToken.None));

        Assert.Contains("code 3", error.Message, StringComparison.Ordinal);
    }
}
