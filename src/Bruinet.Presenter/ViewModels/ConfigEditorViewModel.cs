using System.Collections.ObjectModel;
using System.Windows.Input;
using Bruinet.Presenter.Commands;
using Bruinet.Presenter.Models;
using Bruinet.Presenter.Services;
using Bruinet.Uat;

namespace Bruinet.Presenter.ViewModels;

/// <summary>
/// Edits the two rows a workspace config carries, and shows what the rest derives to.
/// Save stays enabled while the config is invalid, so half-fixed work is never lost (AD-018).
/// </summary>
public sealed class ConfigEditorViewModel : ViewModelBase
{
    private readonly IFixtureInspector _fixtureInspector;
    private readonly Func<Task<string?>> _pickProject;
    private readonly Action _reloadWorkspace;

    private string _workspacePath = string.Empty;
    private string _configPath = string.Empty;
    private string _projectText = string.Empty;
    private string _savedProjectText = string.Empty;
    private string _fixtureText = string.Empty;
    private string _savedFixtureText = string.Empty;
    private string _derivedTarget = string.Empty;
    private string _derivedPages = string.Empty;
    private string _derivedApp = string.Empty;
    private string _projectProblem = string.Empty;
    private string _fixtureProblem = string.Empty;
    private string _saveMessage = string.Empty;
    private string _pagesAssemblyPath = string.Empty;
    private bool _configExists;
    private bool _hasErrors;
    private bool _loading;
    private DateTime _configWriteTimeAtLoad;

    /// <summary>Creates the editor.</summary>
    /// <param name="fixtureInspector">Reads fixture candidates out of the Pages assembly.</param>
    /// <param name="pickProject">Asks the person for a project file; returns null when cancelled.</param>
    /// <param name="reloadWorkspace">Reloads the workspace after a successful save.</param>
    public ConfigEditorViewModel(
        IFixtureInspector fixtureInspector,
        Func<Task<string?>> pickProject,
        Action reloadWorkspace)
    {
        _fixtureInspector = fixtureInspector;
        _pickProject = pickProject;
        _reloadWorkspace = reloadWorkspace;

        BrowseProjectCommand = new AsyncRelayCommand(BrowseProjectAsync);
        RescanFixturesCommand = new RelayCommand(RescanFixtures);
        SaveCommand = new RelayCommand(Save, () => IsDirty);
        RevertCommand = new RelayCommand(Revert, () => IsDirty);
        CreateCommand = new RelayCommand(Create, () => !ConfigExists && WorkspacePathIsSet);
    }

    /// <summary>The fixture names the Pages assembly offers.</summary>
    public ObservableCollection<string> FixtureCandidates { get; } = [];

    /// <summary>
    /// The platforms the chosen fixture declares through <c>[UatPlatforms]</c>, or empty when it
    /// declares none — in which case every platform the target supports is on offer and the
    /// fixture's own exception stays the error.
    /// </summary>
    public IReadOnlyList<string> DeclaredPlatforms { get; private set; } = [];

    /// <summary>Opens a file picker for the project.</summary>
    public ICommand BrowseProjectCommand { get; }

    /// <summary>Re-reads the fixtures from the built assembly.</summary>
    public ICommand RescanFixturesCommand { get; }

    /// <summary>Writes the edited rows and reloads.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Throws the edits away.</summary>
    public ICommand RevertCommand { get; }

    /// <summary>Writes a minimal config into a folder that has none.</summary>
    public ICommand CreateCommand { get; }

    /// <summary>The <c>Runtime Project</c> row, as edited.</summary>
    public string ProjectText
    {
        get => _projectText;
        set
        {
            if (SetProperty(ref _projectText, value))
            {
                OnEditChanged();
            }
        }
    }

    /// <summary>The <c>Runtime Fixture</c> row, as edited.</summary>
    public string FixtureText
    {
        get => _fixtureText;
        set
        {
            if (SetProperty(ref _fixtureText, value))
            {
                OnEditChanged();
            }
        }
    }

    /// <summary>The target, derived from the fixture's base type.</summary>
    public string DerivedTarget
    {
        get => _derivedTarget;
        private set => SetProperty(ref _derivedTarget, value);
    }

