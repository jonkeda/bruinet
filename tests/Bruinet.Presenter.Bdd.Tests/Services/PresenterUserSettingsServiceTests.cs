using Bruinet.Presenter.Models;
using Bruinet.Presenter.Services;
using Bruinet.Presenter.ViewModels;

namespace Bruinet.Presenter.Bdd.Tests.Services;

public sealed class PresenterUserSettingsServiceTests
{
    [Fact]
    public void RecordOpenedFolder_KeepsLastTenAndMovesDuplicatesToTop()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterSettings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var service = new PresenterUserSettingsService(settingsPath);
            var folders = Enumerable.Range(0, 12)
                .Select(index =>
                {
                    var folder = Path.Combine(root, $"Workspace{index:00}");
                    Directory.CreateDirectory(folder);
                    return folder;
                })
                .ToArray();

            foreach (var folder in folders)
            {
                service.RecordOpenedFolder(folder);
            }

            var settings = service.Load();
            Assert.Equal(10, settings.RecentFolders.Count);
            Assert.Equal(Path.GetFullPath(folders[11]), settings.LastOpenedFolder);
            Assert.Equal(Path.GetFullPath(folders[11]), settings.RecentFolders[0]);
            Assert.DoesNotContain(settings.RecentFolders, path => path.EndsWith("Workspace00", StringComparison.Ordinal));

            service.RecordOpenedFolder(folders[5]);

