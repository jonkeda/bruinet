using Bruinet.Narrator;
using Bruinet.Presenter.Models;
using Bruinet.Presenter.Services;
using Bruinet.Presenter.ViewModels;

namespace Bruinet.Presenter.Bdd.Tests.Services;

/// <summary>
/// Run in Narrator mode: the checked scenes, in the script's order, a session each; a scene that
/// fails does not stop the ones after it (Present mode stops at the first failure).
/// </summary>
public sealed class NarratorRunTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BruinetNarratorRun", Guid.NewGuid().ToString("N"));
    private readonly string _feature;
    private readonly string _script;
    private readonly string _home;

    public NarratorRunTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Features"));
        _home = Path.Combine(_root, "home");
        Directory.CreateDirectory(_home);
        File.WriteAllText(Path.Combine(_root, "uat.config.md"), "# UAT Config\n");
        _feature = Path.Combine(_root, "Features", "greeting.feature.md");
        File.WriteAllText(
            _feature,
            """
            # Feature: Greeting

            ## Scenario: First

            ```gherkin
            Given I am on the Main page
            ```

            ## Scenario: Second

            ```gherkin
            Given I am on the Main page
            ```

            ## Scenario: Third

            ```gherkin
            Given I am on the Main page
            ```
            """);
        _script = Path.Combine(_root, "Features", "tour.video.md");
        File.WriteAllText(
            _script,
            """
            # Video: A tour

            ## Scene: One

            Scenario: Features/greeting.feature.md#First

            ## Scene: Two

            Scenario: Features/greeting.feature.md#Second

            ## Scene: Three

            Scenario: Features/greeting.feature.md#Third
            """);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Run_GoesOnAfterASceneThatFails_AndSkipsAnUncheckedOne()
    {
        var execution = new FailingExecutionService();
        var shell = Shell(execution);
        shell.ShowNarratorModeCommand.Execute(null);

        Assert.True(shell.IsNarratorMode);
        Assert.False(shell.IsPresentMode);
        Assert.False(shell.IsWorkspaceMode);
        Assert.Equal("tour.video.md", shell.Narrator.ScriptName);
        Assert.Equal(3, shell.Narrator.CheckedScenes.Count);

        shell.Narrator.Scenes[1].Node.IsChecked = false;
        shell.RunCommand.Execute(null);

        // No session can start here, so every scene fails (and the run ends at once): the third is still tried.
        Assert.Equal(["First", "Third"], execution.Started);
        Assert.Equal("Failed: 0/2 scenarios passed", shell.StatusSummary);
        Assert.Equal("fail", shell.Narrator.Scenes[0].Node.Status);
        Assert.Equal("wait", shell.Narrator.Scenes[1].Node.Status);
        Assert.Equal("fail", shell.Narrator.Scenes[2].Node.Status);
    }

    [Fact]
    public void Run_InPresentMode_StopsAtTheFirstFailure()
    {
        var execution = new FailingExecutionService();
        var shell = Shell(execution);

        shell.RunCommand.Execute(null);

        Assert.Single(execution.Started);
    }

    [Fact]
    public void Run_WithNoSceneChecked_SaysSo()
    {
        var execution = new FailingExecutionService();
        var shell = Shell(execution);
        shell.ShowNarratorModeCommand.Execute(null);
        shell.Narrator.Roots[0].IsChecked = false;

        shell.RunCommand.Execute(null);

        Assert.Empty(execution.Started);
        Assert.Equal("No scenes checked", shell.StatusSummary);
    }

    [Fact]
    public void OpenScript_LeavesTheModeOnNarrator_AndSelectsTheFirstScene()
    {
        var shell = Shell(new FailingExecutionService());

        shell.OpenScript(_script);

        Assert.True(shell.IsNarratorMode);
        Assert.Equal("First", shell.SelectedScenario?.Name);

        shell.Narrator.SelectedNode = shell.Narrator.Scenes[2].Node;
        Assert.Equal("Third", shell.SelectedScenario?.Name);

        shell.ShowPresentModeCommand.Execute(null);
        Assert.False(shell.IsNarratorMode);
        Assert.True(shell.IsPresentMode);
    }

    [Fact]
    public void Settings_TryTheVoice_AndSayWhenTheScriptNamesItsOwn()
    {
        var shell = Shell(new FailingExecutionService());
        Assert.False(shell.HasScriptVoiceNote);

        shell.TryVoiceCommand.Execute(null);
        Assert.Equal("Spoken in Default voice.", shell.VoiceStatus);

        File.WriteAllText(_script, File.ReadAllText(_script).Replace(
            "# Video: A tour",
            "# Video: A tour\n\n| Field | Value |\n| --- | --- |\n| Voice | Microsoft Jenny |",
            StringComparison.Ordinal));
        shell.OpenScript(_script);

        Assert.Equal("The open script asks for Microsoft Jenny: that voice is used for it.", shell.ScriptVoiceNote);
    }

    [Fact]
    public void CloudVoice_SavedForMe_IsTheUsersFile_AndTheKeyIsNotKept()
    {
        var shell = Shell(new FailingExecutionService());
        Assert.Equal("Not set up.", shell.CloudVoiceState);
        Assert.DoesNotContain("azure:coral", shell.VoiceChoices);

        shell.CloudEndpointText = "https://voice.openai.azure.com";
        shell.CloudDefaultVoice = "coral";
        shell.CloudKeyText = CloudKey;
        shell.SaveCloudVoiceCommand.Execute(null);

        var file = Path.Combine(_home, ".bruinet", VoiceSecrets.FileName);
        Assert.Equal(CloudKey, VoiceSecrets.Find(null, NoVariables, _home)?.Key);
        Assert.Equal($"Set up from {file}.", shell.CloudVoiceState);
        Assert.Equal(string.Empty, shell.CloudKeyText);
        Assert.Equal("Key set: leave empty to keep it", shell.CloudKeyPlaceholder);
        Assert.Contains("azure:coral", shell.VoiceChoices);
        Assert.DoesNotContain(
            typeof(PresenterShellViewModel).GetProperties().Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0),
            property => property.GetValue(shell) is string text && text.Contains(CloudKey, StringComparison.Ordinal));
    }

    [Fact]
    public void CloudVoice_ARealtimeUrl_IsNotSaved_AndTheStatusSaysWhy()
    {
        var shell = Shell(new FailingExecutionService());

        shell.CloudEndpointText = "wss://voice.openai.azure.com/openai/v1/realtime?model=gpt-realtime-mini";
        shell.CloudKeyText = CloudKey;
        shell.SaveCloudVoiceCommand.Execute(null);

        Assert.Contains("Realtime URL", shell.CloudVoiceStatus, StringComparison.Ordinal);
        Assert.Equal("Not set up.", shell.CloudVoiceState);
    }

    [Fact]
    public void CloudVoice_AWorkspaceInNoRepository_CannotHoldTheFile()
    {
        var shell = Shell(new FailingExecutionService());

        shell.CloudSaveForWorkspace = true;

        Assert.False(shell.CanSaveCloudForWorkspace);
        Assert.False(shell.CloudSaveForWorkspace);
        Assert.Equal(Path.Combine(_home, ".bruinet", VoiceSecrets.FileName), shell.CloudSavePath);
        Assert.Equal("This workspace is in no git repository: save it for this machine instead.", shell.CloudWorkspaceNote);
    }

    [Fact]
    public void CloudVoice_Try_SpeaksWithTheFieldsAsTheyAre_BeforeASave()
    {
        var recorder = new NoRecorder();
        var shell = Shell(new FailingExecutionService(), recorder);
        shell.CloudEndpointText = "https://voice.openai.azure.com";
        shell.CloudDefaultVoice = "sage";
        shell.CloudKeyText = CloudKey;

        shell.TryCloudVoiceCommand.Execute(null);

        var (voice, secrets) = Assert.Single(recorder.TriedCloud);
        Assert.Equal("azure:sage", voice);
        Assert.Equal(CloudKey, secrets.Key);
        Assert.Equal(VoiceSecrets.DefaultDeployment, secrets.Deployment);
        Assert.Equal("Spoken in azure:sage.", shell.CloudVoiceStatus);
        Assert.False(File.Exists(Path.Combine(_home, ".bruinet", VoiceSecrets.FileName)));
    }

    [Fact]
    public void CloudVoice_Remove_AsksOnce_ThenDeletesTheFile()
    {
        var file = Path.Combine(_home, ".bruinet", VoiceSecrets.FileName);
        VoiceSecrets.Save(file, "https://voice.openai.azure.com", null, null, CloudKey, null);
        var shell = Shell(new FailingExecutionService());

        shell.RemoveCloudVoiceCommand.Execute(null);
        Assert.True(File.Exists(file));
        Assert.Equal("Click again to delete", shell.RemoveCloudVoiceText);

        shell.RemoveCloudVoiceCommand.Execute(null);
        Assert.False(File.Exists(file));
        Assert.Equal("Not set up.", shell.CloudVoiceState);
    }

    [Fact]
    public void CloudVoice_SetUpByTheEnvironment_SaysSo()
    {
        var shell = Shell(new FailingExecutionService(), readVariable: name => name is VoiceSecrets.KeyVariable or VoiceSecrets.EndpointVariable ? "set" : null);

        Assert.True(shell.IsCloudVoiceSetUp);
        Assert.Equal($"Set up from the environment ({VoiceSecrets.KeyVariable}, {VoiceSecrets.EndpointVariable}).", shell.CloudVoiceState);
    }

    [Fact]
    public void Speech_Saved_IsTheSpeechObjectOfTheUsersFile_TheKeyIsNotKept_AndOpenAisFieldsStay()
    {
        var file = Path.Combine(_home, ".bruinet", VoiceSecrets.FileName);
        VoiceSecrets.Save(file, "https://voice.openai.azure.com", null, null, CloudKey, "coral");
        var shell = Shell(new FailingExecutionService());
        Assert.Equal("Not set up.", shell.SpeechState);
        Assert.DoesNotContain("azure-speech-service:en-GB-Emily:MAI-Voice-2.1", shell.VoiceChoices);

        shell.SpeechEndpointText = "eastus2";
        shell.SpeechKeyText = SpeechKey;
        shell.SpeechSampleRate = 48000;
        shell.SaveSpeechCommand.Execute(null);

        var saved = SpeechSecrets.Find(null, NoVariables, _home)!;
        Assert.Equal((SpeechKey, "eastus2", 48000), (saved.Key, saved.Endpoint, saved.SampleRate));
        Assert.Equal("coral", VoiceSecrets.Find(null, NoVariables, _home)?.Voice);
        Assert.Equal($"Set up from {file}.", shell.SpeechState);
        Assert.Equal(string.Empty, shell.SpeechKeyText);
        Assert.Equal("Key set: leave empty to keep it", shell.SpeechKeyPlaceholder);
        Assert.Contains("azure-speech-service:en-GB-Emily:MAI-Voice-2.1", shell.VoiceChoices);
        Assert.DoesNotContain(
            typeof(PresenterShellViewModel).GetProperties().Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0),
            property => property.GetValue(shell) is string text && text.Contains(SpeechKey, StringComparison.Ordinal));
    }

    [Fact]
    public void Speech_AnEndpointThatIsNotARegionOrHttps_IsNotSaved_AndTheStatusSaysWhy()
    {
        var shell = Shell(new FailingExecutionService());

        shell.SpeechEndpointText = "http://insecure.example";
        shell.SpeechKeyText = SpeechKey;
        shell.SaveSpeechCommand.Execute(null);

        Assert.Contains("https://", shell.SpeechStatus, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home, ".bruinet", VoiceSecrets.FileName)));
        Assert.Equal("Not set up.", shell.SpeechState);
    }

    [Fact]
    public void Speech_Try_SpeaksWithTheFieldsAsTheyAre_BeforeASave()
    {
        var recorder = new NoRecorder();
        var shell = Shell(new FailingExecutionService(), recorder);
        shell.SpeechEndpointText = "eastus2";
        shell.SpeechKeyText = SpeechKey;

        shell.TrySpeechCommand.Execute(null);

        var (voice, secrets) = Assert.Single(recorder.TriedSpeech);
        Assert.Equal("azure-speech-service:en-GB-Emily:MAI-Voice-2.1", voice);
        Assert.Equal(SpeechKey, secrets.Key);
        Assert.Equal("Spoken in azure-speech-service:en-GB-Emily:MAI-Voice-2.1.", shell.SpeechStatus);
        Assert.False(File.Exists(Path.Combine(_home, ".bruinet", VoiceSecrets.FileName)));
    }

    [Fact]
    public void Speech_TryWithoutAKey_SaysWhatIsNeeded()
    {
        var recorder = new NoRecorder();
        var shell = Shell(new FailingExecutionService(), recorder);
        shell.SpeechEndpointText = "eastus2";

        shell.TrySpeechCommand.Execute(null);

        Assert.Empty(recorder.TriedSpeech);
        Assert.Equal("Azure Speech needs an endpoint and a key.", shell.SpeechStatus);
    }

    [Fact]
    public void Speech_LoadVoices_FillsThePickerForTheLocale_AndAPickedVoiceBecomesTheDefault()
    {
        var recorder = new NoRecorder
        {
            SpeechVoices = [new SpeechVoice("en-GB-Emily:MAI-Voice-2.1", "en-GB", "Female", ["narrator"], "Neural", "Preview"), new SpeechVoice("en-GB-Harry:MAI-Voice-2.1", "en-GB", "Male", [], "Neural", "Preview")],
        };
        var shell = Shell(new FailingExecutionService(), recorder);
        shell.SpeechEndpointText = "eastus2";
        shell.SpeechKeyText = SpeechKey;
        shell.SpeechLocale = "en-GB";

        shell.LoadSpeechVoicesCommand.Execute(null);

        Assert.Equal(["en-GB-Emily:MAI-Voice-2.1", "en-GB-Harry:MAI-Voice-2.1"], shell.SpeechVoiceChoices);
        Assert.Equal("2 voices for en-GB.", shell.SpeechStatus);
        Assert.Equal("en-GB", recorder.ListedLocale);

        shell.SpeechPickedVoice = "en-GB-Harry:MAI-Voice-2.1";
        Assert.Equal("en-GB-Harry:MAI-Voice-2.1", shell.SpeechDefaultVoice);
    }

    [Fact]
    public void Speech_Remove_AsksOnce_ThenRemovesOnlyItsObject()
    {
        var file = Path.Combine(_home, ".bruinet", VoiceSecrets.FileName);
        VoiceSecrets.Save(file, "https://voice.openai.azure.com", null, null, CloudKey, null);
        SpeechSecrets.Save(file, "eastus2", SpeechKey, null);
        var shell = Shell(new FailingExecutionService());

        shell.RemoveSpeechCommand.Execute(null);
        Assert.NotNull(SpeechSecrets.Find(null, NoVariables, _home));
        Assert.Equal("Click again to delete", shell.RemoveSpeechText);

        shell.RemoveSpeechCommand.Execute(null);
        Assert.Null(SpeechSecrets.Find(null, NoVariables, _home));
        Assert.NotNull(VoiceSecrets.Find(null, NoVariables, _home));
        Assert.Equal("Not set up.", shell.SpeechState);
        Assert.True(shell.IsCloudVoiceSetUp);
    }

    [Fact]
    public void Speech_SetUpByTheEnvironment_SaysSo()
    {
        var shell = Shell(new FailingExecutionService(), readVariable: name => name is SpeechSecrets.KeyVariable or SpeechSecrets.EndpointVariable ? "set" : null);

        Assert.True(shell.IsSpeechSetUp);
        Assert.Equal($"Set up from the environment ({SpeechSecrets.KeyVariable}, {SpeechSecrets.EndpointVariable}).", shell.SpeechState);
        Assert.Contains("azure-speech-service:en-GB-Emily:MAI-Voice-2.1", shell.VoiceChoices);
    }

    [Fact]
    public void Speech_ARunLeavesTheDevelopersRealFileAlone()
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bruinet", VoiceSecrets.FileName);
        var before = File.Exists(real) ? File.ReadAllBytes(real) : null;
        var shell = Shell(new FailingExecutionService());
        shell.SpeechEndpointText = "eastus2";
        shell.SpeechKeyText = SpeechKey;
        shell.SaveSpeechCommand.Execute(null);
        shell.RemoveSpeechCommand.Execute(null);
        shell.RemoveSpeechCommand.Execute(null);

        Assert.Equal(before, File.Exists(real) ? File.ReadAllBytes(real) : null);
    }

    private const string CloudKey = "secret-key-123";
    private const string SpeechKey = "speech-key-987";

    private static string? NoVariables(string name) => null;

    private PresenterShellViewModel Shell(IBddExecutionService execution, NoRecorder? recorder = null, Func<string, string?>? readVariable = null)
        => new(new Workspace(_root, _feature), execution, new NoPicker(), new MemorySettings(), recorder: recorder ?? new NoRecorder(), userFolder: _home, readVariable: readVariable ?? NoVariables);

    private sealed class FailingExecutionService : IBddExecutionService
    {
        public List<string> Started { get; } = [];

        public IReadOnlyDictionary<string, string> WindowEnvironment { get; set; } = new Dictionary<string, string>();

        public Task<PresenterBddExecutionSession> CreateSessionAsync(string workspacePath, string scenarioFilePath, string scenarioName, CancellationToken cancellationToken)
        {
            Started.Add(scenarioName);
            throw new InvalidOperationException("No app here.");
        }
    }

    private sealed class Workspace(string root, string feature) : IBddWorkspaceService
    {
        public string? FindDefaultWorkspace() => root;

        public BddWorkspaceLoadResult LoadFolder(string folderPath)
        {
            BddScenarioLoadResult Scenario(string name, int line)
                => new(name, "Greeting", feature, [], [new BddStepLoadResult("wait", "Given I am on the Main page", "Page.Open", line)]);

            return new BddWorkspaceLoadResult(
                root,
                "Workspace",
                new BddWorkspaceConfigLoadResult(true, Path.Combine(root, "uat.config.md"), "MAUI", "Fixture", "App.exe", "App.exe", true, string.Empty, string.Empty, true, [], []),
                [new BddFileLoadResult(feature, "greeting.feature.md", true, true, [])],
                [Scenario("First", 6), Scenario("Second", 12), Scenario("Third", 18)],
                [],
                "Discovery report:",
                "Command catalog:");
        }
    }

    private sealed class NoPicker : IFolderPickerService
    {
        public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<string?> PickProjectAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

        public Task<string?> PickScriptAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class MemorySettings : IPresenterUserSettingsService
    {
        private PresenterUserSettings _settings = new();

        public PresenterUserSettings Load() => _settings;

        public void Save(PresenterUserSettings settings) => _settings = settings;

        public PresenterUserSettings RecordOpenedFolder(string folderPath) => _settings;
    }

    private sealed class NoRecorder : IPresenterRecorder
    {
        public string? Unavailable => "No ffmpeg in a unit test.";

        public Task StartAsync(PresenterBddExecutionSession session, string workspacePath, string featureFile, string scenarioName, PresenterRecordingSettings settings, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PresenterRecorded?> FinishAsync(CancellationToken cancellationToken) => Task.FromResult<PresenterRecorded?>(null);

        public Task<bool> RehearseAsync(PresenterBddExecutionSession session, BddVideoScenePlan scene, string? voice, TimeSpan beat, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task StartSceneAsync(PresenterBddExecutionSession session, PresenterSceneRequest scene, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<BddSceneTimeline?> FinishSceneAsync(CancellationToken cancellationToken) => Task.FromResult<BddSceneTimeline?>(null);

        public Task DropSceneAsync() => Task.CompletedTask;

        public Task<PresenterRecorded> StitchAsync(string title, string scenesFolder, string video, int sceneCount, BddTouchMarks touches, CancellationToken cancellationToken)
            => Task.FromResult(new PresenterRecorded(video, true, []));

        public Task<bool> TryVoiceAsync(string text, string? voice, CancellationToken cancellationToken)
        {
            Tried.Add(voice);
            return Task.FromResult(true);
        }

        public List<string?> Tried { get; } = [];

        public List<(string Voice, VoiceSecrets Secrets)> TriedCloud { get; } = [];

        public Task TryCloudVoiceAsync(string text, string voice, VoiceSecrets secrets, CancellationToken cancellationToken)
        {
            TriedCloud.Add((voice, secrets));
            return Task.CompletedTask;
        }

        public List<(string Voice, SpeechSecrets Secrets)> TriedSpeech { get; } = [];

        public IReadOnlyList<SpeechVoice> SpeechVoices { get; init; } = [];

        public string? ListedLocale { get; private set; }

        public Task TryAzureSpeechAsync(string text, string voice, SpeechSecrets secrets, CancellationToken cancellationToken)
        {
            TriedSpeech.Add((voice, secrets));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SpeechVoice>> ListSpeechVoicesAsync(SpeechSecrets secrets, string locale, CancellationToken cancellationToken)
        {
            ListedLocale = locale;
            return Task.FromResult(AzureSpeechVoices.Of(SpeechVoices, locale));
        }
    }
}
