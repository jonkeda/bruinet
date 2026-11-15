using Bruinet.Bdd;
using Bruinet.Core.Locators;
using Bruinet.Maui.Controls.Buttons;
using Bruinet.Maui.Controls.Display;
using Bruinet.Maui.Controls.Selection;
using Bruinet.Maui.Controls.Text;
using Bruinet.Maui.Controls.Toggle;
using Bruinet.Maui.Interfaces;
using Bruinet.Maui.Pages;
using Bruinet.Presenter.ViewModels;
using Bruinet.Uat;

namespace Bruinet.Presenter.Bdd.Tests.PageObjects;

[TestPage("Presenter")]
public sealed class PresenterPage : PageObjectBase<PresenterPage>
{
    public PresenterPage(IMauiTestContext context)
        : base(context)
    {
    }

    public override string Name => "Presenter";

    /// <summary>
    /// The page's root element. The default locator is the page <see cref="Name"/>, which is
    /// "Presenter" because that is what scenarios say; the root grid is <c>PresenterRoot</c>.
    /// </summary>
    protected override Locator Locator => new(LocatorStrategy.AutomationId, "PresenterRoot");

    [BddControl("Status Summary")]
    public PresenterStatusLabel<PresenterPage> StatusSummary => new(this, "StatusSummaryLabel");

    [BddControl("Workspace Summary")]
    public Label<PresenterPage> WorkspaceSummary => new(this, "WorkspaceSummaryLabel");

    [BddControl("Workspace Tree")]
    public Label<PresenterPage> WorkspaceTree => new(this, "WorkspaceTreeText");

    [BddControl("All Workspace Tree")]
    public Label<PresenterPage> AllWorkspaceTree => new(this, "AllWorkspaceTreeText");

    [BddControl("Step List")]
    public Label<PresenterPage> StepList => new(this, "StepListText");

    [BddControl("Workspace Config")]
    public Label<PresenterPage> WorkspaceConfig => new(this, "WorkspaceConfigText");

    [BddControl("Execution Timing")]
    public Label<PresenterPage> ExecutionTiming => new(this, "ExecutionTimingText");

    [BddControl("Run Scope")]
    public Label<PresenterPage> RunScope => new(this, "RunScopeText");

    [BddControl("Open Recent")]
    public Button<PresenterPage> OpenRecentButton => new(this, "OpenRecentButton");

    [BddControl("Recent Folders")]
    public Label<PresenterPage> RecentFolders => new(this, "RecentFoldersText");

    [BddControl("AUT Placement")]
    public Label<PresenterPage> AutPlacement => new(this, "AutPlacementText");

    [BddControl("Run")]
    public Button<PresenterPage> RunButton => new(this, "RunButton");

    public Button<PresenterPage> StopButton => new(this, "StopButton");

    public Button<PresenterPage> ReloadButton => new(this, "ReloadButton");

    /// <summary>Present mode: the scenarios, the steps and the moves.</summary>
    [BddControl("Present Mode")]
    public Button<PresenterPage> PresentModeButton => new(this, "PresentModeButton");

    /// <summary>Workspace mode: open, build, config, diagnostics and the tabs.</summary>
    [BddControl("Workspace Mode")]
    public Button<PresenterPage> WorkspaceModeButton => new(this, "WorkspaceModeButton");

    /// <summary>Narrator mode: a video script's scenes, run and recorded.</summary>
    [BddControl("Narrator Mode")]
    public Button<PresenterPage> NarratorModeButton => new(this, "NarratorModeButton");

    /// <summary>The open video script's file name.</summary>
    [BddControl("Narrator Script")]
    public Label<PresenterPage> NarratorScript => new(this, "NarratorScriptLabel");

    /// <summary>The open script's scenes, how many are checked, and its problems.</summary>
    [BddControl("Narrator Summary")]
    public Label<PresenterPage> NarratorSummary => new(this, "NarratorSummaryLabel");

    /// <summary>The Narrator tree as text, a row a line: <c>[x]</c> for a checked scene, its status, its name.</summary>
    [BddControl("Narrator Tree")]
    public Label<PresenterPage> NarratorTree => new(this, "NarratorTreeText");

    /// <summary>The script's checkbox: every scene at once.</summary>
    [BddControl("All Scenes")]
    public CheckBox<PresenterPage> AllScenes => new(this, "NarratorScriptCheck_Root");

    /// <summary>Expands and collapses the first scene.</summary>
    [BddControl("Scene 1 Toggle")]
    public Button<PresenterPage> Scene1Toggle => new(this, "NarratorSceneToggle_1");

