// docs/guides/maui-headless.md quotes these regions: change them here, then run tools/Scripts/update-doc-samples.ps1.
using Bruinet.Maui.Context;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Headless;
using Bruinet.Maui.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace Bruinet.Docs.Samples.Maui;

public sealed class HeadlessAppFixture : MauiTestFixtureBase
{
    private readonly AppSettings settings = new();
    private readonly AppPlatform platform = new();

    protected override string GetDefaultAppPath(MauiPlatform platform) => "MyApp.exe";

    #region docs:options
    protected override MauiTestContextOptions CreateHeadlessOptions()
    {
        // Debug parses XAML at run time and knows only loaded assemblies: load what the builder would.
        _ = typeof(CommunityToolkit.Maui.Layouts.StateContainer).Assembly;
        return MauiHeadlessHead.Options(
            services => services.GetRequiredService<AppShell>(),                                            // the first page
            options => options.Application = services => new App(services.GetRequiredService<AppShell>()),  // the app's Application
            context => context.Services.AddMyApp(settings, platform));                                      // the app's composition
    }
    #endregion
}

public sealed class AppSettings;

public sealed class AppPlatform;

public sealed class AppShell : Shell;

public sealed class App(AppShell shell) : Application
{
    public AppShell Shell { get; } = shell;
}

public static class MyAppServices
{
    public static IServiceCollection AddMyApp(this IServiceCollection services, AppSettings settings, AppPlatform platform)
        => services.AddSingleton(settings).AddSingleton(platform).AddSingleton<AppShell>();
}
