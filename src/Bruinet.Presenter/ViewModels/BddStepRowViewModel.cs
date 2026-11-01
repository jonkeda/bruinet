using System.ComponentModel;
using System.Windows.Input;
using Bruinet.Presenter.Commands;
using Microsoft.Maui.Graphics;

namespace Bruinet.Presenter.ViewModels;

/// <summary>
/// One row of the step list: a step, or a <c>## Step:</c> call over the steps it runs. A call is a
/// place of its own to pause at, as in the engine's stepping (AD-022): Step runs it whole, Into
/// goes inside without running anything.
/// </summary>
public sealed class BddStepRowViewModel : ViewModelBase
{
    private readonly IReadOnlyList<BddStepViewModel> _steps;
    private bool _isCurrent;
    private bool _hasBreakpoint;

    private BddStepRowViewModel(int index, IReadOnlyList<BddStepViewModel> steps, int firstStep, bool isCall, string text)
    {
        Index = index;
        _steps = steps;
        FirstStep = firstStep;
        IsCall = isCall;
        Text = text;
        foreach (var step in steps)
        {
            step.PropertyChanged += OnStepChanged;
        }

        RunUntilCommand = new RelayCommand(() => Host?.RunUntil(this), () => Host?.CanRunUntil(this) == true);
        RunFromCommand = new RelayCommand(() => Host?.RunFrom(this), () => Host?.CanRunFrom(this) == true);
        DebugFromCommand = new RelayCommand(() => Host?.DebugFrom(this), () => Host?.CanDebugFrom(this) == true);
        ToggleBreakpointCommand = new RelayCommand(() => Host?.ToggleBreakpoint(this), () => CanBreak);
    }

    /// <summary>The step list the row is in, for its menu; set when the list shows it.</summary>
    public IStepRowHost? Host { get; set; }

    /// <summary>Runs from where the run is (or the start) and pauses before this row.</summary>
    public ICommand RunUntilCommand { get; }

    /// <summary>Skips the rows before this one and runs on from it; only while a run is paused before it.</summary>
    public ICommand RunFromCommand { get; }

    /// <summary>Debug from here: a debugger on the runner, which breaks in this row's step definition; only while paused before it.</summary>
    public ICommand DebugFromCommand { get; }

    /// <summary>Sets or clears the breakpoint on this row's line.</summary>
    public ICommand ToggleBreakpointCommand { get; }

    /// <summary>The menu item's text, which says why Run from here is off.</summary>
    public string RunFromText => Host?.CanRunFrom(this) == true ? "Run from here" : "Run from here (pause a run before this step)";