    /// <summary>Whether the first scene is run and recorded.</summary>
    [BddControl("Scene 1")]
    public CheckBox<PresenterPage> Scene1 => new(this, "NarratorSceneCheck_1");

    /// <summary>Whether the second scene is run and recorded.</summary>
    [BddControl("Scene 2")]
    public CheckBox<PresenterPage> Scene2 => new(this, "NarratorSceneCheck_2");

    /// <summary>Settings: speaks a sample line in the chosen voice.</summary>
    [BddControl("Try Voice")]
    public Button<PresenterPage> TryVoiceButton => new(this, "TryVoiceButton");

    /// <summary>Settings: the narrator's voice.</summary>
    [BddControl("Voice")]
    public Picker<PresenterPage> VoicePicker => new(this, "VoicePicker");

    /// <summary>Narrator mode: joins the latest recording of every scene into the video.</summary>
    [BddControl("Stitch")]
    public Button<PresenterPage> StitchButton => new(this, "StitchButton");

    /// <summary>Opens the first scene's own recording; there only once the scene has one.</summary>
    [BddControl("Scene 1 Watch")]
    public Button<PresenterPage> Scene1Watch => new(this, "NarratorSceneWatch_1");

    [BddControl("Next")]
    public Button<PresenterPage> NextButton => new(this, "NextButton");

    /// <summary>The run's speed: a click cycles Fast, Normal and Slow.</summary>
    [BddControl("Speed")]
    public Button<PresenterPage> SpeedButton => new(this, "SpeedButton");

    /// <summary>Record: each run is recorded on video while it is on.</summary>
    [BddControl("Record")]
    public Button<PresenterPage> RecordButton => new(this, "RecordButton");

    /// <summary>Opens the last recording; there only once a run was recorded.</summary>
    [BddControl("Watch Recording")]
    public Button<PresenterPage> WatchRecordingButton => new(this, "WatchRecordingButton");

    /// <summary>What the last recording made, or why it could not; composing can take a while after the run.</summary>
    [BddControl("Recording Status")]
    public PresenterStatusLabel<PresenterPage> RecordingStatus => new(this, "RecordingStatusLabel");

    /// <summary>The Settings panel's Narrator heading: there while the panel is open.</summary>
    [BddControl("Narrator Settings")]
    public Label<PresenterPage> NarratorSettings => new(this, "NarratorSettingsLabel");

    /// <summary>Settings, Cloud voice: where its settings come from, or "Not set up.".</summary>
    [BddControl("Cloud Voice State")]
    public Label<PresenterPage> CloudVoiceState => new(this, "CloudVoiceStateLabel");

    /// <summary>Settings, Cloud voice: the Azure OpenAI resource.</summary>
    [BddControl("Cloud Endpoint")]
    public Entry<PresenterPage> CloudEndpoint => new(this, "CloudEndpointInput");

    /// <summary>Settings, Cloud voice: the key, a password field.</summary>
    [BddControl("Cloud Key")]
    public Entry<PresenterPage> CloudKey => new(this, "CloudKeyInput");

    /// <summary>Settings, Cloud voice: the voice when a script names none.</summary>
    [BddControl("Cloud Default Voice")]
    public Picker<PresenterPage> CloudDefaultVoice => new(this, "CloudDefaultVoicePicker");

    /// <summary>Settings, Cloud voice: whether the workspace may hold the file.</summary>
    [BddControl("Cloud Workspace Note")]
    public Label<PresenterPage> CloudWorkspaceNote => new(this, "CloudWorkspaceNoteLabel");

    /// <summary>Settings, Cloud voice: where Save writes.</summary>
    [BddControl("Cloud Save Path")]
    public Label<PresenterPage> CloudSavePath => new(this, "CloudSavePathLabel");

    [BddControl("Save Cloud Voice")]
    public Button<PresenterPage> SaveCloudVoiceButton => new(this, "SaveCloudVoiceButton");

    [BddControl("Remove Cloud Voice")]
    public Button<PresenterPage> RemoveCloudVoiceButton => new(this, "RemoveCloudVoiceButton");

    /// <summary>Settings, Cloud voice: what Try, Save, Remove or Add did.</summary>
    [BddControl("Cloud Voice Status")]
    public Label<PresenterPage> CloudVoiceStatus => new(this, "CloudVoiceStatusLabel");

    /// <summary>Opens and closes the Settings page.</summary>
    [BddControl("Settings")]
    public Button<PresenterPage> SettingsButton => new(this, "SettingsButton");

