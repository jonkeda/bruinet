// docs/getting-started/quick-start-stride.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
#region docs:game
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
#endregion
