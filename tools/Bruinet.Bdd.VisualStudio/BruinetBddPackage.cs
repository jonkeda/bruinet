using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Bruinet.Bdd.VisualStudio;

/// <summary>
/// The extension's package: the Scenario Preview tool window and its commands. It loads in the
/// background when a feature or step file is selected or opened, and starts the language server
/// the first time one becomes the active document.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(PackageIds.Package)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(ScenarioPreviewWindow), Style = VsDockStyle.Tabbed, Window = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}", Orientation = ToolWindowOrientation.Right)]
[ProvideToolWindow(typeof(BddExplorerWindow), Style = VsDockStyle.Tabbed, Window = "{3AE79031-E1BC-11D0-8F78-00A0C9110057}")]
[ProvideAutoLoad(PackageIds.FeatureFileContext, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideOptionPage(typeof(BddOptionsPage), "Bruinet BDD", "General", 0, 0, true)]
// Loaded when debugging starts too, so a run debugged from Test Explorer can be stepped through.
[ProvideAutoLoad(Microsoft.VisualStudio.VSConstants.UICONTEXT.Debugging_string, PackageAutoLoadFlags.BackgroundLoad)]
[ProvideUIContextRule(
    PackageIds.FeatureFileContext,
    name: "Bruinet feature, step or video file",
    expression: "Feature | Steps | Video",
    termNames: ["Feature", "Steps", "Video"],
    termValues: [@"HierSingleSelectionName:\.feature\.md$", @"HierSingleSelectionName:\.steps\.md$", @"HierSingleSelectionName:\.video\.md$"])]
public sealed class BruinetBddPackage : AsyncPackage
{
    private PreviewController? _controller;

    /// <summary>The package once loaded, for CodeLens's callback, which runs outside it.</summary>
    internal static BruinetBddPackage? Instance { get; private set; }

    /// <summary>Tools, Options, Bruinet BDD, Recording.</summary>
    internal BddOptionsPage Options => (BddOptionsPage)GetDialogPage(typeof(BddOptionsPage));

    /// <summary>The preview's controller, which holds the language server.</summary>
    internal PreviewController? Controller => _controller;

    /// <summary>
    /// The package, loaded if it is not yet: an editor's preview margin can be created before the
    /// package's background load has run.
    /// </summary>
    internal static async Task<BruinetBddPackage?> LoadAsync()
    {
        if (Instance is { } loaded)
        {
            return loaded;
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsShell)) is IVsShell7 shell)
        {
            var guid = new Guid(PackageIds.Package);
            await shell.LoadPackageAsync(ref guid);
        }

        return Instance;
    }

    /// <inheritdoc />
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        _controller = new PreviewController(this);
        await _controller.StartAsync(cancellationToken);
        Instance = this;

        if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService commands)
        {
            commands.AddCommand(new MenuCommand((_, _) => ShowPreview(), new CommandID(PackageIds.CommandSet, PackageIds.ShowScenarioPreview)));
            var open = new OleMenuCommand((_, _) => _controller.TogglePreviewMargin(), new CommandID(PackageIds.CommandSet, PackageIds.OpenScenarioPreview));
            open.BeforeQueryStatus += (_, _) => open.Visible = _controller.ActiveIsFeatureOrSteps;
            commands.AddCommand(open);

            commands.AddCommand(new MenuCommand((_, _) => ShowExplorer(), new CommandID(PackageIds.CommandSet, PackageIds.ShowBddExplorer)));

            var toggle = new OleMenuCommand((_, _) => ToggleStepBreakpoint(), new CommandID(PackageIds.CommandSet, PackageIds.ToggleStepBreakpoint));
            toggle.BeforeQueryStatus += (_, _) => toggle.Visible = toggle.Enabled = _controller.ActiveIsFeatureOrSteps;
            commands.AddCommand(toggle);

            // Stepping through a scenario while a debugged run is paused at a step (Debug menu, and the preview's bar).
            AddStep(commands, PackageIds.StepOverBdd, "over", () => _controller.IsPaused);
            AddStep(commands, PackageIds.StepIntoBdd, "into", () => _controller.PausedAtCall);
            AddStep(commands, PackageIds.StepOutBdd, "out", () => _controller.PausedInCall);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (disposing)
        {
            _controller?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AddStep(OleMenuCommandService commands, int id, string move, Func<bool> enabled)
    {
        var command = new OleMenuCommand(
            (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _controller?.Move(move);
            },
            new CommandID(PackageIds.CommandSet, id));
        command.BeforeQueryStatus += (_, _) => command.Enabled = enabled();
        commands.AddCommand(command);
    }

    private void ToggleStepBreakpoint()
        => JoinableTaskFactory.RunAsync(() => _controller!.ToggleBreakpointAtCaretAsync()).FileAndForget("Bruinet/ToggleStepBreakpoint");

    private void ShowExplorer() => Show(typeof(BddExplorerWindow), "Bruinet/ShowBddExplorer");

    private void ShowPreview() => Show(typeof(ScenarioPreviewWindow), "Bruinet/ShowScenarioPreview");

    private void Show(Type window, string context)
        => JoinableTaskFactory.RunAsync(async () =>
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            if (await ShowToolWindowAsync(window, 0, create: true, DisposalToken) is ToolWindowPane pane)
            {
                await AttachAsync(pane);
            }
        }).FileAndForget(context);

    /// <summary>
    /// Fills a tool window from the language server. A command calls it after showing the window, and the
    /// window itself when Visual Studio restores it at start-up from the last session's layout.
    /// </summary>
    internal void Attach(ToolWindowPane pane)
        => JoinableTaskFactory.RunAsync(() => AttachAsync(pane)).FileAndForget("Bruinet/AttachToolWindow");

    private async Task AttachAsync(ToolWindowPane pane)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
        if (_controller is null)
        {
            return;
        }

        switch (pane)
        {
            case BddExplorerWindow explorer:
                await _controller.AttachExplorerAsync(explorer.Control);
                break;
            case ScenarioPreviewWindow preview:
                await _controller.AttachAsync(preview.Control);
                break;
        }
    }
}
