namespace Bruinet.Maui.Enums;

/// <summary>
/// What drives the built app on a device: the platform's own runner, or MauiDevFlow's agent inside
/// the app (<c>Bruinet.Maui.DevFlow</c>, .my/devflow-runner/design.md).
/// </summary>
public enum MauiRunner
{
    /// <summary>The platform's runner: FlaUI on Windows, Appium on Android and iOS.</summary>
    Default,

    /// <summary>
    /// The MauiDevFlow agent the app carries, over HTTP on loopback: the same runner on every
    /// platform. Its input is semantic (a tap invokes, a fill sets the text), so it does not judge a gate.
    /// </summary>
    DevFlow,
}
