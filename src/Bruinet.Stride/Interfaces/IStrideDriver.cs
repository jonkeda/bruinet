namespace Bruinet.Stride.Interfaces;

/// <summary>
/// The automation pipe to the Stride game under test: one-attempt reads and lookups, actions, keys
/// fed to the game's own input, and the game's readiness. A game that is gone fails a call with
/// <see cref="AppUnavailableException"/>.
/// </summary>
public interface IStrideDriver : IDisposable
{
    /// <summary>The state of the element a target names (a name, <c>#key</c>, or empty for the root). One attempt.</summary>
    ElementState GetState(string target);

    /// <summary>The first descendant of <paramref name="scope"/> that <paramref name="target"/> names; <c>Exists</c> false when none. One attempt.</summary>
    ElementState Find(string scope, string target);

    /// <summary>Every descendant of <paramref name="scope"/> that <paramref name="target"/> names. One attempt.</summary>
    IReadOnlyList<ElementState> FindAll(string scope, string target);

    /// <summary>The children of <paramref name="scope"/>. One attempt.</summary>
    IReadOnlyList<ElementState> GetChildren(string scope);

    /// <summary>Runs one action (<c>Click</c>, <c>Toggle</c>, <c>SetText</c>, <c>SetSliderValue</c>) on the element a target names.</summary>
    /// <exception cref="InvalidOperationException">The game refused the action.</exception>
    void Act(string method, string target, params object[] args);

    /// <summary>Whether the game says it is ready (its scene loaded).</summary>
    bool IsGameReady();

    /// <summary>Whether the game says it is busy.</summary>
    bool IsGameBusy();

    /// <summary>Presses a key in the game's own input system, for one frame or more.</summary>
    void PressKey(VirtualKey key);

    /// <summary>Holds a key down in the game's own input system for <paramref name="durationMs"/>.</summary>
    void HoldKey(VirtualKey key, int durationMs);

    /// <summary>The game window as PNG bytes; empty when the game took none.</summary>
    byte[] TakeScreenshot();

    /// <summary>The game window's handle, for a recorder; zero when it is not known.</summary>
    nint WindowHandle { get; }
}
