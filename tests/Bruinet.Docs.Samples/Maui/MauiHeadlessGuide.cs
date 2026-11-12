// docs/guides/maui-headless.md quotes this region: change it here, then run tools/Scripts/update-doc-samples.ps1.
using MauiHeadless;
using MauiHeadless.Assertions;
using MauiHeadless.Dialogs;
using MauiHeadless.Interaction;
using MauiHeadless.Samples.App.Pages;
using MauiHeadless.Samples.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace Bruinet.Docs.Samples.Maui;

public static class MauiHeadlessGuide
{
    public static void SettingsPage_SavesTheName()
    {
        #region docs:standalone
        using var ctx = new MauiTestContext();
        ctx.Services.AddSingleton<ISettingsService, PreferencesSettingsService>();
        ctx.Preferences.Set("name", "Ada");

        var page = ctx.Render<SettingsPage>();
        page.Find<Entry>("Name").Should().HaveText("Ada");
        page.Find<Entry>("Name").SetText("Grace");
        page.Find<Button>("SaveButton").Click();

        ctx.AdvanceTime(TimeSpan.FromSeconds(1));          // timers and delayed work, in order
        page.Find<TestDialog>().Choose("OK");               // a DisplayAlertAsync, answered
        #endregion
    }
}
