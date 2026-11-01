using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using Bruinet.Core.Artifacts;
using Microsoft.Playwright;

namespace Bruinet.Bdd.VisualStudio.UITests.TestBase;

/// <summary>
/// A Visual Studio with the Bruinet extension, driven from outside: its automation (DTE) over COM,
/// and the Scenario Preview's WebView2 over CDP as a Bruinet.Html context. It runs under a root
/// suffix of its own (<see cref="RootSuffix"/>), so a developer's own instances are untouched, and
/// is started on first use: a skipped test starts nothing.
/// </summary>
// The app host of these tests: the one place that touches COM, processes and the browser.
public sealed class VisualStudioIde : IAsyncLifetime
{
    /// <summary>The Visual Studio instance these tests install into and start.</summary>
    public const string RootSuffix = "BruinetTest";

    private const string ExtensionId = "Bruinet.Bdd.VisualStudio.720464a3-8754-41d5-ac15-22ba895fcd17";

    // Where the build deploys the extension in an instance: Extensions\<publisher>\<name>\<version>.
    private const string DeployFolder = @"\Bruinet Contributors\";

    // dbgDebugMode
    private const int DesignMode = 1;
    private const int BreakMode = 2;

    // Named intervals: Visual Studio's start, a call it rejects while busy, and how often state is read.
    private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan BusyTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BreakpointsLoad = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    private readonly DateTime _created = DateTime.Now;
    private Process? _devenv;
    private dynamic? _dte;
    private Task? _starting;
    private int _port;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>The Bruinet repository root.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>The breakpoints file the IDEs and the engine share (AD-022).</summary>
    public static string BreakpointsPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bruinet", "bdd-breakpoints.json");

    // The developer's own breakpoints while the tests use theirs: a file, so a run that was killed
    // before it could put them back has them put back by the next.
    private static string BreakpointsBackup => BreakpointsPath + ".bruinet-tests";

    /// <summary>The file the engine writes while a run is paused at a step.</summary>
    public static string PausedPath { get; } = Path.Combine(Path.GetDirectoryName(BreakpointsPath)!, "bdd-paused.json");

    /// <summary>Each step of the fixture, timestamped: where a run that hangs is waiting.</summary>
    public static string LogPath { get; } = Path.Combine(Root, "TestResults", "ide-tests.log");

    /// <summary>The solution these tests open.</summary>
    public static string TodoSolution { get; } = Path.Combine(Root, "samples", "Todo", "Bruinet.Samples.Todo.slnx");

    // The solution's own settings, where a developer's C# breakpoints are kept: the tests disable those
    // breakpoints, so a debugged run stops only where a test says, and the file is put back afterwards.
    private static string SolutionOptions { get; } = Path.Combine(Path.GetDirectoryName(TodoSolution)!, ".vs", Path.GetFileName(TodoSolution), "v18", ".suo");

    private static string SolutionOptionsBackup => SolutionOptions + ".bruinet-tests";

    /// <summary>Whether the debugger is in break mode.</summary>
    public bool InBreakMode => Com(() => (int)_dte!.Debugger.CurrentMode) == BreakMode;

    /// <summary>Whether the debugger is not debugging.</summary>
    public bool InDesignMode => Com(() => (int)_dte!.Debugger.CurrentMode) == DesignMode;

    /// <summary>The function the debugger is stopped in, or null when it is not in break mode.</summary>
    public string? CurrentFunction => Com(() => _dte!.Debugger.CurrentStackFrame is { } frame ? (string)frame.FunctionName : null);

    /// <summary>The active document's path, or null.</summary>
    public string? ActiveDocument => Com(() => _dte!.ActiveDocument is { } document ? (string)document.FullName : null);

    /// <summary>The caret's line in the active document, 1-based.</summary>
    public int CaretLine => Com(() => (int)_dte!.ActiveDocument.Selection.CurrentLine);

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Builds and deploys the extension into <see cref="RootSuffix"/> and starts Visual Studio there
    /// with the Todo solution; once. A start that failed fails every test that asks for it.
    /// </summary>
    public Task StartAsync() => _starting ??= StartOnceAsync();

