using Bruinet.Maui.Context;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Testing;
using Bruinet.Presenter.Bdd.Tests.PageObjects;

namespace Bruinet.Presenter.Bdd.Tests.Runtime;

[TestModuleScan(typeof(PresenterPage), NamespacePrefix = "Bruinet.Presenter.Bdd.Tests.PageObjects")]
public sealed class PresenterFixture : MauiTestFixtureBase
{
    public PresenterFixture()
    {
        Composition = TestComposition.ForFixture(this, services =>
            services.AddSingleton<IMauiTestContext>(Context));
    }

    public TestComposition Composition { get; }

    /// <summary>Where Presenter's Record puts its videos during the suite.</summary>
    public string RecordingsFolder { get; private set; } = string.Empty;

    protected override MauiTestContextOptions CreateTestContextOptions(MauiTestContextOptions loaded)
    {
        var settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            "BruinetPresenterUat",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDirectory);
        var settingsPath = Path.Combine(settingsDirectory, "presenter-settings.json");
        Environment.SetEnvironmentVariable("BRUINET_PRESENTER_SETTINGS_PATH", settingsPath);

        // The cloud voice's file goes to a user folder of the suite's own: the developer's real
        // %USERPROFILE%\.bruinet\voice.secrets.json is never read or written by these scenarios.
        var userFolder = Path.Combine(settingsDirectory, "home");
        Directory.CreateDirectory(userFolder);
        Environment.SetEnvironmentVariable(Bruinet.Presenter.ViewModels.PresenterShellViewModel.UserFolderVariable, userFolder);

        // Recordings go beside the settings, not into the repository's TestResults.
        RecordingsFolder = Path.Combine(settingsDirectory, "recordings");
        // The app the Presenter presents goes behind everything, as the Presenter itself does in a
        // test run: neither takes the screen from the person at the machine.
        File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            RecordingsFolder,
            AppWindow = Bruinet.Presenter.Services.PresenterAppWindow.Background,
        }));

        return base.CreateTestContextOptions(loaded);
    }

    protected override string GetDefaultAppPath(MauiPlatform platform)
    {
        var solutionDirectory = FindSolutionDirectory();
        return platform switch
        {
            MauiPlatform.Windows => Path.Combine(
               solutionDirectory,
               "src",
               "Bruinet.Presenter",
               "bin",
               "Debug",
               "net10.0-windows10.0.19041.0",
               "win-x64",
               "Bruinet.Presenter.exe"),
            _ => string.Empty
        };
    }
}
