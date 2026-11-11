using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;

namespace Bruinet.Bdd.VisualStudio;

/// <summary>
/// Joins the editor to the preview: follows the active feature or step file, sends its text to the
/// server (on open, and after a pause in typing), refreshes the preview then and after each build,
/// keeps the caret and the preview in step both ways, and passes Visual Studio's theme on. Keeps
/// the step breakpoints: the preview's gutter and the editor command toggle them, and both show them.
/// </summary>
internal sealed class PreviewController : IVsSelectionEvents, IVsUpdateSolutionEvents, IVsSolutionEvents, IDisposable
{
    // How long typing must pause before the preview is rendered again.
    private static readonly TimeSpan TypingPause = TimeSpan.FromMilliseconds(300);

    // A step in the preview's HTML: the lines a step breakpoint can go on.
    private static readonly Regex StepLine = new("<li class=\"uat-step(?! uat-not-a-step)[^\"]*\" data-line=\"(?<line>\\d+)\"", RegexOptions.CultureInvariant);

    private readonly AsyncPackage _package;
    private readonly Dictionary<string, string> _suites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _opened = new(StringComparer.OrdinalIgnoreCase);
    private BddServerClient? _server;
    private BddRunController? _runs;
    private IVsOutputWindowPane? _output;
    private IVsMonitorSelection? _selection;
    private IVsSolutionBuildManager? _builds;
    private IVsEditorAdaptersFactoryService? _adapters;
    private IVsSolution? _solution;
    private uint _selectionCookie;
    private uint _buildCookie;
    private uint _solutionCookie;
    // The previews on show: the tool window's follows the active file (no path); an editor's
    // margin shows its own file.
    private readonly List<(ScenarioPreviewControl Control, string? Path)> _views = [];
    private IWpfTextView? _textView;
    private string? _path;
    private int _version;
    private CancellationTokenSource? _typing;
    private ScenarioPreviewControl? _explorer;
    private FileSystemWatcher? _pausedWatcher;

    // Held so their handlers stay subscribed (DTE events are dropped with their object).
    private EnvDTE.DebuggerEvents? _debuggerEvents;
    private readonly List<(EnvDTE.CommandEvents Events, EnvDTE._dispCommandEvents_BeforeExecuteEventHandler Handler)> _keys = [];
    private JObject? _paused;

    // A Step-through run paused (bruinet/step/paused), in the shape of the engine's paused file;
    // null while it moves and when none is going.
    private JObject? _stepping;
    private string _explorerView = "steps";
    private string? _explorerSuite;

    public PreviewController(AsyncPackage package) => _package = package;