    /// <summary>Re-reads what the menu offers, after the run moved.</summary>
    public void RefreshMenu()
    {
        ((RelayCommand)RunUntilCommand).NotifyCanExecuteChanged();
        ((RelayCommand)RunFromCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DebugFromCommand).NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(RunFromText));
    }

    /// <summary>A step's row.</summary>
    /// <param name="index">The row's position in the list.</param>
    /// <param name="step">The step.</param>
    /// <param name="stepIndex">The step's position in the scenario's run.</param>
    public static BddStepRowViewModel ForStep(int index, BddStepViewModel step, int stepIndex)
        => new(index, [step], stepIndex, isCall: false, step.Text);

    /// <summary>A call's row, over the steps it runs.</summary>
    /// <param name="index">The row's position in the list.</param>
    /// <param name="callText">The call as written in the scenario.</param>
    /// <param name="steps">The steps it runs.</param>
    /// <param name="firstStep">The first of them, in the scenario's run.</param>
    public static BddStepRowViewModel ForCall(int index, string callText, IReadOnlyList<BddStepViewModel> steps, int firstStep)
        => new(index, steps, firstStep, isCall: true, callText);

    /// <summary>The row's position in the list.</summary>
    public int Index { get; }

    /// <summary>The first step the row runs, as its position in the scenario's run.</summary>
    public int FirstStep { get; }

    /// <summary>The position after the last step the row runs.</summary>
    public int EndStep => FirstStep + _steps.Count;

    /// <summary>Whether this is a <c>## Step:</c> call's row.</summary>
    public bool IsCall { get; }

    /// <summary>Whether this is a step inside a call.</summary>
    public bool IsInCall => !IsCall && _steps[0].CallText is not null;

    /// <summary>The step's or the call's text, in full: the tooltip.</summary>
    public string Text { get; }

    /// <summary>The text without the indent it carries under a call.</summary>
    public string Label => Text.TrimStart();

    /// <summary>A step inside a call is indented under it.</summary>
    public Thickness Indent => new(IsInCall ? 16 + 8 * ((Text.Length - Label.Length) / 2 - 1) : 0, 0, 0, 0);

    /// <summary>The line of the step, or of the call, in the scenario file.</summary>
    public int LineNumber => _steps[0].LineNumber;

    /// <summary>Whether a breakpoint can be set here: a line of the scenario file itself, not of a step file.</summary>
    public bool CanBreak => !IsInCall;

    public string AutomationId => $"StepRow_{Index}";

    /// <summary>A call's status is its steps': failed if one failed, passed when all passed.</summary>
    public string Status
    {
        get
        {
            if (!IsCall)
            {
                return _steps[0].Status;
            }

            var statuses = _steps.Select(step => step.Status).ToArray();
            return statuses.Contains("fail") ? "fail"
                : statuses.Contains("run") ? "run"
                : statuses.Contains("cancel") ? "cancel"
                : statuses.All(status => status == "pass") ? "pass"
                : statuses.All(status => status == "skip") ? "skip"
                : statuses.Any(status => status == "pass") ? "run"
                : "wait";
        }
    }

    public string StatusGlyph => BddStatusPresentation.Glyph(Status);

    public string StatusDescription => BddStatusPresentation.Description(Status);

    public Color StatusColor => BddStatusPresentation.Color(Status);

    /// <summary>What a failed step said; null otherwise.</summary>
    public string? Message => _steps.Select(step => step.Message).FirstOrDefault(message => message is not null);

    /// <summary>The full text and, after a failure, why: the row's tooltip.</summary>
    public string Tooltip => Message is { } message ? $"{Label}{Environment.NewLine}{message}" : Label;

    /// <summary>Whether the run is paused here: the next thing it does is this row.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    /// <summary>Whether a step breakpoint is set on this row's line.</summary>
    public bool HasBreakpoint
    {
        get => _hasBreakpoint;
        set
        {
            if (SetProperty(ref _hasBreakpoint, value))
            {
                OnPropertyChanged(nameof(BreakpointGlyph));
            }
        }
    }

    /// <summary>A dot when a breakpoint is set.</summary>
    public string BreakpointGlyph => HasBreakpoint ? "●" : string.Empty;

    /// <summary>The ASCII line for the step list's text mirror.</summary>
    public string DisplayText => $"{(IsCurrent ? ">" : " ")} {BddStatusPresentation.Icon(Status)}  {Text}";

    /// <summary>Re-reads <see cref="StatusColor"/> against the current theme.</summary>
    public void RefreshThemeColors() => OnPropertyChanged(nameof(StatusColor));

    private void OnStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BddStepViewModel.Status) or nameof(BddStepViewModel.Message))
        {
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusGlyph));
            OnPropertyChanged(nameof(StatusDescription));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(Tooltip));
        }
    }
}

/// <summary>What a step row's menu asks of the step list.</summary>
public interface IStepRowHost
{
    /// <summary>Whether the run can go on to the row and pause before it.</summary>
    bool CanRunUntil(BddStepRowViewModel row);

    /// <summary>Whether the run can skip to the row and run on from it.</summary>
    bool CanRunFrom(BddStepRowViewModel row);

    /// <summary>Runs on to the row and pauses before it.</summary>
    void RunUntil(BddStepRowViewModel row);

    /// <summary>Skips to the row and runs on from it.</summary>
    void RunFrom(BddStepRowViewModel row);

    /// <summary>Sets or clears the row's breakpoint.</summary>
    void ToggleBreakpoint(BddStepRowViewModel row);

    /// <summary>Whether the run is paused before the row, so a debugger can be put on its runner.</summary>
    bool CanDebugFrom(BddStepRowViewModel row) => false;

    /// <summary>Debug from here (AD-022): asks Windows for a debugger on the runner, then runs the row's step, which breaks in its definition.</summary>
    void DebugFrom(BddStepRowViewModel row)
    {
    }
}