    /// <summary>Where the Pages assembly resolved, and how.</summary>
    public string DerivedPages
    {
        get => _derivedPages;
        private set => SetProperty(ref _derivedPages, value);
    }

    /// <summary>What owns the app path.</summary>
    public string DerivedApp
    {
        get => _derivedApp;
        private set => SetProperty(ref _derivedApp, value);
    }

    /// <summary>The problem attached to the project row, if any.</summary>
    public string ProjectProblem
    {
        get => _projectProblem;
        private set => SetProperty(ref _projectProblem, value);
    }

    /// <summary>The problem attached to the fixture row, if any.</summary>
    public string FixtureProblem
    {
        get => _fixtureProblem;
        private set => SetProperty(ref _fixtureProblem, value);
    }

    /// <summary>What the last save did, or why it could not.</summary>
    public string SaveMessage
    {
        get => _saveMessage;
        private set => SetProperty(ref _saveMessage, value);
    }

    /// <summary>Whether the folder has a config at all.</summary>
    public bool ConfigExists
    {
        get => _configExists;
        private set
        {
            if (SetProperty(ref _configExists, value))
            {
                OnPropertyChanged(nameof(CanCreate));
                (CreateCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Whether the create action applies.</summary>
    public bool CanCreate => !ConfigExists && WorkspacePathIsSet;

    /// <summary>Whether the editor holds unsaved changes.</summary>
    public bool IsDirty =>
        !string.Equals(_projectText, _savedProjectText, StringComparison.Ordinal) ||
        !string.Equals(_fixtureText, _savedFixtureText, StringComparison.Ordinal);

    /// <summary>The header line: clean, unsaved, or the number of problems.</summary>
    public string StateText
    {
        get
        {
            if (!ConfigExists)
            {
                return $"No {BddFileFormat.ConfigFileName}";
            }

            var problems = _hasErrors ? "problems" : string.Empty;
            return (IsDirty, problems.Length > 0) switch
            {
                (true, true) => $"{ConfigName}  * unsaved, problems",
                (true, false) => $"{ConfigName}  * unsaved",
                (false, true) => $"{ConfigName}  problems",
                _ => ConfigName
            };
        }
    }

    private bool WorkspacePathIsSet => _workspacePath.Length > 0;

    // uat.config.md or bdd.config.md, whichever the workspace has.
    private string ConfigName => _configPath.Length > 0 ? Path.GetFileName(_configPath) : BddFileFormat.ConfigFileName;

    /// <summary>Shows the workspace's config, discarding any unsaved edits.</summary>
    /// <param name="workspacePath">The workspace folder.</param>
    /// <param name="config">What the inspector made of it.</param>
    public void Load(string workspacePath, BddWorkspaceConfigLoadResult config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _loading = true;
        try
        {
            _workspacePath = workspacePath ?? string.Empty;
            _configPath = config.ConfigPath;
            ConfigExists = config.ConfigExists;
            _hasErrors = config.HasErrors;
            _configWriteTimeAtLoad = ReadWriteTime();

            _savedProjectText = config.Project?.Configured ?? string.Empty;
            _savedFixtureText = config.Fixture;
            ProjectText = _savedProjectText;
            FixtureText = _savedFixtureText;

            _pagesAssemblyPath = config.Assemblies
                .FirstOrDefault(assembly => assembly.Kind.Equals("Pages", StringComparison.OrdinalIgnoreCase))
                ?.ResolvedPath ?? string.Empty;

            DerivedTarget = config.Target.Length > 0
                ? $"{config.Target}   from the fixture's base"
                : "not derived yet";
            DerivedPages = DescribePages(config);
            DerivedApp = config.AppPath.Length > 0
                ? $"{config.ResolvedAppPath}   configured override"
                : "owned by the fixture";

            ProjectProblem = Describe(config.ProblemsFor(BddConfigSections.Runtime, BddConfigFields.Project));
            FixtureProblem = Describe(config.ProblemsFor(BddConfigSections.Runtime, BddConfigFields.Fixture));
            SaveMessage = string.Empty;

            RefreshFixtureCandidates();
        }
        finally
        {
            _loading = false;
            RaiseStateChanged();
        }
    }

    private static string Describe(IReadOnlyList<BddConfigProblem> problems) =>
        problems.Count == 0 ? string.Empty : string.Join(Environment.NewLine, problems.Select(p => p.Message));

    private static string DescribePages(BddWorkspaceConfigLoadResult config)
    {
        var pages = config.Assemblies.FirstOrDefault(assembly =>
            assembly.Kind.Equals("Pages", StringComparison.OrdinalIgnoreCase));
        if (pages is null)
        {
            return "not resolved";
        }

        var route = config.Project is not null && pages.Assembly == config.Project.Configured
            ? "evaluated from the project"
            : "configured";
        return $"{pages.ResolvedPath}   {route}, {(pages.Exists ? "built" : "not built")}";
    }

    private void OnEditChanged()
    {
        if (_loading)
        {
            return;
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(CanCreate));
        (SaveCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RevertCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CreateCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void RefreshFixtureCandidates()
    {
        FixtureCandidates.Clear();
        if (_pagesAssemblyPath.Length == 0)
        {
            return;
        }

        var inspection = _fixtureInspector.Inspect(_pagesAssemblyPath);
        foreach (var candidate in inspection.Candidates.Where(candidate => candidate.IsUsable))
        {
            FixtureCandidates.Add(candidate.Name);
        }

        DeclaredPlatforms = inspection.Find(FixtureText)?.Platforms ?? [];
        OnPropertyChanged(nameof(DeclaredPlatforms));
    }

    private void RescanFixtures()
    {
        RefreshFixtureCandidates();
        SaveMessage = FixtureCandidates.Count == 0
            ? "No usable fixture found. Build the project first."
            : $"{FixtureCandidates.Count} fixture(s) found.";
    }

    private async Task BrowseProjectAsync()
    {
        var picked = await _pickProject().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        ProjectText = MakeRelative(picked);
    }

    // A project under the workspace stays relative, so the config travels with the repo.
    private string MakeRelative(string path)
    {
        if (_workspacePath.Length == 0)
        {
            return path;
        }

        var relative = Path.GetRelativePath(_workspacePath, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : $"./{relative.Replace('\\', '/')}";
    }

    private void Save()
    {
        if (!ConfigExists)
        {
            return;
        }

        if (ReadWriteTime() != _configWriteTimeAtLoad)
        {
            SaveMessage = $"{ConfigName} changed on disk. Reload to see it, or save again to overwrite.";
            _configWriteTimeAtLoad = ReadWriteTime();
            return;
        }

        var edit = BddConfigEdit.Empty
            .WithField(BddConfigSections.Runtime, BddConfigFields.Project, Blank(ProjectText))
            .WithField(BddConfigSections.Runtime, BddConfigFields.Fixture, Blank(FixtureText));

        BddConfigWriter.ApplyToFile(_configPath, edit);
        _savedProjectText = ProjectText;
        _savedFixtureText = FixtureText;
        _configWriteTimeAtLoad = ReadWriteTime();
        SaveMessage = "Saved.";
        RaiseStateChanged();

        // Saving exists to make the workspace work, so it reloads; the tree keeps its place.
        _reloadWorkspace();
    }

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Revert()
    {
        _loading = true;
        ProjectText = _savedProjectText;
        FixtureText = _savedFixtureText;
        _loading = false;
        SaveMessage = "Reverted.";
        RaiseStateChanged();
    }

    private void Create()
    {
        if (ConfigExists || !WorkspacePathIsSet)
        {
            return;
        }

        var path = Path.Combine(_workspacePath, "uat.config.md");
        BddConfigWriter.CreateFile(path, new BddConfigTemplate(Blank(ProjectText) ?? "./", Blank(FixtureText)));
        SaveMessage = "Created uat.config.md.";
        _reloadWorkspace();
    }

    private DateTime ReadWriteTime() =>
        _configPath.Length > 0 && File.Exists(_configPath) ? File.GetLastWriteTimeUtc(_configPath) : DateTime.MinValue;
}