    /// <summary>Whether the active document is a feature or step file.</summary>
    public bool ActiveIsFeatureOrSteps => _path is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _output = await CreateOutputPaneAsync();
        _server = new BddServerClient(BddServerClient.DefaultServerPath, Log)
        {
            RunnerSettings = () => (((BruinetBddPackage)_package).Options.WarmRunner, ((BruinetBddPackage)_package).Options.WarmRunnerIdleMinutes),
        };
        _server.ResultsChanged += OnResultsChanged;
        _server.StepPaused += OnStepPaused;
        _server.StepEnded += OnStepEnded;
        _server.RunEnded += (_, ended) => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            _runs?.RunEnded(ended);
        }).FileAndForget("Bruinet/RunEnded");
        _runs = new BddRunController(_package, _server, Log, Post);

        _selection = await _package.GetServiceAsync(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
        Assumes.Present(_selection);
        ErrorHandler.ThrowOnFailure(_selection.AdviseSelectionEvents(this, out _selectionCookie));

        _builds = await _package.GetServiceAsync(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager;
        Assumes.Present(_builds);
        ErrorHandler.ThrowOnFailure(_builds.AdviseUpdateSolutionEvents(this, out _buildCookie));

        _solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
        Assumes.Present(_solution);
        ErrorHandler.ThrowOnFailure(_solution.AdviseSolutionEvents(this, out _solutionCookie));

        var components = await _package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
        Assumes.Present(components);
        _adapters = components.GetService<IVsEditorAdaptersFactoryService>();

        VSColorTheme.ThemeChanged += OnThemeChanged;
        BddBreakpointStore.Instance.Changed += OnBreakpointsChanged;
        WatchPaused();
        await WatchDebuggerAsync();

        ErrorHandler.ThrowOnFailure(_selection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_DocumentFrame, out var frame));
        await TrackAsync(frame, cancellationToken);
    }

    /// <summary>
    /// Shows the preview in a control: loads the host page once, then the file. The tool window's
    /// control follows the active file; an editor margin's shows its own.
    /// </summary>
    /// <param name="view">The control.</param>
    /// <param name="path">The margin's file; null for the tool window.</param>
    public async Task AttachAsync(ScenarioPreviewControl view, string? path = null)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (!_views.Any(entry => entry.Control == view))
        {
            _views.Add((view, path));
            view.MessageReceived += OnMessage;
        }

        if (!view.IsReady)
        {
            await EnsureServerAsync(path ?? _path);
            var host = await _server!.PreviewHostAsync(_package.DisposalToken);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            await view.InitializeAsync(host);
            view.Post(VsTheme.Message());
        }

        // A margin's file renders once it is the active one (TrackAsync opens it on the server).
        if (path is null || string.Equals(path, _path, StringComparison.OrdinalIgnoreCase))
        {
            await RefreshAsync();
        }
    }

    /// <summary>Forgets a control: its editor closed.</summary>
    public void Release(ScenarioPreviewControl view)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        view.MessageReceived -= OnMessage;
        _views.RemoveAll(entry => entry.Control == view);
    }

    /// <summary>The editor command: shows or hides the preview beside the active feature or step file.</summary>
    public void TogglePreviewMargin()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_textView is { } view)
        {
            view.Options.SetOptionValue(BddPreviewOptions.ShowPreview, !view.Options.GetOptionValue(BddPreviewOptions.ShowPreview));
        }
    }

    // The controls that show a file: the tool window's, and the margins of that file.
    private IEnumerable<ScenarioPreviewControl> ViewsOf(string? path)
        => _views.Where(entry => entry.Path is null || string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Control).ToList();

    private void PostTo(string? path, object message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var view in ViewsOf(path))
        {
            view.Post(message);
        }
    }

    public void Dispose()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        VSColorTheme.ThemeChanged -= OnThemeChanged;
        BddBreakpointStore.Instance.Changed -= OnBreakpointsChanged;
        if (_selectionCookie != 0)
        {
            _selection?.UnadviseSelectionEvents(_selectionCookie);
        }

        if (_buildCookie != 0)
        {
            _builds?.UnadviseUpdateSolutionEvents(_buildCookie);
        }

        if (_solutionCookie != 0)
        {
            _solution?.UnadviseSolutionEvents(_solutionCookie);
        }

        Detach();
        _pausedWatcher?.Dispose();
        if (_debuggerEvents is not null)
        {
            _debuggerEvents.OnEnterBreakMode -= OnEnterBreakMode;
        }

        foreach (var (events, handler) in _keys)
        {
            events.BeforeExecute -= handler;
        }

        _typing?.Cancel();
        _server?.Dispose();
    }

    /// <summary>Whether a run is paused at a step: a Step-through run, or a debugged one (the engine's <c>bdd-paused.json</c>).</summary>
    public bool IsPaused => Current is not null;

    /// <summary>Paused at a <c>## Step:</c> call: Step into goes inside.</summary>
    public bool PausedAtCall => Current?["isCall"]?.Value<bool>() == true;

    /// <summary>Paused inside a call: Step out goes after it.</summary>
    public bool PausedInCall => (Current?["depth"]?.Value<int>() ?? 0) > 0;

    private JObject? Current => _stepping ?? _paused;

    /// <summary>
    /// A move from the paused step. A Step-through run's go to the server. A debugged run's
    /// <c>over</c>, <c>into</c> and <c>out</c> are written for the engine and the debugger goes on;
    /// <c>continue</c> goes on to the next breakpoint; <c>stop</c> ends the debugging.
    /// </summary>
    public void Move(string move)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_stepping is not null)
        {
            StepMove(move);
            return;
        }

        if (_paused is not { } paused)
        {
            return;
        }

        if (move is "over" or "into" or "out")
        {
            BddBreakpointStore.Instance.SetMove(move, paused["depth"]?.Value<int>() ?? 0, paused["process"]?.Value<int>() ?? 0);
        }
        else
        {
            BddBreakpointStore.Instance.ClearMove();
        }

        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            if (await _package.GetServiceAsync(typeof(EnvDTE.DTE)) is not EnvDTE80.DTE2 dte || dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode)
            {
                Log("No debugger is paused, so there is nothing to step.");
                return;
            }

            if (move == "stop")
            {
                dte.Debugger.Stop(WaitForDesignMode: false);
            }
            else
            {
                dte.Debugger.Go(WaitForBreakOrEnd: false);
            }
        }).FileAndForget("Bruinet/DebugMove");
    }

    // The engine writes where it is paused, and deletes the file when the run goes on.
    private void WatchPaused()
    {
        var path = BddBreakpointStore.Instance.PausedPath;
        var folder = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(folder);
        _pausedWatcher = new FileSystemWatcher(folder, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
        FileSystemEventHandler changed = (_, _) => _package.JoinableTaskFactory.RunAsync(OnPausedChangedAsync).FileAndForget("Bruinet/Paused");
        _pausedWatcher.Created += changed;
        _pausedWatcher.Changed += changed;
        _pausedWatcher.Deleted += changed;
        _pausedWatcher.Renamed += (_, _) => _package.JoinableTaskFactory.RunAsync(OnPausedChangedAsync).FileAndForget("Bruinet/Paused");
        _pausedWatcher.EnableRaisingEvents = true;
        _package.JoinableTaskFactory.RunAsync(OnPausedChangedAsync).FileAndForget("Bruinet/Paused");
    }

    // Paused: the step's source file in the editor at its line, and the bar in every preview. Gone
    // on: the bar goes.
    private async Task OnPausedChangedAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        _paused = ReadPaused(BddBreakpointStore.Instance.PausedPath);
        if (_stepping is null)
        {
            ShowPaused(_paused);
        }
    }

    // The bar in every preview, and the step's source file at its line; for null, the bar goes.
    private void ShowPaused(JObject? paused)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (paused is null || (string?)paused["file"] is not { } running)
        {
            foreach (var (control, _) in _views.ToList())
            {
                control.Post(new { type = "paused" });
            }

            return;
        }

        var file = SourceOf(running);
        var line = paused["line"]?.Value<int>() ?? 1;
        ShowPausedStep();

        foreach (var (control, path) in _views.ToList())
        {
            control.Post(new
            {
                type = "paused",
                text = (string?)paused["text"],
                line,
                isCall = paused["isCall"]?.Value<bool>() == true,
                depth = paused["depth"]?.Value<int>() ?? 0,
                calls = paused["calls"]?.ToObject<string[]>() ?? [],
                failed = paused["failed"]?.Value<bool>() == true,
                message = (string?)paused["message"],
                arguments = paused["arguments"] as JObject,
                app = (string?)paused["app"],
                page = (string?)paused["page"],
                stepping = paused["stepping"]?.Value<bool>() == true,
                here = string.Equals(path ?? file, file, StringComparison.OrdinalIgnoreCase),
            });
        }
    }

    // The paused step in its feature file, at its line. Visual Studio shows the C# line it stopped
    // at (the engine's Debugger.Break) when it enters break mode, so this runs again after that.
    private void ShowPausedStep()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (Current is not { } paused || (string?)paused["file"] is not { } running)
        {
            return;
        }

        var file = SourceOf(running);
        var line = paused["line"]?.Value<int>() ?? 1;
        if (File.Exists(file))
        {
            VsShellUtilities.OpenDocument(_package, file, Guid.Empty, out _, out _, out _, out var textView);
            if (textView is not null && line >= 1)
            {
                textView.SetCaretPos(line - 1, 0);
                textView.CenterLines(line - 1, 1);
            }
        }
    }

    // Break mode at a scenario step: the feature file comes back in front once Visual Studio has
    // shown its own location. F10 there is the BDD Step over (F11 still goes into the step's C#).
    private async Task WatchDebuggerAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (await _package.GetServiceAsync(typeof(EnvDTE.DTE)) is not EnvDTE80.DTE2 dte)
        {
            return;
        }

        _debuggerEvents = dte.Events.DebuggerEvents;
        _debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;

        // D3: while a Step-through run is paused and no debugger is in break mode, the debugger's keys
        // are its moves. A key whose command Visual Studio disables when nothing is debugged (Shift+F11,
        // Shift+F5) never comes here: the bar's buttons and Ctrl+Shift+F11 stand in.
        Intercept(dte, VSConstants.VSStd97CmdID.StepOver, "over", () => IsPausedHere() || SteppingHere());
        Intercept(dte, VSConstants.VSStd97CmdID.StepInto, "into", SteppingHere);
        Intercept(dte, VSConstants.VSStd97CmdID.StepOut, "out", SteppingHere);
        Intercept(dte, VSConstants.VSStd97CmdID.Start, "continue", SteppingHere);
        Intercept(dte, VSConstants.VSStd97CmdID.Stop, "stop", SteppingHere);
    }

    private void Intercept(EnvDTE80.DTE2 dte, VSConstants.VSStd97CmdID command, string move, Func<bool> ours)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var events = dte.Events.CommandEvents[VSConstants.GUID_VSStandardCommandSet97.ToString("B"), (int)command];
        EnvDTE._dispCommandEvents_BeforeExecuteEventHandler handler = (string guid, int id, object customIn, object customOut, ref bool cancelDefault) =>
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ours())
            {
                cancelDefault = true;
                Move(move);
            }
        };
        events.BeforeExecute += handler;
        _keys.Add((events, handler));
    }

    private bool SteppingHere()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return _stepping is not null &&
               Package.GetGlobalService(typeof(EnvDTE.DTE)) is EnvDTE80.DTE2 dte &&
               dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode;
    }

    // From the server: the Step-through run paused. The bar shows it as a debugged run's pause.
    private void OnStepPaused(object sender, JObject paused)
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            var calls = paused["calls"] as JArray ?? [];
            _stepping = new JObject
            {
                ["file"] = (string?)paused["at"] is { } at ? new Uri(at).LocalPath : null,
                ["line"] = paused["line"],
                ["depth"] = paused["depth"],
                ["isCall"] = paused["isCall"],
                ["text"] = paused["text"],
                ["calls"] = new JArray(calls.Select(call => $"{(string?)call["text"]} at {Path.GetFileName((string?)call["file"] ?? string.Empty)}:{(int?)call["line"]}")),
                ["failed"] = paused["failed"],
                ["message"] = paused["message"],
                ["arguments"] = paused["arguments"],
                ["app"] = paused["app"],
                ["page"] = paused["page"],
                ["stepping"] = true,
            };
            _runs?.StepPaused(paused: true);
            ShowPaused(_stepping);
        }).FileAndForget("Bruinet/StepPaused");

    private void OnStepEnded(object sender, JObject ended)
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            _stepping = null;
            ShowPaused(null);
            _runs?.StepEnded((string?)ended["status"], (string?)ended["message"]);
        }).FileAndForget("Bruinet/StepEnded");

    // D6: the debugger on the runner; once attached, Step over runs the paused step, which breaks in it.
    private void DebugFromHere()
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            if (_runs is null || !await _runs.AttachToRunnerAsync())
            {
                return;
            }

            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            if (_stepping is not null)
            {
                StepMove("over");
            }
        }).FileAndForget("Bruinet/DebugFromHere");

    // D5: the paused Step-through run's app as it is now, taken by the runner and opened in Visual Studio.
    private void Screenshot()
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            var shot = await _server!.StepScreenshotAsync(_package.DisposalToken);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            if ((string?)shot["path"] is { } path && File.Exists(path))
            {
                VsShellUtilities.OpenDocument(_package, path);
            }
            else
            {
                Log($"No screenshot: {(string?)shot["reason"]}");
            }
        }).FileAndForget("Bruinet/StepScreenshot");

    // The bar goes while the run moves; the server says where it pauses next.
    private void StepMove(string move)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _stepping = null;
        _runs?.StepPaused(paused: false);
        ShowPaused(null);
        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            var taken = await _server!.StepMoveAsync(move, _package.DisposalToken);
            if ((bool?)taken["taken"] != true)
            {
                Log($"The Step-through move '{move}' did not go: {(string?)taken["reason"]}");
            }
        }).FileAndForget("Bruinet/StepMove");
    }

    private void OnEnterBreakMode(EnvDTE.dbgEventReason reason, ref EnvDTE.dbgExecutionAction executionAction)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        // Read here, not left to the file watcher: the engine writes the file before it breaks, and
        // the watcher's event can come after break mode, too late to bring the feature file back.
        _paused = ReadPaused(BddBreakpointStore.Instance.PausedPath);
        if (IsPausedHere())
        {
            _package.JoinableTaskFactory.StartOnIdle(ShowPausedStep).FileAndForget("Bruinet/PausedStep");
        }
    }

    // Only a pause the debugger is in now: a test process killed while paused leaves the file
    // behind, and F10 in any later debugging must stay Visual Studio's own.
    private bool IsPausedHere()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_paused?["process"]?.Value<int>() is not { } process ||
            Package.GetGlobalService(typeof(EnvDTE.DTE)) is not EnvDTE80.DTE2 dte ||
            dte.Debugger.CurrentMode != EnvDTE.dbgDebugMode.dbgBreakMode)
        {
            return false;
        }

        foreach (EnvDTE.Process debugged in dte.Debugger.DebuggedProcesses)
        {
            if (debugged.ProcessID == process)
            {
                return true;
            }
        }

        return false;
    }

    private static JObject? ReadPaused(string path)
    {
        try
        {
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            // Being written or removed just now: the watcher tells again when it settles.
            return null;
        }
    }

    // The run's file is the build output's copy (<project>\bin\...\Features\<file>): the source is the
    // project's own Features\<file>. Failing that, the same path under the active file's Features
    // folder, as the engine matches breakpoints; never the copy when a source exists, since an edit
    // there is lost at the next build. At a break the active file is often Visual Studio's C#.
    private string SourceOf(string running)
    {
        const string Features = "\\Features\\";
        const string Bin = "\\bin\\";
        var at = running.LastIndexOf(Features, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return running;
        }

        var relative = running.Substring(at + Features.Length);
        var bin = running.LastIndexOf(Bin, at, StringComparison.OrdinalIgnoreCase);
        if (bin >= 0 && Path.Combine(running.Substring(0, bin), "Features", relative) is var project && File.Exists(project))
        {
            return project;
        }

        var mine = _path?.LastIndexOf(Features, StringComparison.OrdinalIgnoreCase) ?? -1;
        var source = mine < 0 ? null : Path.Combine(_path!.Substring(0, mine + Features.Length), relative);
        return source is not null && File.Exists(source) ? source : running;
    }

    // The active document changed: follow it when it is a feature or step file; otherwise the
    // preview keeps showing the last one.
    private async Task TrackAsync(object? documentFrame, CancellationToken cancellationToken)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (documentFrame is not IVsWindowFrame frame ||
            ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out var document)) ||
            document is not string path ||
            !IsPreviewable(path))
        {
            _path = null;
            return;
        }

        var textView = VsShellUtilities.GetTextView(frame) is { } vsView ? _adapters?.GetWpfTextView(vsView) : null;
        if (textView is null || string.Equals(path, _path, StringComparison.OrdinalIgnoreCase) && textView == _textView)
        {
            _path = textView is null ? null : path;
            return;
        }

        Detach();
        _path = path;
        _textView = textView;
        textView.TextBuffer.Changed += OnTextChanged;
        textView.Caret.PositionChanged += OnCaretMoved;
        textView.Closed += OnViewClosed;

        // The step breakpoints' dots: the Markdown editor hides the glyph margin by default.
        textView.Options.SetOptionValue(DefaultTextViewHostOptions.GlyphMarginName, true);

        await EnsureServerAsync(path);
        _version = Math.Max(_version, 1);
        var text = textView.TextSnapshot.GetText();
        if (_opened.Add(path))
        {
            await _server!.DidOpenAsync(path, _version, text, cancellationToken);
        }
        else
        {
            await _server!.DidChangeAsync(path, ++_version, text, cancellationToken);
        }

        await RefreshAsync();
    }

    /// <summary>The language server, started for the solution: the Scraper view's requests go to it.</summary>
    internal async Task<BddServerClient> ServerAsync()
    {
        await EnsureServerAsync(_path);
        return _server!;
    }

    /// <summary>A line in the extension's Output pane.</summary>
    internal void Note(string line) => Log(line);

    private async Task EnsureServerAsync(string? path)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        var solution = await _package.GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
        string? directory = null;
        if (solution is not null)
        {
            ErrorHandler.ThrowOnFailure(solution.GetSolutionInfo(out directory, out _, out _));
        }

        // A folder opened without a solution: the file's folder, or the nearest one above with a solution file.
        directory = string.IsNullOrEmpty(directory) ? SolutionFolderOf(path) : directory;
        if (await _server!.InitializeAsync(directory!.TrimEnd('\\'), _package.DisposalToken))
        {
            // A new server: it has none of the files the old one had open.
            _opened.Clear();
        }
    }

    // A solution opened: its folder is where the suites are, so the server serves it from now on,
    // and the preview and the explorer show what it finds (they may have asked before it loaded).
    private async Task OnSolutionOpenedAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (_server is null)
        {
            return;
        }

        await EnsureServerAsync(_path);
        if (_path is { } path && _textView is { } view && _opened.Add(path))
        {
            await _server.DidOpenAsync(path, ++_version, view.TextSnapshot.GetText(), _package.DisposalToken);
        }

        await RefreshAsync();
        await RefreshExplorerAsync();
        SolutionOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised on the main thread once a solution opened and the server serves its folder.</summary>
    internal event EventHandler? SolutionOpened;

    private async Task RefreshAsync()
    {
        if (_path is not { } path || _server is null || !ViewsOf(path).Any())
        {
            return;
        }

        try
        {
            var preview = await _server.PreviewAsync(path, _suites.TryGetValue(path, out var suite) ? suite : null, _package.DisposalToken, ((BruinetBddPackage)_package).Options.RecordingsFolder);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            preview["type"] = "update";
            foreach (var target in ViewsOf(path))
            {
                if ((string?)preview["resultsHost"] is { } host && (string?)preview["resultsRoot"] is { } root)
                {
                    target.MapResults(host, root);
                    _resultsRoot = root;
                }

                target.Post(preview);
                target.Post(Breaks(path));
                if (_textView is { } view)
                {
                    target.Post(new { type = "scrollTo", line = view.Caret.Position.BufferPosition.GetContainingLine().LineNumber + 1 });
                }
            }
        }
        catch (Exception exception) when (exception is StreamJsonRpc.RemoteInvocationException or StreamJsonRpc.ConnectionLostException or IOException)
        {
            Log($"The preview of {Path.GetFileName(path)} failed: {exception.Message}");
        }
    }

    private void OnTextChanged(object sender, TextContentChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        _typing?.Cancel();
        var typing = _typing = new CancellationTokenSource();
        var path = _path;
        var snapshot = e.After;
        if (path is not null)
        {
            // Last change first: each one's line is then still where the file had it before.
            foreach (var change in e.Changes.Reverse())
            {
                BddBreakpointStore.Instance.Shift(path, e.Before.GetLineNumberFromPosition(change.OldPosition) + 1, change.LineCountDelta);
            }
        }

        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await Task.Delay(TypingPause, typing.Token);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(typing.Token);
            if (path is null || _server is null || typing.IsCancellationRequested)
            {
                return;
            }

            await _server.DidChangeAsync(path, ++_version, snapshot.TextBuffer.CurrentSnapshot.GetText(), typing.Token);
            await RefreshAsync();
        }).FileAndForget("Bruinet/PreviewTyping");
    }

    private void OnCaretMoved(object sender, CaretPositionChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        PostTo(_path, new { type = "scrollTo", line = e.NewPosition.BufferPosition.GetContainingLine().LineNumber + 1 });
    }

    private void OnViewClosed(object sender, EventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (_path is { } path && _server is not null && _opened.Remove(path))
        {
            _package.JoinableTaskFactory.RunAsync(() => _server.DidCloseAsync(path, _package.DisposalToken)).FileAndForget("Bruinet/PreviewClose");
        }

        Detach();
        _path = null;
    }

    // From the page: a click on a block moves the caret there; a suite picked binds the file in it.
    [SuppressMessage("Maintainability", "CA1502", Justification = "Known debt (AD-033): split when it next changes.")]
    private void OnMessage(object sender, JObject message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        switch ((string?)message["type"])
        {
            case "reveal" when _textView is { } view && (int?)message["line"] is { } line && line >= 1 && line <= view.TextSnapshot.LineCount:
                var start = view.TextSnapshot.GetLineFromLineNumber(line - 1).Start;
                view.Caret.MoveTo(start);
                view.ViewScroller.EnsureSpanVisible(new SnapshotSpan(start, 0), EnsureSpanVisibleOptions.AlwaysCenter);
                break;
            case "run" when _path is { } file && (int?)message["line"] is { } heading:
                Run(file, heading, (bool?)message["debug"] == true, (bool?)message["test"] == true);
                break;
            case "runFile" when _path is { } file:
                Run(file, null, (bool?)message["debug"] == true, (bool?)message["test"] == true);
                break;
            case "record" when _path is { } file:
                Record(file, (int?)message["line"]);
                break;
            case "watch" when (string?)message["path"] is { } video && File.Exists(video) && IsRecording(video):
                // The system's video player: Visual Studio has none.
                using (Process.Start(new ProcessStartInfo(video) { UseShellExecute = true }))
                {
                }

                break;
            case "toggleBreak" when _path is { } file && (int?)message["line"] is { } step:
                BddBreakpointStore.Instance.Toggle(file, step);
                break;
            case "step" when _path is { } file && (int?)message["line"] is { } scenario:
                Step(file, scenario);
                break;
            case "cancel":
                _runs?.Cancel();
                break;
            case "debug" when (string?)message["move"] is { } move:
                Move(move);
                break;
            case "screenshot":
                Screenshot();
                break;
            case "debugHere":
                DebugFromHere();
                break;
            case "deleteResults" when _path is { } file && (string?)message["scope"] is { } scope:
                DeleteResults(scope, new Uri(file).AbsoluteUri, (int?)message["line"], _suites.TryGetValue(file, out var picked) ? picked : null);
                break;
            case "openScreenshot" when (string?)message["path"] is { } screenshot && File.Exists(screenshot):
                VsShellUtilities.OpenDocument(_package, screenshot);
                break;
            case "pickSuite" when _path is { } path && (string?)message["suite"] is { } suite:
                _suites[path] = suite;
                _package.JoinableTaskFactory.RunAsync(RefreshAsync).FileAndForget("Bruinet/PreviewPickSuite");
                break;
        }
    }

    // Old results, for good: the server counts first, the user confirms with the counts, then it deletes.
    private void DeleteResults(string scope, string? uri, int? line, string? suite)
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            if (_server is null)
            {
                return;
            }

            if (_runs is { IsRunning: true })
            {
                Log("A BDD run is going, so no results are deleted now: its test process still writes them.");
                return;
            }

            var count = await _server.DeleteResultsAsync(scope, uri, line, suite, dryRun: true, _package.DisposalToken);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            if ((bool?)count["done"] != true)
            {
                Log($"No results deleted: {(string?)count["reason"]}");
                return;
            }

            var runs = ((int?)count["runsChanged"] ?? 0) + ((int?)count["runsDeleted"] ?? 0);
            var what = (string?)count["what"];
            if (runs == 0)
            {
                VsShellUtilities.ShowMessageBox(_package, $"{what} has no results to delete in {(string?)count["suite"]}.", "Delete results", OLEMSGICON.OLEMSGICON_INFO, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                return;
            }

            var answer = VsShellUtilities.ShowMessageBox(
                _package,
                $"Delete the results of {what} in {runs} run(s) of {(string?)count["suite"]}: {(int?)count["filesDeleted"] ?? 0} file(s)?\n\nThey are deleted for good.",
                "Delete results",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);
            if (answer != (int)VSConstants.MessageBoxResult.IDOK)
            {
                return;
            }

            var done = await _server.DeleteResultsAsync(scope, uri, line, suite, dryRun: false, _package.DisposalToken);
            Log($"Deleted the results of {what}: {(int?)done["filesDeleted"] ?? 0} file(s); {(int?)done["runsDeleted"] ?? 0} run(s) left empty and removed, {(int?)done["runsChanged"] ?? 0} kept with other results.");
            foreach (var skipped in done["skipped"] as JArray ?? [])
            {
                Log($"  Left {(string?)skipped["run"]} alone: {(string?)skipped["reason"]}.");
            }

            await RefreshAsync();
            await RefreshExplorerAsync();
        }).FileAndForget("Bruinet/DeleteResults");

    private void Record(string path, int? line)
    {
        var suite = _suites.TryGetValue(path, out var picked) ? picked : null;
        var options = ((BruinetBddPackage)_package).Options;
        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _runs!.RecordAsync(path, line, suite, options);
            await RefreshAsync();
        }).FileAndForget("Bruinet/PreviewRecord");
    }

    private void Step(string path, int line)
    {
        var suite = _suites.TryGetValue(path, out var picked) ? picked : null;
        _package.JoinableTaskFactory.RunAsync(() => _runs!.StepAsync(path, line, suite)).FileAndForget("Bruinet/PreviewStep");
    }

    // Run is the suite's runner's; Run as test and Debug are dotnet test's (AD-022).
    private void Run(string path, int? line, bool debug, bool test)
    {
        var suite = _suites.TryGetValue(path, out var picked) ? picked : null;
        _package.JoinableTaskFactory.RunAsync(() => debug || test ? _runs!.RunAsync(path, line, suite, debug) : _runs!.RunOnRunnerAsync(path, line, suite)).FileAndForget("Bruinet/PreviewRun");
    }

    /// <summary>
    /// The editor command: toggles the step breakpoint on the caret's line. A line that is not a step,
    /// by the server's parse of the text as it is now, gets none, saying so.
    /// </summary>
    public async Task ToggleBreakpointAtCaretAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (_textView is not { } view || _path is not { } path || _server is null)
        {
            return;
        }

        var line = view.Caret.Position.BufferPosition.GetContainingLine().LineNumber + 1;
        if (!BddBreakpointStore.Instance.LinesOf(path).Contains(line))
        {
            await EnsureServerAsync(path);
            _typing?.Cancel();
            await _server.DidChangeAsync(path, ++_version, view.TextSnapshot.GetText(), _package.DisposalToken);
            var preview = await _server.PreviewAsync(path, _suites.TryGetValue(path, out var suite) ? suite : null, _package.DisposalToken, ((BruinetBddPackage)_package).Options.RecordingsFolder);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            var steps = StepLine.Matches((string?)preview["html"] ?? string.Empty).Cast<Match>()
                .Select(match => int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture));
            if (!steps.Contains(line))
            {
                if (await _package.GetServiceAsync(typeof(SVsStatusbar)) is IVsStatusbar status)
                {
                    status.SetText($"Line {line} is not a step, so it cannot have a BDD step breakpoint.");
                }

                return;
            }
        }

        BddBreakpointStore.Instance.Toggle(path, line);
    }

    /// <summary>Shows the BDD Explorer in a window's control: loads its page once, then the view.</summary>
    public async Task AttachExplorerAsync(ScenarioPreviewControl view)
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (_explorer != view)
        {
            _explorer = view;
            view.MessageReceived += OnExplorerMessage;
        }

        if (!view.IsReady)
        {
            await EnsureServerAsync(_path);
            var host = await _server!.ExplorerHostAsync(_package.DisposalToken);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            await view.InitializeAsync(host);
            view.Post(VsTheme.Message());
        }

        await RefreshExplorerAsync();
    }

    private async Task RefreshExplorerAsync()
    {
        if (_explorer is not { } view || _server is null)
        {
            return;
        }

        try
        {
            var result = await _server.ExplorerAsync(_explorerView, _explorerSuite, _package.DisposalToken);
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            result["type"] = "update";
            view.Post(result);
        }
        catch (Exception exception) when (exception is StreamJsonRpc.RemoteInvocationException or StreamJsonRpc.ConnectionLostException or IOException)
        {
            Log($"The BDD Explorer failed: {exception.Message}");
        }
    }

    // From the explorer's page: a view picked, a suite picked, or an entry to open at its line.
    private void OnExplorerMessage(object sender, JObject message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        switch ((string?)message["type"])
        {
            case "view" when (string?)message["view"] is { } name:
                _explorerView = name;
                _package.JoinableTaskFactory.RunAsync(RefreshExplorerAsync).FileAndForget("Bruinet/ExplorerView");
                break;
            case "deleteResults" when (string?)message["scope"] is { } scope:
                DeleteResults(scope, (string?)message["uri"], null, (string?)message["suite"] ?? _explorerSuite);
                break;
            case "pickSuite" when (string?)message["suite"] is { } suite:
                _explorerSuite = suite;
                _package.JoinableTaskFactory.RunAsync(RefreshExplorerAsync).FileAndForget("Bruinet/ExplorerSuite");
                break;
            case "open" when (string?)message["uri"] is { } uri && Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile && File.Exists(parsed.LocalPath):
                VsShellUtilities.OpenDocument(_package, parsed.LocalPath, Guid.Empty, out _, out _, out _, out var textView);
                if (textView is not null && (int?)message["line"] is { } line && line >= 1)
                {
                    textView.SetCaretPos(line - 1, 0);
                    textView.CenterLines(line - 1, 1);
                }

                break;
        }
    }

    /// <summary>The steps that use a C# member (CodeLens), in the suites of the solution that holds the C# file.</summary>
    public async Task<JObject> UsagesAsync(string csharpFile, string symbol)
    {
        await EnsureServerAsync(csharpFile);
        return await _server!.UsagesAsync(symbol, _package.DisposalToken);
    }

    private static object Breaks(string path) => new { type = "breaks", lines = BddBreakpointStore.Instance.LinesOf(path) };

    // A breakpoint set or cleared, from the preview, the editor or an edit: the preview shows it.
    private void OnBreakpointsChanged(object sender, string path)
    {
        _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);

            // The tool window shows the active file only; a margin of that file shows it always.
            foreach (var entry in _views.Where(entry => string.Equals(entry.Path ?? _path, path, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                entry.Control.Post(Breaks(path));
            }
        }).FileAndForget("Bruinet/PreviewBreaks");
    }

    // A message to the active file's previews, from any thread.
    private void Post(object message)
        => _package.JoinableTaskFactory.RunAsync(async () =>
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
            PostTo(_path, message);
        }).FileAndForget("Bruinet/PreviewPost");

    // A run wrote results for the file on show: draw them.
    private void OnResultsChanged(object sender, string path)
    {
        if (_explorerView == "results")
        {
            _package.JoinableTaskFactory.RunAsync(RefreshExplorerAsync).FileAndForget("Bruinet/ExplorerResults");
        }

        if (string.Equals(path, _path, StringComparison.OrdinalIgnoreCase))
        {
            _package.JoinableTaskFactory.RunAsync(RefreshAsync).FileAndForget("Bruinet/PreviewResults");
        }
    }

    private void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        foreach (var (control, _) in _views.ToList())
        {
            control.Post(VsTheme.Message());
        }

        _explorer?.Post(VsTheme.Message());
    }

    private void Detach()
    {
        if (_textView is { } view)
        {
            view.TextBuffer.Changed -= OnTextChanged;
            view.Caret.PositionChanged -= OnCaretMoved;
            view.Closed -= OnViewClosed;
        }

        _textView = null;
    }

    private async Task<IVsOutputWindowPane?> CreateOutputPaneAsync()
    {
        await _package.JoinableTaskFactory.SwitchToMainThreadAsync(_package.DisposalToken);
        if (await _package.GetServiceAsync(typeof(SVsOutputWindow)) is not IVsOutputWindow output)
        {
            return null;
        }

        var guid = new Guid("5c1b1a9e-6b1f-4f7e-9b1e-9c0b8f4e2a61");
        output.CreatePane(ref guid, "Bruinet BDD", fInitVisible: 1, fClearWithSolution: 0);
        output.GetPane(ref guid, out var pane);
        return pane;
    }

    // OutputStringThreadSafe may be called from any thread; the analyzer cannot know that.
