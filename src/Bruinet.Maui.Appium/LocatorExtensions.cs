using Bruinet.Core.Locators;
using Bruinet.Maui.Enums;
using OpenQA.Selenium;

namespace Bruinet.Maui.Appium;

/// <summary>
/// Converts a Bruinet Locator to an Appium/Selenium By selector. The mapping itself lives in
/// <see cref="AppiumPlatform"/>.
/// </summary>
internal static class LocatorExtensions
{
    // There is deliberately no platform-less ToBy overload. One existed and defaulted to
    // MauiPlatform.Windows, so every AutomationId on Android resolved as an AccessibilityId
    // (content-desc) instead of By.Id (resource-id) - which is how MAUI actually surfaces it
    // there. The platform is always known at the call site; requiring it makes the mistake
    // unrepresentable.

    /// <exception cref="ArgumentNullException">Thrown when locator is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the platform or locator strategy is not supported.</exception>
    public static By ToBy(this Locator locator, MauiPlatform platform)
        => AppiumPlatform.For(platform).ToBy(locator);

    /// <summary>
    /// Converts a locator for a search under an element, which must stay inside that element's
    /// subtree.
    /// </summary>
    public static By ToChildBy(this Locator locator, MauiPlatform platform)
        => AppiumPlatform.For(platform).ToChildBy(locator);
}
