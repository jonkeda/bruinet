using Bruinet.Bdd.Runner;
using Bruinet.Uat;

namespace Bruinet.Presenter.Services;

/// <summary>
/// Runs a suite's scenarios in a runner process (AD-025), one session a scenario. Presenter loads
/// none of the suite's code for a run. The runner starts from a copy of the suite's output and waits
/// between sessions (<see cref="BddWarmRunners"/>), so the suite can be built while it does.
/// </summary>
public sealed class BddExecutionService : IBddExecutionService, IDisposable
{
    private readonly object _lock = new();
    private readonly BddRunnerJob _job = new();
    private readonly List<Task> _ending = [];
    private readonly BddWarmRunners _runners;

    /// <summary>Creates the service; its runners end with Presenter.</summary>
    public BddExecutionService()
    {
        _runners = new BddWarmRunners(started: process => _job.Add(process));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> WindowEnvironment { get; set; } = new Dictionary<string, string>();

    public async Task<PresenterBddExecutionSession> CreateSessionAsync(
        string workspacePath,
        string scenarioFilePath,
        string scenarioName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioName);
        cancellationToken.ThrowIfCancellationRequested();

        // The suite's warm runner, or a new one, from a copy of its output (AD-025).
        var start = StartFor(workspacePath, out var configPath, out var pages);
        var runner = await _runners.TakeAsync(start, cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = await runner.RequestAsync(new BddRunnerOpen(configPath, pages, scenarioFilePath, scenarioName), cancellationToken).ConfigureAwait(false);
            return opened switch
            {
                BddRunnerOpened ready => new PresenterBddExecutionSession(runner, ready, (ended, closing, debugged) => Release(ended, closing, debugged)),
                BddRunnerFailed failed => throw new InvalidOperationException(failed.Message),
                _ => throw new BddRunnerException($"The runner answered '{opened.GetType().Name}' to an open request."),
            };
        }
        catch
        {
            await runner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public IDisposable BeginRun() => new Run();

    /// <inheritdoc />
    public Task EndRunnersAsync()
    {
        lock (_lock)
        {
            _ending.RemoveAll(task => task.IsCompleted);
            return Task.WhenAll(_ending);
        }
    }

    /// <inheritdoc />
    public Task WarmAsync(string workspacePath)
    {
        try
        {
            return _runners.WarmAsync(StartFor(workspacePath, out _, out _));
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or BddRunnerException)
        {
            // Not runnable yet (not built, no config): a session says why.
            return Task.CompletedTask;
        }
    }

    /// <summary>Ends every runner, waiting or in a session, when Presenter closes.</summary>
    public void Dispose()
    {
        _runners.DisposeAsync().AsTask().Wait(BddRunnerClient.CancelGrace);
        _job.Dispose();
    }

    // How the suite's runner starts, from its output; the config and Pages assembly it opens with.
    private BddRunnerStart StartFor(string workspacePath, out string configPath, out string pages)
    {
        configPath = BddFileFormat.FindConfig(workspacePath);
        if (!File.Exists(configPath))
        {
            throw new InvalidOperationException($"UAT config was not found: {configPath}");
        }

        var config = BddConfigParser.ParseFile(configPath);
        ValidateConfig(config, configPath);
        pages = ResolvePagesAssembly(workspacePath, config);
        return new BddRunnerStart(
            pages,
            BddWorkspaceConfigInspector.ResolveWorkingDirectory(workspacePath, config),
            CreateTargetEnvironment(workspacePath, config, BddWorkspaceConfigInspector.ResolveConfiguredAppPath(workspacePath, config), WindowEnvironment));
    }

    // A session ended: once its scenario is closed, its runner waits for the next session, from its
    // copy of the output; one that could not close the scenario ends.
    private void Release(BddRunnerClient runner, Task closing, bool debugged)
    {
        var ending = ReleaseAsync(runner, closing, debugged);
        lock (_lock)
        {
            _ending.RemoveAll(task => task.IsCompleted);
            _ending.Add(ending);
        }
    }

    private async Task ReleaseAsync(BddRunnerClient runner, Task closing, bool debugged)
    {
        try
        {
            await closing.ConfigureAwait(false);
            // A debugged runner ends with its session, and the debugging session with it.
            if (!debugged && closing is Task<BddRunnerMessage> { Result: BddRunnerDone })
            {
                _runners.Return(runner);
                return;
            }
        }
        catch (Exception exception) when (exception is BddRunnerException or OperationCanceledException or ObjectDisposedException or IOException)
        {
            // Closed with its runner, below.
        }

        await runner.DisposeAsync().ConfigureAwait(false);
    }

    // Runners wait between sessions anyway (AD-025): a run needs nothing of its own.
    private sealed class Run : IDisposable
    {
        public void Dispose()
        {
        }
    }
    private static void ValidateConfig(BddConfig config, string configPath)
    {
        if (!config.Runtime.TryGetValue("Target", out var target) ||
            !BddTargetRegistry.TryGet(target, out _))
        {
            throw new InvalidOperationException(
                $"UAT config '{configPath}' must set Runtime Target to one of: {BddTargetRegistry.SupportedTargetList}.");
        }

        if (!config.Runtime.TryGetValue("Fixture", out var fixture) ||
            string.IsNullOrWhiteSpace(fixture))
        {
            throw new InvalidOperationException(
                $"UAT config '{configPath}' must set Runtime Fixture.");
        }

        // A Project row replaces the Pages registration, so either is enough.
        if (config.Assemblies.Count == 0 &&
            !config.Runtime.ContainsKey(BddConfigFields.Project))
        {
            throw new InvalidOperationException(
                $"UAT config '{configPath}' must set Runtime Project or register at least one assembly.");
        }
    }

    // What the runner, and so the app it starts, gets beside Presenter's own environment.
    private static Dictionary<string, string> CreateTargetEnvironment(
        string workspacePath,
        BddConfig config,
        string? appPath,
        IReadOnlyDictionary<string, string> windowEnvironment)
    {
        // How the app's window is shown; Presenter places it itself once it is open
        // (PresenterWindowService), for every target.
        Dictionary<string, string> environment = new(windowEnvironment, StringComparer.OrdinalIgnoreCase);

        // Only an explicit override travels to the environment. With no AppPath the fixture
        // resolves the app itself, which is what every fixture base already requires of it.
        if (appPath is not null)
        {
            environment[BddTargetRegistry.GetRequired(config).AppPathEnvironmentVariable] = appPath;
        }

        // The recorders a suite does not reference itself (Bruinet.Narrator.Windows) are Presenter's.
        environment.TryAdd(Bruinet.Recording.RecordingPackages.Variable, AppContext.BaseDirectory);

        // Results where the IDEs' preview reads them (AD-022), whichever folder the runner runs in.
        environment.TryAdd(Bruinet.Core.Artifacts.TestArtifactOptions.RootDirectoryEnvironmentVariable, Bruinet.Core.Artifacts.TestArtifactOptions.RootFor(workspacePath));

        // The scenario files from the workspace, not the build's copies (AD-022).
        environment.TryAdd(BddScenarioSource.SourceDirectoryEnvironmentVariable, Path.GetDirectoryName(Path.GetFullPath(BddFileFormat.FindConfig(workspacePath)))!);
        return environment;
    }

    /// <summary>
    /// The Pages assembly: an explicit registration when there is one, otherwise the output
    /// of the project named by <c>Runtime Project</c>.
    /// </summary>
    private static string ResolvePagesAssembly(string workspacePath, BddConfig config)
    {
        var registration = config.Assemblies.FirstOrDefault(assembly =>
            assembly.Kind.Equals("Pages", StringComparison.OrdinalIgnoreCase));
        if (registration is not null)
        {
            // As written, then by name below the solution, then as a suite Presenter itself was built with.
            var hosted = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(registration.Assembly));
            return BddWorkspaceConfigInspector.ResolveAssemblyPath(workspacePath, registration.Assembly)
                   ?? (File.Exists(hosted)
                       ? hosted
                       : throw new InvalidOperationException($"Assembly '{registration.Assembly}' was not found. Build the UAT page-object project first."));
        }

        var inspection = BddWorkspaceConfigInspector.Inspect(workspacePath);
        if (inspection.Project is { PagesAssemblyPath.Length: > 0 } project)
        {
            return project.PagesAssemblyExists
                ? project.PagesAssemblyPath
                : throw new InvalidOperationException(
                    $"{Path.GetFileNameWithoutExtension(project.ResolvedPath)} is not built. " +
                    $"Its output belongs at {project.PagesAssemblyPath}.");
        }

        throw new InvalidOperationException(
            "UAT config must set Runtime Project or register a Pages assembly.");
    }
}
