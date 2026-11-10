using Stride.Engine;
using Stride.UI;
using Bruinet.Core.Configuration;
using Bruinet.Stride;

namespace Bruinet.Stride.Automation;

/// <summary>
/// Extension methods for integrating Bruinet automation into Stride games.
/// </summary>
public static class StrideAutomationExtensions
{
    public static Game UseAutomation(this Game game, Func<UIElement?> uiRootProvider, AutomationServerOptions? options = null)
    {
        var system = new AutomationGameSystem(game.Services, uiRootProvider, options, game);
        game.GameSystems.Add(system);
        return game;
    }

    public static Game UseAutomation(this Game game, IAutomationHandler handler, AutomationServerOptions? options = null)
    {
        var system = new AutomationGameSystem(game.Services, handler, options);
        game.GameSystems.Add(system);
        return game;
    }

    /// <summary>
    /// Whether the game was started for automation: the <c>--automation</c> argument, which the test
    /// fixture passes, or <c>BRUINET_AUTOMATION=1</c>. The game reads no test settings file.
    /// </summary>
    public static bool IsAutomationEnabled()
    {
        return Environment.GetCommandLineArgs().Contains("--automation") ||
               Environment.GetEnvironmentVariable("BRUINET_AUTOMATION") == "1";
    }

    public static Game UseAutomationIfEnabled(this Game game, Func<UIElement?> uiRootProvider, AutomationServerOptions? options = null)
    {
        if (IsAutomationEnabled())
            return game.UseAutomation(uiRootProvider, options);
        return game;
    }

}