            settings = service.Load();
            Assert.Equal(10, settings.RecentFolders.Count);
            Assert.Equal(Path.GetFullPath(folders[5]), settings.RecentFolders[0]);
            Assert.Equal(1, settings.RecentFolders.Count(path => path.Equals(Path.GetFullPath(folders[5]), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Theme_RoundTripsThroughTheSettingsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterTheme", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var service = new PresenterUserSettingsService(settingsPath);

            Assert.Equal(PresenterTheme.System, service.Load().Theme);

            service.Save(new PresenterUserSettings
            {
                LastOpenedFolder = root,
                RecentFolders = [root],
                Theme = PresenterTheme.Dark
            });

            var reloaded = service.Load();
            Assert.Equal(PresenterTheme.Dark, reloaded.Theme);
            Assert.Equal(root, reloaded.LastOpenedFolder);
            Assert.Equal(root, Assert.Single(reloaded.RecentFolders));

            // Written by name, so the file stays readable and survives enum reordering.
            Assert.Contains("\"Dark\"", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Theme_DefaultsToSystemForASettingsFileWrittenBeforeTheThemeExisted()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterLegacyTheme", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            File.WriteAllText(
                settingsPath,
                """
                {
                  "LastOpenedFolder": "C:/Workspace",
                  "RecentFolders": [ "C:/Workspace" ]
                }
                """);

            var settings = new PresenterUserSettingsService(settingsPath).Load();

            Assert.Equal(PresenterTheme.System, settings.Theme);
            Assert.Equal("C:/Workspace", settings.LastOpenedFolder);
            Assert.Single(settings.RecentFolders);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RecordOpenedFolder_KeepsTheSavedTheme()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterThemeKeep", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Workspace");
        Directory.CreateDirectory(workspace);

        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var service = new PresenterUserSettingsService(settingsPath);
            service.Save(new PresenterUserSettings { Theme = PresenterTheme.Dark });

            service.RecordOpenedFolder(workspace);

            Assert.Equal(PresenterTheme.Dark, service.Load().Theme);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void PresenterShell_LoadsFirstExistingRecentFolderOnStartup()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStartup", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(root, "settings.json");
        var missing = Path.Combine(root, "Missing");
        var recent = Path.Combine(root, "RecentWorkspace");
        var fallback = Path.Combine(root, "FallbackWorkspace");
        Directory.CreateDirectory(recent);
        Directory.CreateDirectory(fallback);
        File.WriteAllText(Path.Combine(recent, "uat.config.md"), "# Recent");
        File.WriteAllText(Path.Combine(fallback, "uat.config.md"), "# Fallback");

        try
        {
            var settingsService = new PresenterUserSettingsService(settingsPath);
            settingsService.Save(new PresenterUserSettings
            {
                LastOpenedFolder = missing,
                RecentFolders = [missing, recent, fallback]
            });

            var workspaceService = new FakeWorkspaceService(fallback);
            var viewModel = new PresenterShellViewModel(
                workspaceService,
                new FakeExecutionService(),
                new FakeFolderPickerService(),
                settingsService);

            Assert.Equal(Path.GetFullPath(recent), Path.GetFullPath(viewModel.WorkspacePath!));
            Assert.DoesNotContain(missing, viewModel.RecentFoldersText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("RecentWorkspace", viewModel.WorkspaceName, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A clean workspace opens in Present mode, the bar docks to the remembered side, and the
    /// dock toggle moves it and remembers the new side.
    /// </summary>
    [Fact]
    public void PresenterShell_OpensInPresentMode_AndRemembersTheDock()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStartup", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "uat.config.md"), "# Workspace");

        try
        {
            var settingsService = new PresenterUserSettingsService(Path.Combine(root, "settings.json"));
            settingsService.Save(new PresenterUserSettings { LastOpenedFolder = workspace, Dock = PresenterDock.Right });
            var window = new FakeWindowService();
            var viewModel = new PresenterShellViewModel(
                new FakeWorkspaceService(workspace),
                new FakeExecutionService(),
                new FakeFolderPickerService(),
                settingsService,
                windowService: window);

            Assert.True(viewModel.IsPresentMode);
            viewModel.ApplyDock();
            viewModel.ToggleDockCommand.Execute(null);
            viewModel.ShowWorkspaceModeCommand.Execute(null);

            Assert.Equal([PresenterDock.Right, PresenterDock.Left], window.Docks);
            Assert.Equal(PresenterDock.Left, settingsService.Load().Dock);
            Assert.True(viewModel.IsWorkspaceMode);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// The Speed button cycles Fast, Normal and Slow; each speed's pause is set on the Settings
    /// page; both are remembered.
    /// </summary>
    [Fact]
    public void PresenterShell_CyclesSpeeds_AndRemembersTheirPauses()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStartup", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "uat.config.md"), "# Workspace");

        try
        {
            var settingsService = new PresenterUserSettingsService(Path.Combine(root, "settings.json"));
            settingsService.Save(new PresenterUserSettings { LastOpenedFolder = workspace });
            var viewModel = new PresenterShellViewModel(
                new FakeWorkspaceService(workspace),
                new FakeExecutionService(),
                new FakeFolderPickerService(),
                settingsService);

            Assert.Equal(PresenterSpeed.Normal, viewModel.Speed);
            Assert.Equal(800, viewModel.ExecutionDelayMilliseconds);

            viewModel.CycleSpeedCommand.Execute(null);
            Assert.Equal(PresenterSpeed.Slow, viewModel.Speed);
            Assert.Equal(1500, viewModel.ExecutionDelayMilliseconds);

            viewModel.SlowMillisecondsText = "2000";
            viewModel.FastMillisecondsText = "not a number";
            Assert.Equal(2000, viewModel.ExecutionDelayMilliseconds);

            viewModel.CycleSpeedCommand.Execute(null);
            Assert.Equal(PresenterSpeed.Fast, viewModel.Speed);
            Assert.Equal(250, viewModel.ExecutionDelayMilliseconds);

            var saved = settingsService.Load();
            Assert.Equal(PresenterSpeed.Fast, saved.Speed);
            Assert.Equal(2000, saved.Speeds.Slow);

            viewModel.ShowSettingsCommand.Execute(null);
            Assert.True(viewModel.IsSettingsTabSelected);
            viewModel.ShowSettingsCommand.Execute(null);
            Assert.True(viewModel.IsTreeTabSelected);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void OtherMonitor_IsTheLargestWithoutTheBar()
    {
        var bar = new System.Drawing.Rectangle(0, 0, 1920, 1040);
        var small = new System.Drawing.Rectangle(-1280, 0, 1280, 984);
        var large = new System.Drawing.Rectangle(1920, 0, 2560, 1400);

        Assert.Equal(large, PresenterLayout.OtherMonitor([bar, small, large], bar));
        Assert.Null(PresenterLayout.OtherMonitor([bar], bar));
    }

    /// <summary>The App window and When a run ends settings are remembered; the names map both ways.</summary>
    [Fact]
    public void PresenterShell_RemembersTheAppWindowAndRunEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStartup", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "uat.config.md"), "# Workspace");

        try
        {
            var settingsService = new PresenterUserSettingsService(Path.Combine(root, "settings.json"));
            settingsService.Save(new PresenterUserSettings { LastOpenedFolder = workspace });
            var viewModel = new PresenterShellViewModel(
                new FakeWorkspaceService(workspace),
                new FakeExecutionService(),
                new FakeFolderPickerService(),
                settingsService);

            Assert.Equal("Beside the bar", viewModel.AppWindowChoice);
            Assert.Equal("Close the app", viewModel.RunEndChoice);

            viewModel.AppWindowChoice = "Maximized, other monitor";
            viewModel.RunEndChoice = "Keep it open";

            var saved = settingsService.Load();
            Assert.Equal(PresenterAppWindow.MaximizedOtherMonitor, saved.AppWindow);
            Assert.Equal(PresenterRunEnd.KeepOpen, saved.RunEnd);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
    /// <summary>The Recording settings are remembered; Record turns on only where it can work.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("Recording needs ffmpeg.", false)]
    public void PresenterShell_RemembersTheRecordingSettings_AndRecordsOnlyWhereItCan(string? unavailable, bool records)
    {
        var root = Path.Combine(Path.GetTempPath(), "BruinetPresenterStartup", Guid.NewGuid().ToString("N"));
        var workspace = Path.Combine(root, "Workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "uat.config.md"), "# Workspace");

        try
        {
            var settingsService = new PresenterUserSettingsService(Path.Combine(root, "settings.json"));
            settingsService.Save(new PresenterUserSettings { LastOpenedFolder = workspace });
            var viewModel = new PresenterShellViewModel(
                new FakeWorkspaceService(workspace),
                new FakeExecutionService(),
                new FakeFolderPickerService(),
                settingsService,
                recorder: new FakeRecorder(unavailable));

            Assert.Equal("Default voice", viewModel.VoiceChoice);
            Assert.Equal("600", viewModel.BeatText);
            viewModel.RecordingsFolderText = @"C:\Videos";
            viewModel.BeatText = "450";
            viewModel.BeatText = "slow";
            viewModel.SpeakAloud = true;
            viewModel.TouchColorText = "#00a0ff";
            viewModel.TouchColorText = "#00a0f";
            viewModel.ToggleRecordCommand.Execute(null);

            var saved = settingsService.Load();
            Assert.Equal(@"C:\Videos", saved.RecordingsFolder);
            Assert.Null(saved.Voice);
            Assert.Equal(450, saved.Beat);
            Assert.True(saved.SpeakAloud);
            Assert.Equal("#00A0FF", saved.TouchColor);
            Assert.Equal(records, viewModel.IsRecording);
            Assert.Equal(unavailable ?? "Recording is on: the next run is recorded.", viewModel.RecordingStatus);
            Assert.False(viewModel.HasRecording);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeRecorder(string? unavailable) : IPresenterRecorder
    {
        public string? Unavailable { get; } = unavailable;

        public Task StartAsync(PresenterBddExecutionSession session, string workspacePath, string featureFile, string scenarioName, PresenterRecordingSettings settings, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PresenterRecorded?> FinishAsync(CancellationToken cancellationToken) => Task.FromResult<PresenterRecorded?>(null);

        public Task<bool> RehearseAsync(PresenterBddExecutionSession session, Bruinet.Narrator.BddVideoScenePlan scene, string? voice, TimeSpan beat, CancellationToken cancellationToken, string? instructions = null)
            => Task.FromResult(false);

        public Task StartSceneAsync(PresenterBddExecutionSession session, PresenterSceneRequest scene, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Bruinet.Narrator.BddSceneTimeline?> FinishSceneAsync(CancellationToken cancellationToken) => Task.FromResult<Bruinet.Narrator.BddSceneTimeline?>(null);

        public Task DropSceneAsync() => Task.CompletedTask;

        public Task<PresenterRecorded> StitchAsync(string title, string scenesFolder, string video, int sceneCount, Bruinet.Narrator.BddTouchMarks touches, CancellationToken cancellationToken)
            => Task.FromResult(new PresenterRecorded(video, true, []));

        public Task<bool> TryVoiceAsync(string text, string? voice, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    [Fact]
    public void Layout_PutsTheBarOnItsSide_AndTheAppBesideIt()
    {
        var work = new System.Drawing.Rectangle(0, 0, 1920, 1040);

        var left = PresenterLayout.Bar(work, PresenterDock.Left);
        var right = PresenterLayout.Bar(work, PresenterDock.Right);

        Assert.Equal(new System.Drawing.Rectangle(0, 0, 480, 1040), left);
        Assert.Equal(new System.Drawing.Rectangle(1440, 0, 480, 1040), right);
        Assert.Equal(System.Drawing.Rectangle.FromLTRB(488, 0, 1920, 1040), PresenterLayout.App(work, left, PresenterDock.Left));
        Assert.Equal(System.Drawing.Rectangle.FromLTRB(0, 0, 1432, 1040), PresenterLayout.App(work, right, PresenterDock.Right));
    }

    private sealed class FakeWindowService : IPresenterWindowService
    {
        public List<PresenterDock> Docks { get; } = [];

        public void Dock(PresenterDock dock) => Docks.Add(dock);

        public System.Drawing.Rectangle? TargetBounds(PresenterAppWindow mode, PresenterDock dock) => null;

        public object Snapshot() => new();

        public string? PlaceApp(object before, PresenterDock dock, PresenterAppWindow mode) => null;

        public void ReclaimFocus()
        {
        }

        public void SetBarTopmost(bool topmost)
        {
        }
    }

    private sealed class FakeFolderPickerService : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickProjectAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> PickScriptAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeExecutionService : IBddExecutionService
    {
        public Task<PresenterBddExecutionSession> CreateSessionAsync(
            string workspacePath,
            string scenarioFilePath,
            string scenarioName,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeWorkspaceService : IBddWorkspaceService
    {
        private readonly string _defaultWorkspace;

        public FakeWorkspaceService(string defaultWorkspace)
        {
            _defaultWorkspace = defaultWorkspace;
        }

        public string? FindDefaultWorkspace()
        {
            return _defaultWorkspace;
        }

        public BddWorkspaceLoadResult LoadFolder(string folderPath)
        {
            var fullPath = Path.GetFullPath(folderPath);
            return new BddWorkspaceLoadResult(
                fullPath,
                new DirectoryInfo(fullPath).Name,
                new BddWorkspaceConfigLoadResult(
                    true,
                    Path.Combine(fullPath, "uat.config.md"),
                    "MAUI",
                    "Fixture",
                    "App.exe",
                    "App.exe",
                    true,
                    string.Empty,
                    string.Empty,
                    true,
                    [],
                    []),
                [],
                [],
                [],
                "Discovery report:",
                "Command catalog:");
        }
    }
}