    private async Task StartOnceAsync()
    {
        Log("start");
        RestoreBreakpoints();
        RestoreSolutionOptions();
        var installation = InstallationPath();
        RemoveOtherCopies();
        var ide = Path.Combine(installation, "Common7", "IDE");

        // Built and deployed the way F5 deploys to Exp: VSIXInstaller waits for every running Visual
        // Studio to close, a developer's own included.
        var extension = Path.Combine(Root, "tools", "Bruinet.Bdd.VisualStudio", "Bruinet.Bdd.VisualStudio.slnx");
        await RunAsync(
            Path.Combine(installation, "MSBuild", "Current", "Bin", "MSBuild.exe"),
            $"\"{extension}\" /restore /p:Configuration=Debug /p:DeployExtension=true /p:VSSDKTargetPlatformRegRootSuffix={RootSuffix} /v:minimal /nologo");

        // Without it a freshly installed extension has no commands in the instance.
        await RunAsync(Path.Combine(ide, "devenv.exe"), $"/rootSuffix {RootSuffix} /updateconfiguration");

        _port = FreePort();
        if (File.Exists(SolutionOptions) && !File.Exists(SolutionOptionsBackup))
        {
            File.Copy(SolutionOptions, SolutionOptionsBackup);
        }

        var start = new ProcessStartInfo(Path.Combine(ide, "devenv.exe"), $"\"{TodoSolution}\" /rootSuffix {RootSuffix}") { UseShellExecute = false };
        start.Environment["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"] = $"--remote-debugging-port={_port}";
        Log($"devenv {start.Arguments}, CDP port {_port}");
        _devenv = Process.Start(start) ?? throw new InvalidOperationException("Visual Studio did not start.");
        _dte = await Until(() => Rot.Find(_devenv.Id), StartTimeout, "Visual Studio's automation object");
        Log($"DTE of process {_devenv.Id}");
        await WaitAsync(() => Com(() => (bool)_dte.Solution.IsOpen), StartTimeout, "the Todo solution to open");
        Log("solution open");
        await DisableBreakpointsAsync();
    }

    // Available only once the solution's breakpoints are loaded, and never when it has none.
    private async Task DisableBreakpointsAsync()
    {
        const string command = "Debug.DisableAllBreakpoints";
        try
        {
            await WaitAsync(() => Com(() => (bool)_dte!.Commands.Item(command).IsAvailable), BreakpointsLoad, command);
            ExecuteCommand(command);
        }
        catch (TimeoutException)
        {
            Log("no C# breakpoints to disable");
        }
    }

    /// <summary>Opens a file in the editor.</summary>
    public void OpenFile(string path)
    {
        Log($"open {path}");
        Com(() => _dte!.ItemOperations.OpenFile(path));
    }

    /// <summary>Runs a Visual Studio command by its name, as the Command Window does.</summary>
    public void ExecuteCommand(string name)
    {
        Log($"command {name}");
        Com(() => { _dte!.ExecuteCommand(name); return 0; });
    }

    /// <summary>The Scenario Preview's page, as a Bruinet.Html context: open it first (<c>View.ScenarioPreview</c>).</summary>
    public async Task<IHtmlTestContext> PreviewAsync()
    {
        _playwright ??= await Playwright.CreateAsync();
        Log("connect to the preview over CDP");
        try
        {
            _browser ??= await UntilAsync(() => TryConnect(), StartTimeout, "the preview's WebView2 over CDP");
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{exception.Message} {PreviewDiagnostics()}", exception);
        }

        var page = await Until(
            () => _browser.Contexts.SelectMany(context => context.Pages).FirstOrDefault(candidate => candidate.TitleAsync().GetAwaiter().GetResult() == "Scenario Preview"),
            StartTimeout,
            "the Scenario Preview page");
        Log("preview page found");
        return PlaywrightTestContext.ForPage(page);
    }

