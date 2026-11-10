
namespace Bruinet.Core.Testing;

/// <summary>How a MAUI suite runs its app (docs/guides/maui-headless.md): on a device, or rendered headless by MauiHeadless.</summary>
public enum MauiHead
{
    /// <summary>The built app on Windows (FlaUI), Android or iOS (Appium): the default, and the judge at every gate.</summary>
    Device,

    /// <summary>The app's pages rendered headless by MauiHeadless: no device, no layout, no pixels.</summary>
    Headless,
}

/// <summary>
/// The MAUI head this run uses, from <c>BRUINET_HEAD</c> (<c>headless</c>; anything else is the device),
/// the variable <see cref="WebHeads"/> reads too, so one run picks the headless head of every suite.
/// A fixture, a <c>DeviceFact</c> (<c>Bruinet.Maui.Testing</c>) and the scenario runner read it alike.
/// </summary>
public static class MauiHeads
{
    /// <summary>The environment variable that picks the head.</summary>
    public const string Variable = WebHeads.Variable;

    /// <summary>The head of this run.</summary>
    public static MauiHead Current => Parse(Environment.GetEnvironmentVariable(Variable));

    /// <summary>Whether this run renders with MauiHeadless.</summary>
    public static bool IsHeadless => Current == MauiHead.Headless;

    /// <summary>The head a value of <see cref="Variable"/> names; anything but <c>headless</c> is the device.</summary>
    public static MauiHead Parse(string? value)
        => string.Equals(value?.Trim(), "headless", StringComparison.OrdinalIgnoreCase) ? MauiHead.Headless : MauiHead.Device;
}