#pragma warning disable VSTHRD010
    private void Log(string line) => _output?.OutputStringThreadSafe(line + Environment.NewLine);
#pragma warning restore VSTHRD010

    // The suite's results root, from the last preview: where its recordings are.
    private string? _resultsRoot;

    // A path the page asks to open with the shell: only a video, and only one of this suite's recordings, so a page
    // that is made to send another path cannot start any file (review 09).
    private bool IsRecording(string path)
    {
        if (!path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var full = Path.GetFullPath(path);
        var configured = ((BruinetBddPackage)_package).Options.RecordingsFolder;
        return new[] { _resultsRoot, string.IsNullOrWhiteSpace(configured) ? null : configured }
            .Where(folder => !string.IsNullOrEmpty(folder))
            .Select(folder => Path.GetFullPath(folder!).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .Any(folder => full.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }

    // A video script previews too, as the scenes it will record; it has no step breakpoints.
    internal static bool IsPreviewable(string path)
        => IsFeatureOrSteps(path) || path.EndsWith(".video.md", StringComparison.OrdinalIgnoreCase);

    internal static bool IsFeatureOrSteps(string path)
        => path.EndsWith(".feature.md", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".steps.md", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".uat.md", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".phrases.md", StringComparison.OrdinalIgnoreCase);

    private static string SolutionFolderOf(string? path)
    {
        var start = path is null ? Environment.CurrentDirectory : Path.GetDirectoryName(path)!;
        for (var folder = new DirectoryInfo(start); folder is not null; folder = folder.Parent)
        {
            if (folder.EnumerateFiles("*.sln").Any() || folder.EnumerateFiles("*.slnx").Any())
            {
                return folder.FullName;
            }
        }

        return start;
    }

    int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        => VSConstants.S_OK;

    int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
    {
        if (elementid == (uint)VSConstants.VSSELELEMID.SEID_DocumentFrame)
        {
            _package.JoinableTaskFactory.RunAsync(() => TrackAsync(varValueNew, _package.DisposalToken)).FileAndForget("Bruinet/PreviewTrack");
        }

        return VSConstants.S_OK;
    }

    int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive) => VSConstants.S_OK;

    // R4: a build during a run would overwrite binaries under it, so it is cancelled, saying why.
    int IVsUpdateSolutionEvents.UpdateSolution_Begin(ref int pfCancelUpdate)
    {
        if (_runs is { IsRunning: true, OwnBuild: false })
        {
            pfCancelUpdate = 1;
            Log("A BDD run is going, so the build is cancelled: it would overwrite the binaries under the run. Cancel the run first.");
        }

        return VSConstants.S_OK;
    }

    // A build finished: the suites load their new output, and the preview binds against it.
    int IVsUpdateSolutionEvents.UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
    {
        _runs?.BuildFinished(fSucceeded != 0 && fCancelCommand == 0);
        if (_server is { } server)
        {
            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await server.BuildDoneAsync(_package.DisposalToken);
                await RefreshAsync();
                await RefreshExplorerAsync();
            }).FileAndForget("Bruinet/PreviewBuildDone");
        }

        return VSConstants.S_OK;
    }

    int IVsUpdateSolutionEvents.UpdateSolution_StartUpdate(ref int pfCancelUpdate) => VSConstants.S_OK;

    int IVsUpdateSolutionEvents.UpdateSolution_Cancel() => VSConstants.S_OK;

    int IVsUpdateSolutionEvents.OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy) => VSConstants.S_OK;

    int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
    {
        _package.JoinableTaskFactory.RunAsync(OnSolutionOpenedAsync).FileAndForget("Bruinet/SolutionOpened");
        return VSConstants.S_OK;
    }

    int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;

    int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;

    int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;

    int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;

    int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

    int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;

    int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;

    int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;

    int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved) => VSConstants.S_OK;
}