    /// <summary>Sets the step breakpoints for these tests; the developer's own come back when the tests end.</summary>
    /// <param name="file">The feature file.</param>
    /// <param name="lines">Its breakpoint lines.</param>
    public void UseBreakpoints(string file, params int[] lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BreakpointsPath)!);
        if (!File.Exists(BreakpointsBackup))
        {
            File.WriteAllText(BreakpointsBackup, File.Exists(BreakpointsPath) ? File.ReadAllText(BreakpointsPath) : string.Empty);
        }

        File.WriteAllText(BreakpointsPath, JsonSerializer.Serialize(new { files = new Dictionary<string, int[]> { [file] = lines } }));
    }

    /// <summary>Where the engine says a run is paused: its file and line; null when it is not.</summary>
    public static (string File, int Line)? Paused()
    {
        try
        {
            using var paused = JsonDocument.Parse(File.ReadAllText(PausedPath));
            return (paused.RootElement.GetProperty("file").GetString()!, paused.RootElement.GetProperty("line").GetInt32());
        }
        catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException)
        {
            // Not paused, or being written just now: the next poll reads it again.
            return null;
        }
    }

    /// <summary>The status of a scenario's newest result written since <paramref name="since"/>; null when none.</summary>
    public static string? ResultSince(string scenario, DateTime since)
    {
        var results = Path.Combine(Root, "TestResults");
        return !Directory.Exists(results)
            ? null
            : Directory.EnumerateDirectories(results)
                .Where(run => File.Exists(Path.Combine(run, "manifest.json")) && File.GetLastWriteTime(Path.Combine(run, "manifest.json")) >= since)
                .Select(TestArtifactManifestWriter.Read)
                .OfType<TestArtifactManifest>()
                .SelectMany(manifest => manifest.Suites.SelectMany(suite => suite.Artifacts))
                .Where(artifact => artifact.Kind == "uat-scenario" && artifact.Name == scenario)
                .OrderByDescending(artifact => artifact.CreatedAtUtc)
                .Select(artifact => artifact.Status)
                .FirstOrDefault();
    }

    /// <summary>Waits until <paramref name="condition"/> holds; fails naming <paramref name="what"/>.</summary>
    public static Task WaitAsync(Func<bool> condition, TimeSpan timeout, string what) => WaitAsync(condition, timeout, () => what);

    /// <summary>Waits until <paramref name="condition"/> holds; fails with <paramref name="what"/> as it is then.</summary>
    public static async Task WaitAsync(Func<bool> condition, TimeSpan timeout, Func<string> what)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > timeout)
            {
                throw new TimeoutException($"Waited {timeout.TotalSeconds:N0} s for {what()}.");
            }

            await Task.Delay(Poll);
        }
    }

    public async Task DisposeAsync()
    {
        RestoreBreakpoints();

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
        if (_dte is not null)
        {
            try
            {
                await QuitAsync();
            }
            catch (Exception exception)
            {
                // Whatever went wrong in the test may leave Visual Studio unable to answer: it is ended below.
                Log($"quitting Visual Studio failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        if (_devenv is { HasExited: false })
        {
            Log($"ending Visual Studio (process {_devenv.Id})");
            _devenv.Kill(entireProcessTree: true);
        }

        RestoreSolutionOptions();

        // The app a debugged test started, should the run have been left without its end.
        foreach (var app in Process.GetProcessesByName("Bruinet.Samples.Todo.App").Where(app => app.StartTime >= _created))
        {
            app.Kill(entireProcessTree: true);
        }
    }

    // After Visual Studio has closed: it writes the file when it closes the solution.
    private static void RestoreSolutionOptions()
    {
        if (File.Exists(SolutionOptionsBackup))
        {
            File.Copy(SolutionOptionsBackup, SolutionOptions, overwrite: true);
            File.Delete(SolutionOptionsBackup);
        }
    }

    private static void RestoreBreakpoints()
    {
        if (!File.Exists(BreakpointsBackup))
        {
            return;
        }

        var own = File.ReadAllText(BreakpointsBackup);
        if (own.Length > 0)
        {
            File.WriteAllText(BreakpointsPath, own);
        }
        else
        {
            File.Delete(BreakpointsPath);
        }

        File.Delete(BreakpointsBackup);
    }

    // What a test waiting for the preview needs to know: the window's title, and whether Visual
    // Studio's WebView2 processes were started with the debugging port.
    private string PreviewDiagnostics()
    {
        var caption = Com(() => (string)_dte!.MainWindow.Caption);
        var webViews = Process.GetProcessesByName("msedgewebview2").Length;
        return $"Visual Studio: '{caption}'; {webViews} WebView2 processes on this machine; nothing listens on port {_port}.";
    }

    // Visual Studio refuses to close during a build or a debug session: both are ended first, and
    // the process is ended when it still does not go.
    private async Task QuitAsync()
    {
        if (!InDesignMode)
        {
            Com(() => { _dte!.Debugger.Stop(false); return 0; });
        }

        if (Com(() => (int)_dte!.Solution.SolutionBuild.BuildState) == 2)
        {
            ExecuteCommand("Build.Cancel");
        }

        await WaitAsync(() => InDesignMode && Com(() => (int)_dte!.Solution.SolutionBuild.BuildState) != 2, BusyTimeout, "the build and debugging to end");
        Com(() => { _dte!.Quit(); return 0; });
        if (!_devenv!.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            _devenv.Kill(entireProcessTree: true);
        }
    }

    private async Task<IBrowser?> TryConnect()
    {
        try
        {
            return await _playwright!.Chromium.ConnectOverCDPAsync($"http://localhost:{_port}");
        }
        catch (PlaywrightException)
        {
            // No WebView2 listens yet: the preview window has not been created.
            return null;
        }
    }

    private static async Task<T> UntilAsync<T>(Func<Task<T?>> attempt, TimeSpan timeout, string what)
        where T : class
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            if (await attempt() is { } found)
            {
                return found;
            }

            await Task.Delay(Poll);
        }

        throw new TimeoutException($"Waited {timeout.TotalSeconds:N0} s for {what}.");
    }

    private static Task<T> Until<T>(Func<T?> attempt, TimeSpan timeout, string what)
        where T : class
        => UntilAsync(() => Task.FromResult(attempt()), timeout, what);

    // Visual Studio rejects calls while it is busy (RPC_E_CALL_REJECTED, RPC_E_SERVERCALL_RETRYLATER): retried.
    // A call Visual Studio does not answer at all (a modal dialog) cannot be cancelled: it is left
    // on its thread, and the test fails naming it; disposing then ends the process.
    private static T Com<T>(Func<T> call)
    {
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                var answer = Task.Run(call);
                return answer.Wait(BusyTimeout)
                    ? answer.Result
                    : throw new TimeoutException($"Visual Studio did not answer within {BusyTimeout.TotalMinutes:N0} minutes: a modal dialog may be open in the {RootSuffix} instance.");
            }
            catch (AggregateException wrapped) when (wrapped.InnerException is { } inner)
            {
                if (!IsBusy(inner) || clock.Elapsed >= BusyTimeout)
                {
                    ExceptionDispatchInfo.Capture(inner).Throw();
                }

                Thread.Sleep(Poll);
            }
        }
    }

    // RPC_E_CALL_REJECTED or RPC_E_SERVERCALL_RETRYLATER: as a COMException, or, when late binding
    // asks for a member's dispatch id while Visual Studio is busy, a MissingMemberException naming it.
    private static bool IsBusy(Exception exception)
        => exception is COMException { HResult: unchecked((int)0x80010001) or unchecked((int)0x8001010A) }
           || exception is MissingMemberException missing && (missing.Message.Contains("0x8001010A", StringComparison.OrdinalIgnoreCase) || missing.Message.Contains("0x80010001", StringComparison.OrdinalIgnoreCase));

    // A step before Visual Studio starts, with a time limit: one that waits on something fails naming it.
    private static async Task RunAsync(string file, string arguments)
    {
        Log($"{Path.GetFileName(file)} {arguments}");
        using var process = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, RedirectStandardOutput = true })
            ?? throw new InvalidOperationException($"{file} did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        using var limit = new CancellationTokenSource(SetupTimeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException exception)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{Path.GetFileName(file)} {arguments} did not end within {SetupTimeout.TotalMinutes:N0} minutes.", exception);
        }

        Log($"{Path.GetFileName(file)} ended with {process.ExitCode}");
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(file)} {arguments} ended with {process.ExitCode}:\n{await output}");
        }
    }

    /// <summary>Adds a timestamped line to <see cref="LogPath"/>.</summary>
    public static void Log(string line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
    }

    // A copy installed another way (VSIXInstaller) next to the deployed one makes Visual Studio stop on a dialog.
    private static void RemoveOtherCopies()
    {
        var instance = Vswhere("instanceId");
        var hives = Directory.GetDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "VisualStudio"), $"*_{instance}{RootSuffix}");
        foreach (var manifest in hives.SelectMany(hive => Directory.Exists(Path.Combine(hive, "Extensions"))
                     ? Directory.EnumerateFiles(Path.Combine(hive, "Extensions"), "extension.vsixmanifest", SearchOption.AllDirectories)
                     : []))
        {
            var folder = Path.GetDirectoryName(manifest)!;
            if (File.ReadAllText(manifest).Contains(ExtensionId, StringComparison.Ordinal) && !folder.Contains(DeployFolder, StringComparison.OrdinalIgnoreCase))
            {
                Log($"remove the copy at {folder}");
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static string InstallationPath() => Vswhere("installationPath");

    private static string Vswhere(string property)
    {
        var vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        using var process = Process.Start(new ProcessStartInfo(vswhere, $"-prerelease -latest -property {property}") { UseShellExecute = false, RedirectStandardOutput = true })!;
        var value = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return value.Length > 0 ? value : throw new InvalidOperationException("No Visual Studio found (vswhere).");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bruinet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No Bruinet.sln above the test output.");
    }

    // Visual Studio registers its DTE in the Running Object Table as "!VisualStudio.DTE.<version>:<pid>".
    private static class Rot
    {
        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(int reserved, out IBindCtx context);

        public static object? Find(int processId)
        {
            Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out var table));
            Marshal.ThrowExceptionForHR(CreateBindCtx(0, out var context));
            table.EnumRunning(out var monikers);
            var one = new IMoniker[1];
            while (monikers.Next(1, one, IntPtr.Zero) == 0)
            {
                one[0].GetDisplayName(context, null, out var name);
                if (name.StartsWith("!VisualStudio.DTE.", StringComparison.Ordinal) && name.EndsWith($":{processId}", StringComparison.Ordinal))
                {
                    table.GetObject(one[0], out var found);
                    return found;
                }
            }

            return null;
        }
    }
}
