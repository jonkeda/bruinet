# Quick start: a Stride game

A first UI test of a Stride game, through an automation server the game embeds, in six steps. The code below is
compiled with Bruinet's build (`tests/Bruinet.Docs.Samples`), so it is today's API.

## 1. The feed

Bruinet's packages are on the organisation's GitHub Packages feed, not nuget.org. Add it once, with a token that can
read packages (see [VERSIONING](../../VERSIONING.md)).

## 2. The game's side

The game hosts the automation server, which answers the test over a named pipe:

```bash
dotnet add package Bruinet.Stride.Automation
```

<!-- sample: tests/Bruinet.Docs.Samples/Stride/CounterGame.cs#game -->
```csharp
using Bruinet.Stride.Automation;
using Stride.Engine;
using Stride.UI;
using Stride.UI.Panels;

namespace MyGame;

public sealed class CounterGame : Game
{
    // The game's UI root, built in code or loaded from a UI page asset.
    private UIElement? _ui;

    protected override void BeginRun()
    {
        base.BeginRun();
        _ui = new StackPanel { Name = "MainPanel" };

        // The automation server, only when a test starts the game with --automation.
        this.UseAutomationIfEnabled(() => _ui);
    }
}
```

A UI element is known by its `Name`. A player's game, started without `--automation`, has no server.

## 3. A test project

```bash
dotnet new xunit -n MyGame.UITests
cd MyGame.UITests
```

## 4. The one package

```bash
dotnet add package Bruinet.Stride
```

It brings `Bruinet.Stride.Protocol` and `Bruinet.Core`.

## 5. A page object and a test

A page object names the UI's elements; a test uses only those.

<!-- sample: tests/Bruinet.Docs.Samples/Stride/MainPage.cs#page -->
```csharp
using Bruinet.Core.Locators;
using Bruinet.Stride.Controls.Buttons;
using Bruinet.Stride.Controls.Display;
using Bruinet.Stride.Interfaces;
using Bruinet.Stride.Pages;

namespace MyGame.UITests;

public sealed class MainPage(IStrideTestContext context) : PageObjectBase<MainPage>(context)
{
    public TextBlock<MainPage> CounterDisplay => TextBlock("CounterDisplay");
    public Button<MainPage> IncrementButton => Button("IncrementButton");

    protected override Locator ReadyLocator => Locator.ByAutomationId("MainPanel");
}
```

The fixture starts the game once for the test class and stops it after:

<!-- sample: tests/Bruinet.Docs.Samples/Stride/CounterTests.cs#test -->
```csharp
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
```

## 6. Run it

Build the game, then:

```bash
dotnet test
```

Clicks and keys go to the game's own input, so it needs no foreground, and a locked screen does not matter. Next:
the [Stride guide](../platform-guides/stride.md).
