using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Bruinet.Core.Artifacts;
using Bruinet.Core.Interfaces;
using Bruinet.Core.Recording;
using Bruinet.Core.Settings;
using Bruinet.Narrator;
using Bruinet.Recording;

namespace Bruinet.Uat;

public abstract class UatScenarioTestBase<TFixture>
    where TFixture : class
{
    // One recording session per suite per test process: every scenario of the run shares its scenes folder.
    private static readonly ConcurrentDictionary<string, Lazy<BddRecordingSession?>> Recordings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<ScreenRecorders> Recorders = new(ScreenRecorders.Discover);

    protected UatScenarioTestBase(TFixture fixture)
    {
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    protected TFixture Fixture { get; }

    protected virtual string ConfigFilePath => BddScenarioSource.GetConfigFilePath();

    protected virtual UatRuntimeValidationOptions RuntimeValidation => UatRuntimeValidationOptions.Default;

    protected virtual BddConfig? GetRunConfig(UatRuntime runtime) => runtime.Config;

    protected virtual ITestSettingsProvider CreateSettingsProvider() => new JsonTestSettingsProvider();

    protected virtual TestSettings ResolveScenarioSettings(BddBoundScenario scenario, UatRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(runtime);

        var settings = runtime.Config.Settings;
        return CreateSettingsProvider().Resolve(new TestSettingsRequest(
            runtime.ConfigDirectory,
            settings.Root,
            settings.DefaultFile,
            settings.LocalFile,
            GetScenarioId(scenario),
            settings.ScenarioConvention));
    }

    protected virtual void ConfigureScenarioContext(
        BddExecutionContext context,
        BddBoundScenario scenario,
        UatRuntime runtime)
    {
        context.SetSettings(ResolveScenarioSettings(scenario, runtime));
    }

    /// <summary>Called before each scenario; by default the fixture's <see cref="IUatScenarioLifecycle"/>, when it has one.</summary>
    /// <param name="scenario">The scenario about to run.</param>
    protected virtual void BeforeScenario(BddBoundScenario scenario)
    {
        using var span = Bruinet.Core.Logging.Timing.Span("lifecycle", "BeforeScenario");
        (Fixture as IUatScenarioLifecycle)?.BeforeScenario(scenario);
    }

    /// <summary>
    /// Called after each scenario, passed or failed, once its recording is finished, so nothing it
    /// does is in a video; by default the fixture's <see cref="IUatScenarioLifecycle"/>, when it has one.
    /// </summary>
    /// <param name="scenario">The scenario that ran.</param>
    protected virtual void AfterScenario(BddBoundScenario scenario)
    {
        using var span = Bruinet.Core.Logging.Timing.Span("lifecycle", "AfterScenario");
        (Fixture as IUatScenarioLifecycle)?.AfterScenario(scenario);
    }

    protected virtual string EvidenceTestClassName => GetType().Name;

    protected virtual string EvidenceDescription => "failure";

    protected virtual int EvidenceScenarioNameMaxLength => 80;

    protected virtual string? CaptureEvidenceOnFailure(BddBoundScenario scenario)
    {
        var screenshotService = GetScreenshotService();
        return screenshotService?.Capture(
            EvidenceTestClassName,
            FormatEvidenceScenarioName(scenario),
            EvidenceDescription);
    }

    /// <summary>
    /// What the app shows, for a recording (<c>BRUINET_RECORD</c>): the fixture, when it is
    /// <see cref="IRecordable"/>, else its first public property that is (its test context).
    /// </summary>
    protected virtual RecordingTarget? RecordingTarget => UatRecording.TargetOf(Fixture);

    protected virtual bool IsAcceptableResult(BddScenarioExecutionResult execution) =>
        execution.Result.Passed || execution.Result.Skipped;

    protected static IEnumerable<object[]> GetScenarioFiles(
        string folderName = BddFileFormat.FeatureFolder,
        string? configurationFilter = null)
    {
        return BddScenarioSource.GetScenarioFileTheoryData(
            folderName,
            configurationFilter: configurationFilter);
    }

    /// <summary>One theory case per scenario, <c>[filePath, scenarioName]</c>; run each with <see cref="RunUatScenarioAsync"/>.</summary>
    protected static IEnumerable<object[]> GetScenarioTheoryData(
        string folderName = BddFileFormat.FeatureFolder,
        string? configurationFilter = null)
    {
        return BddScenarioSource.GetScenarioTheoryData(
            folderName,
            configurationFilter: configurationFilter);
    }

    /// <summary>
    /// Called before the first scenario of a file runs, and again whenever the file changes: the
    /// place to load what the file's metadata asks for, such as its <c>TestData</c>. By default the
    /// fixture's <see cref="IUatScenarioLifecycle"/>, when it has one.
    /// </summary>
    /// <param name="document">The file, as parsed.</param>
    protected virtual void BeforeFile(BddDocument document)
    {
        (Fixture as IUatScenarioLifecycle)?.BeforeFile(document);
    }

    [DebuggerNonUserCode] // see RunBoundScenarioAsync
    protected async Task RunUatFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        EnterFile(filePath);
        var context = BindUatFile(filePath);

        foreach (var scenario in context.Document.Scenarios)
        {
            await RunBoundScenarioAsync(context, scenario, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Runs one scenario of a file and asserts it passes.</summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="scenarioName">The scenario's name, as <see cref="GetScenarioTheoryData"/> gives it.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    [DebuggerNonUserCode] // see RunBoundScenarioAsync
    protected async Task RunUatScenarioAsync(
        string filePath,
        string scenarioName,
        CancellationToken cancellationToken = default)
    {
        using var test = Bruinet.Core.Testing.TestScope.EnterTest($"{GetType().Name}: {scenarioName}");
        using var scenarioScope = Bruinet.Core.Testing.TestScope.EnterScenario(scenarioName);
        using var whole = Bruinet.Core.Logging.Timing.Span("test", scenarioName);
        UatBoundFileContext context;
        using (Bruinet.Core.Logging.Timing.Span("bind", "EnterFile"))
        {
            EnterFile(filePath);
        }

        context = BindUatFile(filePath, scenarioName);
        var matches = context.Document.Scenarios.Where(scenario => scenario.Source.Name == scenarioName).ToList();
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(matches.Count == 0
                ? $"'{filePath}' has no scenario named '{scenarioName}'. It has: {string.Join(", ", context.Document.Scenarios.Select(s => $"'{s.Source.Name}'"))}."
                : $"'{filePath}' has {matches.Count} scenarios named '{scenarioName}'. Give each scenario a name of its own.");
        }

        await RunBoundScenarioAsync(context, matches[0], cancellationToken).ConfigureAwait(false);
    }

    // xUnit creates a test-class instance per case, so the file last entered is kept per fixture:
    // collections that share a test base but not a fixture (one per collection) each enter a file
    // once, where one value per closed type made each switch between them enter it again.
    private static readonly ConditionalWeakTable<object, StrongBox<string>> CurrentFiles = new();

    // Before binding: BeforeFile may restart the app, and binding reads the fixture's composition.
    private void EnterFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var current = CurrentFiles.GetValue(Fixture, _ => new StrongBox<string>());
        lock (current)
        {
            if (current.Value == fullPath || BddMarkdownParser.ParseFile(filePath).Document is not { } document)
            {
                return;
            }

            BeforeFile(document);
            current.Value = fullPath;
        }
    }

    // Library code to the debugger, with its callers: a failed scenario is shown in the preview and
    // Test Explorer, so Visual Studio must not stop on the exception that fails the test for xUnit.
    [DebuggerNonUserCode]
    private async Task RunBoundScenarioAsync(
        UatBoundFileContext context,
        BddBoundScenario scenario,
        CancellationToken cancellationToken)
    {
        // A file's run enters each scenario here; a scenario's run has entered it already, before binding.
        using var scenarioScope = Bruinet.Core.Testing.TestScope.Current.Scenario == scenario.Source.Name
            ? null
            : Bruinet.Core.Testing.TestScope.EnterScenario(scenario.Source.Name);
        var started = System.Diagnostics.Stopwatch.StartNew();
        BddScenarioExecutionResult execution;
        using (Bruinet.Core.Logging.Timing.Span("scenario", scenario.Source.Name))
        {
            execution = await RunScenarioAsync(scenario, context.Runtime, cancellationToken)
                .ConfigureAwait(false);
        }

        using (Bruinet.Core.Logging.Timing.Span("report", scenario.Source.Name))
        {
            RecordScenarioResult(context, scenario, execution, started.Elapsed);
        }

        if (!IsAcceptableResult(execution))
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatResults(
                    execution.Result,
                    execution.Context,
                    context.Runtime.DiscoveryReport,
                    context.Catalog,
                    execution.EvidencePath));
        }
    }

    protected async Task RunExpectedFailureUatFileAsync(
        string filePath,
        params string[] expectedDiagnostics)
    {
        var context = BindUatFile(filePath);
        if (context.Document.Scenarios.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected '{filePath}' to contain exactly one scenario, but found {context.Document.Scenarios.Count}.");
        }

        var scenario = context.Document.Scenarios[0];
        var started = System.Diagnostics.Stopwatch.StartNew();
        var execution = await RunScenarioAsync(scenario, context.Runtime, CancellationToken.None)
            .ConfigureAwait(false);

        RecordScenarioResult(context, scenario, execution, started.Elapsed);

        var details = BddDiagnosticsFormatter.FormatResults(
            execution.Result,
            execution.Context,
            context.Runtime.DiscoveryReport,
            context.Catalog,
            execution.EvidencePath);

        if (execution.Result.Passed)
        {
            throw new InvalidOperationException(
                $"Expected UAT scenario '{scenario.Source.Name}' to fail, but it passed.{Environment.NewLine}{details}");
        }

        foreach (var expected in expectedDiagnostics.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!details.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected diagnostics to contain '{expected}'.{Environment.NewLine}{details}");
            }
        }
    }

    /// <summary>
    /// Binds a scenario file that must fail binding, without running it, and checks the bind
    /// diagnostics contain each expected text.
    /// </summary>
    /// <param name="filePath">The scenario file.</param>
    /// <param name="expectedDiagnostics">Texts the formatted bind diagnostics must contain.</param>
    protected void RunExpectedBindFailureUatFile(
        string filePath,
        params string[] expectedDiagnostics)
    {
        var file = BddScenarioFiles.Load(filePath);
        if (!file.Success)
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
        }

        var runtime = CreateRuntime();
        var catalog = CreateCommandCatalog(runtime);
        var bind = file.Bind(runtime.CreateCatalogSet(catalog));
        if (bind.Success)
        {
            throw new InvalidOperationException($"Expected '{filePath}' to fail binding, but it bound.");
        }

        var details = BddDiagnosticsFormatter.FormatBindFailure(bind.Diagnostics, runtime.DiscoveryReport, catalog);
        foreach (var expected in expectedDiagnostics.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!details.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected bind diagnostics to contain '{expected}'.{Environment.NewLine}{details}");
            }
        }
    }

    protected virtual UatRuntime CreateRuntime()
    {
        return new UatRuntime(Fixture, ConfigFilePath, RuntimeValidation);
    }

    protected virtual BddCommandCatalog CreateCommandCatalog(UatRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return runtime.CreateCommandCatalog();
    }

    protected virtual Task<BddScenarioExecutionResult> RunScenarioAsync(
        BddBoundScenario scenario,
        UatRuntime runtime,
        CancellationToken cancellationToken)
    {
        var scope = runtime.CreateScope();
        return RunScenarioWithScopeAsync(scenario, runtime, scope, cancellationToken);
    }

    private async Task<BddScenarioExecutionResult> RunScenarioWithScopeAsync(
        BddBoundScenario scenario,
        UatRuntime runtime,
        IDisposable? scope,
        CancellationToken cancellationToken)
    {
        using (scope)
        {
            BddScenarioExecutionResult execution;
            try
            {
                execution = await RecordScenarioAsync(scenario, runtime, scope, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                AfterFailedScenario(scenario);
                throw;
            }

            if (execution.Result.Passed)
            {
                AfterScenario(scenario);
            }
            else
            {
                AfterFailedScenario(scenario);
            }

            return execution;
        }
    }

    // A failed or canceled scenario's own failure is the one to report: an AfterScenario that fails
    // too, likely on the same stuck app, is only traced.
    private void AfterFailedScenario(BddBoundScenario scenario)
    {
        try
        {
            AfterScenario(scenario);
        }
        catch (Exception after)
        {
            Trace.TraceWarning($"AfterScenario failed after '{scenario.Source.Name}' failed: {after}");
        }
    }

    // Runs the scenario, recorded when BRUINET_RECORD asks; the recording ends before AfterScenario.
    private async Task<BddScenarioExecutionResult> RecordScenarioAsync(
        BddBoundScenario scenario,
        UatRuntime runtime,
        IDisposable? scope,
        CancellationToken cancellationToken)
    {
        var session = Recordings.GetOrAdd(runtime.ConfigDirectory, directory => new Lazy<BddRecordingSession?>(() => BddRecordingSession.FromEnvironment(directory))).Value;
        await using var recording = session is null ? null : await RecordingFor(session, scenario, cancellationToken).ConfigureAwait(false);
        var execution = await BddScenarioExecutor.RunAsync(
            scenario,
            GetRunConfig(runtime),
            BeforeScenario,
            context =>
            {
                runtime.ConfigureContext(context);
                ConfigureScenarioContext(context, scenario, runtime);
                if (scope is Microsoft.Extensions.DependencyInjection.IServiceScope serviceScope)
                {
                    UatRuntime.ConfigureScope(context, serviceScope.ServiceProvider);
                }

                if (recording is not null)
                {
                    context.Pacer = recording;
                }
            },
            CaptureEvidenceOnFailure,
            cancellationToken).ConfigureAwait(false);
        if (recording is not null)
        {
            await session!.FinishAsync(recording, cancellationToken).ConfigureAwait(false);
        }

        return execution;
    }

    // The scene this scenario records under BRUINET_RECORD; null when the run does not record it.
    private async Task<BddSceneRecording?> RecordingFor(BddRecordingSession session, BddBoundScenario scenario, CancellationToken cancellationToken)
    {
        if (scenario.Source.Source.FilePath is not { } featureFile)
        {
            return null;
        }

        var scene = await session.SceneForAsync(featureFile, scenario.Source.Name, cancellationToken).ConfigureAwait(false);
        return scene is null ? null : new BddSceneRecording(scene, () => RecordingTarget, Recorders.Value);
    }

    protected virtual IScreenshotService? GetScreenshotService() => UatScenarioResults.ScreenshotServiceOf(Fixture);

    protected virtual string FormatEvidenceScenarioName(BddBoundScenario scenario)
        => UatScenarioResults.EvidenceName(scenario, EvidenceScenarioNameMaxLength);

    private void RecordScenarioResult(
        UatBoundFileContext context,
        BddBoundScenario scenario,
        BddScenarioExecutionResult execution,
        TimeSpan duration)
        => UatScenarioResults.Record(
            context.Runtime,
            context.Catalog,
            scenario,
            execution,
            duration,
            GetType().Assembly.GetName().Name,
            FormatEvidenceScenarioName(scenario),
            UatScenarioResults.TestMode);

    private static string? GetScenarioId(BddBoundScenario scenario)
    {
        return scenario.Source.Tags
            .Select(BddTagConventions.NormalizeTag)
            .FirstOrDefault(tag => tag.StartsWith("uat-", StringComparison.OrdinalIgnoreCase));
    }

    // The runtime and its catalogs, made once per fixture and test class and kept for the tests after (speed plan, iteration 2):
    // a fresh catalog's first match warms up for seconds, and every test made one. A scenario still gets a scope of its own.
    private static readonly ConditionalWeakTable<object, ConcurrentDictionary<Type, Lazy<(UatRuntime, BddCommandCatalog, BddCatalogSet)>>> KeptCatalogs = new();

    private (UatRuntime Runtime, BddCommandCatalog Catalog, BddCatalogSet Catalogs) Catalogs()
        => KeptCatalogs.GetValue(Fixture, _ => new()).GetOrAdd(GetType(), _ => new Lazy<(UatRuntime, BddCommandCatalog, BddCatalogSet)>(() =>
        {
            UatRuntime runtime;
            using (Bruinet.Core.Logging.Timing.Span("bind", "CreateRuntime"))
            {
                runtime = CreateRuntime();
            }

            using (Bruinet.Core.Logging.Timing.Span("bind", "CreateCommandCatalog"))
            {
                var catalog = CreateCommandCatalog(runtime);
                return (runtime, catalog, runtime.CreateCatalogSet(catalog));
            }
        })).Value;

    // only: the one scenario a test runs; the file's other scenarios are not bound (speed plan, iteration 1).
    private UatBoundFileContext BindUatFile(string filePath, string? only = null)
    {
        BddLoadedScenarioFile file;
        using (Bruinet.Core.Logging.Timing.Span("bind", "Load"))
        {
            file = BddScenarioFiles.Load(filePath);
        }

        if (!file.Success)
        {
            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatDiagnostics(file.Diagnostics));
        }

        var (runtime, catalog, catalogs) = Catalogs();
        BddBindResult bind;
        using (Bruinet.Core.Logging.Timing.Span("bind", "Bind"))
        {
            bind = file.Bind(catalogs, only);
        }

        if (!bind.Success || bind.Document is null)
        {
            // A result for each scenario that did not bind, so the tools show it as failed with kind bind (review 01).
            foreach (var scenario in file.Document!.Scenarios.Where(each => only is null || each.Name == only))
            {
                UatScenarioResults.RecordBindFailure(runtime, scenario, bind.Diagnostics, GetType().Assembly.GetName().Name, UatScenarioResults.TestMode);
            }

            throw new InvalidOperationException(
                BddDiagnosticsFormatter.FormatBindFailure(
                    bind.Diagnostics,
                    runtime.DiscoveryReport,
                    catalog));
        }

        return new UatBoundFileContext(runtime, catalog, bind.Document);
    }

    private sealed record UatBoundFileContext(
        UatRuntime Runtime,
        BddCommandCatalog Catalog,
        BddBoundDocument Document);
}
