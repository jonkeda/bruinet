namespace Bruinet.Stride.Interfaces;

// Core's ITestContext (its name and near-miss settings among them) and Stride's own scope, which
// derives from Core's IScope. See AD-010.

/// <summary>
/// One Stride game under test: the driver, the timeouts and log every call uses, and the game's UI
/// as the outermost scope.
/// </summary>
public interface IStrideTestContext : ITestContext, IStrideElementScope
{
    /// <summary>The automation pipe to the game.</summary>
    IStrideDriver Driver { get; }

    /// <summary>Presses a key in the game's own input system (no OS keyboard).</summary>
    void PressKey(VirtualKey key);

    /// <summary>Holds a key down in the game's own input system for <paramref name="durationMs"/>.</summary>
    void HoldKey(VirtualKey key, int durationMs);
}
