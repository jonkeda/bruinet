using System.ComponentModel;
using System.Text;
using System.Windows.Input;
using Bruinet.Presenter.Commands;
using Microsoft.Maui.Graphics;

namespace Bruinet.Presenter.ViewModels;

public sealed class BddWorkspaceNodeViewModel : ViewModelBase
{
    private readonly Action<BddWorkspaceNodeViewModel>? _expansionChanged;
    private bool _isExpanded;

    public BddWorkspaceNodeViewModel(
        string name,
        BddWorkspaceNodeKind kind,
        int depth,
        string? filePath = null,
        BddScenarioViewModel? scenario = null,
        BddStepViewModel? step = null,
        Action<BddWorkspaceNodeViewModel>? expansionChanged = null)
    {
        Name = name;
        Kind = kind;
        Depth = depth;
        FilePath = filePath;
        Scenario = scenario;
        Step = step;
        _expansionChanged = expansionChanged;
        // One key for both ids, so a tree node object addresses the row and its toggle from the
        // same kind and name.
        var stableId = SanitizeAutomationId($"{kind}_{name}");
        AutomationId = $"WorkspaceNode_{stableId}";
        ToggleAutomationId = $"WorkspaceNodeToggle_{stableId}";
        ToggleExpansionCommand = new RelayCommand(ToggleExpansion, () => CanExpand);

        if (Scenario is not null)
        {
            Scenario.PropertyChanged += OnChildStatusChanged;
        }

        if (Step is not null)
        {
            Step.PropertyChanged += OnChildStatusChanged;
        }
    }

    public string Name { get; }

    public BddWorkspaceNodeKind Kind { get; }

    public int Depth { get; }

    public double IndentWidth => Depth * 18;

    public string? FilePath { get; }

    public BddScenarioViewModel? Scenario { get; }

    public BddStepViewModel? Step { get; }

    public BddWorkspaceNodeViewModel? Parent { get; private set; }

    /// <summary>
    /// This node's position in the tree, as kinds and names from the root.
    /// </summary>
    /// <remarks>
    /// A reload rebuilds every node, so expansion and selection are carried across by this
    /// rather than by reference. Name alone is not unique - two folders can hold a file of the
    /// same name - so the whole chain is the key.
    /// </remarks>
    public string NodePath => Parent is null ? $"{Kind}:{Name}" : $"{Parent.NodePath}/{Kind}:{Name}";

    public string AutomationId { get; }

    public string ToggleAutomationId { get; }

    public ICommand ToggleExpansionCommand { get; }

    public List<BddWorkspaceNodeViewModel> Children { get; } = [];

