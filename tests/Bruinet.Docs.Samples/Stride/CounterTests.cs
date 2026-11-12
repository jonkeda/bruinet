// docs/getting-started/quick-start-stride.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:test
using Bruinet.Stride.Testing;
using Xunit;

namespace MyGame.UITests;

// Starts the game with --automation and a pipe of its own, and stops it after the class.
public sealed class GameFixture : StrideTestFixtureBase, IAsyncLifetime
{
    protected override string GetDefaultAppPath()
        => Path.GetFullPath(@"..\..\..\..\MyGame\bin\Debug\net10.0-windows\MyGame.exe");

    async Task IAsyncLifetime.InitializeAsync() => await InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();
}

public sealed class CounterTests(GameFixture game) : IClassFixture<GameFixture>
{
    [Fact]
    public void Increment_CountsUp()
    {
        var page = new MainPage(game.Context);
        page.CounterDisplay.AssertText("Count: 0");

        page.IncrementButton.Click();

        page.CounterDisplay.AssertText("Count: 1");
    }
}
#endregion