    /// <summary>The Config tab's state header: clean, unsaved, or problems.</summary>
    [BddControl("Config State")]
    public Label<PresenterPage> ConfigState => new(this, "ConfigStateLabel");

    /// <summary>The one editable path: the UAT project.</summary>
    [BddControl("Config Project")]
    public Entry<PresenterPage> ConfigProjectInput => new(this, "ConfigProjectInput");

    /// <summary>The fixture name.</summary>
    [BddControl("Config Fixture")]
    public Entry<PresenterPage> ConfigFixtureInput => new(this, "ConfigFixtureInput");

    /// <summary>The problem attached to the fixture row, empty when there is none.</summary>
    [BddControl("Config Fixture Problem")]
    public Label<PresenterPage> ConfigFixtureProblem => new(this, "ConfigDiagnosticRuntimeFixture");

    /// <summary>The target, derived from the fixture's base type.</summary>
    [BddControl("Config Derived Target")]
    public Label<PresenterPage> ConfigDerivedTarget => new(this, "ConfigDerivedTarget");

    /// <summary>Where the Pages assembly resolved, and how.</summary>
    [BddControl("Config Derived Pages")]
    public Label<PresenterPage> ConfigDerivedPages => new(this, "ConfigDerivedPages");

    /// <summary>What owns the app path.</summary>
    [BddControl("Config Derived App")]
    public Label<PresenterPage> ConfigDerivedApp => new(this, "ConfigDerivedApp");

    /// <summary>What the last save did.</summary>
    [BddControl("Config Save Message")]
    public Label<PresenterPage> ConfigSaveMessage => new(this, "ConfigSaveMessage");

    [BddControl("Config Save")]
    public Button<PresenterPage> ConfigSaveButton => new(this, "ConfigSaveButton");

    [BddControl("Config Revert")]
    public Button<PresenterPage> ConfigRevertButton => new(this, "ConfigRevertButton");

    [BddControl("Config Rescan Fixtures")]
    public Button<PresenterPage> ConfigFixtureRescanButton => new(this, "ConfigFixtureRescanButton");

    /// <summary>The Diagnostics tab's text, which carries a failed run's exception.</summary>
    [BddControl("Diagnostics Text")]
    public Label<PresenterPage> DiagnosticsText => new(this, "DiagnosticsText");

    [BddControl("Config")]
    public Button<PresenterPage> ConfigTabButton => new(this, "ConfigTabButton");

    [BddControl("Tree")]
    public Button<PresenterPage> TreeTabButton => new(this, "TreeTabButton");

    [BddControl("Diagnostics")]
    public Button<PresenterPage> DiagnosticsTabButton => new(this, "DiagnosticsTabButton");

    /// <summary>The workspace tree control; <see cref="WorkspaceTree"/> is its hidden text mirror.</summary>
    public PresenterWorkspaceTree WorkspaceTreeView => new(this);

    public Button<PresenterPage> TreeToggle(BddWorkspaceNodeKind kind, string name)
    {
        return new(this, $"WorkspaceNodeToggle_{SanitizeAutomationId($"{kind}_{name}")}");
    }

    public void ExpandTreeNode(BddWorkspaceNodeKind kind, string name, string expectedVisibleText, int timeoutMs = 5000)
    {
        var treeText = WorkspaceTree.GetText(timeoutMs: timeoutMs) ?? string.Empty;
        if (treeText.Contains(expectedVisibleText, StringComparison.Ordinal))
        {
            return;
        }

        WorkspaceTreeView.Node(kind, name).Expand(timeoutMs);
        WorkspaceTree.AssertTextContains(expectedVisibleText, timeoutMs: timeoutMs);
    }

    // No IsLoaded override: the base checks the page root, which every tab shares.
    // Probing a tab-specific element instead made the page look unloaded the moment
    // anything but the tree tab was selected.

    private static string SanitizeAutomationId(string value)
    {
        return string.Concat(value.Select(c => char.IsLetterOrDigit(c) ? c : '_')).Trim('_');
    }
}

public sealed class PresenterStatusLabel<TScope> : Label<TScope>
    where TScope : IMauiScope<TScope>
{
    public PresenterStatusLabel(IMauiScope<TScope> scope, string locatorValue)
        : base(scope, locatorValue)
    {
    }

    public new TScope AssertTextContains(string? expected, string? message = null, int? timeoutMs = null)
    {
        return base.AssertTextContains(expected, message, timeoutMs ?? 90000);
    }
}