    public bool CanExpand => Children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpansionText));
                OnPropertyChanged(nameof(ExpansionGlyph));
                OnPropertyChanged(nameof(ExpansionDescription));
                OnPropertyChanged(nameof(DisplayText));
                _expansionChanged?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// The ASCII expansion marker, used <b>only</b> by <see cref="DisplayText"/>.
    /// </summary>
    /// <remarks>
    /// The visible row binds <see cref="ExpansionGlyph"/> instead. Keeping the two apart is what
    /// stops a private-use-area codepoint leaking into <c>WorkspaceTreeText</c>, which the UAT
    /// suite asserts against.
    /// </remarks>
    public string ExpansionText => CanExpand ? (IsExpanded ? "v" : ">") : string.Empty;

    /// <summary>
    /// The toggle button's accessible name. It states the node's expansion rather than the action,
    /// because that name is the only route a UI Automation test has to read it: the row itself is a
    /// layout, which publishes no peer and no ExpandCollapse pattern.
    /// </summary>
    public string ExpansionDescription => CanExpand
        ? (IsExpanded ? "Expanded" : "Collapsed")
        : "Leaf";

    /// <summary>The chevron the row's toggle button shows. Never reaches <see cref="DisplayText"/>.</summary>
    public string ExpansionGlyph => CanExpand
        ? (IsExpanded ? PresenterIcons.ChevronDown : PresenterIcons.ChevronRight)
        : string.Empty;

    public bool IsRunnable => Kind is BddWorkspaceNodeKind.Folder
        or BddWorkspaceNodeKind.MarkdownFile
        or BddWorkspaceNodeKind.Suite
        or BddWorkspaceNodeKind.Scenario
        or BddWorkspaceNodeKind.Step;

    public string Icon => Kind switch
    {
        BddWorkspaceNodeKind.Folder => AggregateStatusIcon() ?? "[F]",
        BddWorkspaceNodeKind.MarkdownFile => AggregateStatusIcon() ?? "[MD]",
        BddWorkspaceNodeKind.WorkflowConfig => "[C]",
        BddWorkspaceNodeKind.File => "[ ]",
        BddWorkspaceNodeKind.Suite => AggregateStatusIcon() ?? "[S]",
        BddWorkspaceNodeKind.Scenario => Scenario?.StatusIcon ?? BddStatusPresentation.Icon("wait"),
        BddWorkspaceNodeKind.Step => Step?.StatusIcon ?? BddStatusPresentation.Icon("wait"),
        _ => "[ ]"
    };

    /// <summary>
    /// The glyph the row's icon label shows. The <see cref="Icon"/> above stays ASCII for
    /// <see cref="DisplayText"/>; this is the same meaning drawn in the Segoe icon font.
    /// </summary>
    public string IconGlyph => Kind switch
    {
        BddWorkspaceNodeKind.Folder => AggregateStatusGlyph() ?? PresenterIcons.KindFolder,
        BddWorkspaceNodeKind.MarkdownFile => AggregateStatusGlyph() ?? PresenterIcons.KindMarkdown,
        BddWorkspaceNodeKind.WorkflowConfig => PresenterIcons.KindConfig,
        BddWorkspaceNodeKind.File => PresenterIcons.KindFile,
        BddWorkspaceNodeKind.Suite => AggregateStatusGlyph() ?? PresenterIcons.KindSuite,
        BddWorkspaceNodeKind.Scenario => BddStatusPresentation.Glyph(Scenario?.Status ?? "wait"),
        BddWorkspaceNodeKind.Step => BddStatusPresentation.Glyph(Step?.Status ?? "wait"),
        _ => PresenterIcons.KindFile
    };

    public string StatusDescription => Kind switch
    {
        BddWorkspaceNodeKind.Scenario => Scenario?.StatusDescription ?? BddStatusPresentation.Description("wait"),
        BddWorkspaceNodeKind.Step => Step?.StatusDescription ?? BddStatusPresentation.Description("wait"),
        BddWorkspaceNodeKind.MarkdownFile => "Feature file",
        BddWorkspaceNodeKind.WorkflowConfig => "Workflow config",
        BddWorkspaceNodeKind.Suite => "Suite",
        BddWorkspaceNodeKind.Folder => "Folder",
        _ => "File"
    };

    /// <summary>
    /// Resolved against the effective theme every time it is read, so the shell only has
    /// to re-raise <see cref="INotifyPropertyChanged.PropertyChanged" /> when the theme
    /// flips - see <see cref="RefreshThemeColors" />.
    /// </summary>
    public Color IconColor
    {
        get
        {
            var theme = BddStatusPresentation.CurrentTheme;
            return Kind switch
            {
                BddWorkspaceNodeKind.Scenario => BddStatusPresentation.Color(Scenario?.Status ?? "wait", theme),
                BddWorkspaceNodeKind.Step => BddStatusPresentation.Color(Step?.Status ?? "wait", theme),
                _ when AggregateStatus() is { } status => BddStatusPresentation.Color(status, theme),
                _ => BddStatusPresentation.KindColor(Kind, theme)
            };
        }
    }

    /// <summary>
    /// Re-reads <see cref="IconColor" /> against the current theme. The shell calls this
    /// on every node when the effective theme changes.
    /// </summary>
    public void RefreshThemeColors()
    {
        OnPropertyChanged(nameof(IconColor));
    }

    public string DisplayText => $"{new string(' ', Depth * 2)}{ExpansionText.PadRight(1)} {Icon} {Name}";

    public void AddChild(BddWorkspaceNodeViewModel child)
    {
        child.Parent = this;
        child.PropertyChanged += OnChildStatusChanged;
        Children.Add(child);
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(ExpansionText));
        OnPropertyChanged(nameof(ExpansionGlyph));
        OnPropertyChanged(nameof(ExpansionDescription));
        OnPropertyChanged(nameof(DisplayText));
    }

    private void OnChildStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BddScenarioViewModel.Status)
            or nameof(BddScenarioViewModel.StatusIcon)
            or nameof(BddStepViewModel.Status)
            or nameof(BddStepViewModel.StatusIcon)
            or nameof(Icon)
            or nameof(IconGlyph)
            or nameof(IconColor)
            or nameof(DisplayText))
        {
            OnPropertyChanged(nameof(Icon));
            OnPropertyChanged(nameof(IconGlyph));
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    private void ToggleExpansion()
    {
        if (CanExpand)
        {
            IsExpanded = !IsExpanded;
        }
    }

    private string? AggregateStatusIcon()
    {
        var status = AggregateStatus();
        return status is null ? null : BddStatusPresentation.Icon(status);
    }

    private string? AggregateStatusGlyph()
    {
        var status = AggregateStatus();
        return status is null ? null : BddStatusPresentation.Glyph(status);
    }

    private string? AggregateStatus()
    {
        if (Children.Count == 0)
        {
            return null;
        }

        var statuses = EnumerateStatusNodes(this).ToArray();
        if (statuses.Length == 0)
        {
            return null;
        }

        if (statuses.Any(status => status == "fail"))
        {
            return "fail";
        }

        if (statuses.Any(status => status == "run"))
        {
            return "run";
        }

        if (statuses.Any(status => status == "cancel"))
        {
            return "cancel";
        }

        return statuses.All(status => status == "pass") ? "pass" : null;
    }

    private static IEnumerable<string> EnumerateStatusNodes(BddWorkspaceNodeViewModel node)
    {
        foreach (var child in node.Children)
        {
            if (child.Kind == BddWorkspaceNodeKind.Scenario && child.Scenario is not null)
            {
                yield return child.Scenario.Status;
            }

            if (child.Kind == BddWorkspaceNodeKind.Step && child.Step is not null)
            {
                yield return child.Step.Status;
            }

            foreach (var status in EnumerateStatusNodes(child))
            {
                yield return status;
            }
        }
    }

    private static string SanitizeAutomationId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString().Trim('_');
    }
}
