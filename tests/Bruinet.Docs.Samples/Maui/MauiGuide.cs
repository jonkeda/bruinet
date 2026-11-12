// docs/platform-guides/maui.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Maui.AppSupport;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace Bruinet.Docs.Samples.Maui;

public static class MauiGuide
{
    public static void Bridge(MauiAppBuilder builder)
    {
        #region docs:bridge
        builder.UseBruinetGestureBridge();   // in MauiProgram, before the first page loads
        #endregion
    }

    public static void Gesture(IMauiDriver driver)
    {
        #region docs:gesture
        if (driver.SupportsGesture("TestSwipeView", MauiGesture.SwipeRight))
        {
            driver.PerformGesture("TestSwipeView", MauiGesture.SwipeRight);   // throws if refused
        }
        #endregion
    }
}
