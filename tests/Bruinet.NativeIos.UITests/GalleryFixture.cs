using Bruinet.Appium;
using Bruinet.NativeIos.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bruinet.NativeIos.UITests;

/// <summary>
/// One Appium session for the gallery app, shared by the UI tests and the UAT suite. The app is
/// uninstalled before the session opens, so it is installed fresh with its permissions cleared;
/// each test opens its screen by deep link.
/// </summary>
[TestModuleScan(typeof(LoginPage), NamespacePrefix = "Bruinet.NativeIos.UITests.Pages")]
public sealed class GalleryFixture : NativeIosTestFixtureBase, IAsyncLifetime
{
    /// <summary>The gallery app's bundle id.</summary>
    public const string BundleId = "com.bruinet.samples.nativeios";

    private TestComposition? _composition;

    /// <summary>The pages and the context, for UAT scenarios.</summary>
    public TestComposition Composition
        => _composition ??= TestComposition.ForFixture(this, services => services.AddSingleton(Context));

    /// <inheritdoc />
    protected override NativeIosTestContextOptions CreateOptions()
    {
        var options = base.CreateOptions();
        options.AppPath ??= GalleryApp();
        options.BundleId ??= BundleId;
        options.Udid ??= IosHost.BootedSimulator();

        // The permission prompt is under test (DialogsTests): it must not be accepted for the test.
        options.AutoAcceptAlerts = false;
        IosHost.Uninstall(options.Udid, options.BundleId);
        options.Timeouts = new Bruinet.Core.Configuration.TimeoutSettings { DefaultWait = TimeSpan.FromMilliseconds(10000), PageLoad = TimeSpan.FromMilliseconds(15000), PollingInterval = TimeSpan.FromMilliseconds(100) };
        return options;
    }

    /// <summary>Opens a gallery screen by its deep link (<c>bruinet-native://login</c>).</summary>
    public void Open(string screen) => Context.NavigateTo($"bruinet-native://{screen}");

    /// <summary>"Given I am on the Sign In page".</summary>
    public void NavigateToSignIn() => Open("login");

    /// <summary>"Given I am on the Controls page".</summary>
    public void NavigateToControls() => Open("controls");

    /// <summary>"Given I am on the Invoices page".</summary>
    public void NavigateToInvoices() => Open("invoices");

    /// <summary>"Given I am on the Dialogs page".</summary>
    public void NavigateToDialogs() => Open("dialogs");

    /// <summary>"Given I am on the Navigation page".</summary>
    public void NavigateToNavigation() => Open("navigation");

    Task IAsyncLifetime.InitializeAsync() => InitializeAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync();

    // The sample's Debug build, beside this repository's samples; APPIUM_APP_PATH overrides it.
    private static string GalleryApp()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var app = Path.Combine(folder.FullName, "samples", "Bruinet.Samples.NativeIos.App", "bin", "Debug", "net10.0-ios", "iossimulator-arm64", "Bruinet.Samples.NativeIos.App.app");
            if (Directory.Exists(app))
            {
                return app;
            }
        }

        throw new DirectoryNotFoundException("The gallery app is not built. Run: dotnet build samples/Bruinet.Samples.NativeIos.App, or set APPIUM_APP_PATH.");
    }
}

/// <summary>The suite's one session: UI test classes and the UAT suite run one after another on it.</summary>
[CollectionDefinition(Name)]
public sealed class GalleryCollection : ICollectionFixture<GalleryFixture>
{
    public const string Name = "Gallery";
}
