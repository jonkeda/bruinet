using System.ComponentModel;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Bruinet.Uat;

/// <summary>
/// A theory over scenarios (<c>[filePath, scenarioName]</c> rows, as
/// <see cref="UatScenarioTestBase{TFixture}"/> gives them): the same one case per scenario as
/// <c>[Theory]</c>, each with its stable address in the <c>BddScenario</c> trait
/// (<see cref="BddScenarioAddress"/>), so one scenario can be run by filter (R1 of the IDE plan).
/// </summary>
[XunitTestCaseDiscoverer("Bruinet.Uat.UatScenarioTheoryDiscoverer", "Bruinet.Uat")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class UatScenarioTheoryAttribute : TheoryAttribute
{
}

/// <summary>Finds the cases of a <see cref="UatScenarioTheoryAttribute"/>: one <see cref="UatScenarioTestCase"/> per row.</summary>
/// <param name="diagnosticMessageSink">Where discovery reports problems.</param>
// xUnit v2 only: a move to v3 redoes this discoverer (v3 lets a case set its own source location).
public sealed class UatScenarioTheoryDiscoverer(IMessageSink diagnosticMessageSink) : TheoryDiscoverer(diagnosticMessageSink)
{
    /// <inheritdoc />
    protected override IEnumerable<IXunitTestCase> CreateTestCasesForDataRow(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo theoryAttribute,
        object[] dataRow)
        => [new UatScenarioTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod, dataRow)];
}

/// <summary>
/// One scenario's case: an <see cref="XunitTestCase"/> whose <c>BddScenario</c> trait is its
/// address, and whose source is the scenario's line in its feature file when that file is the
/// project's own.
/// </summary>
public sealed class UatScenarioTestCase : XunitTestCase
{
    /// <summary>For the deserializer.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public UatScenarioTestCase()
    {
    }

    /// <summary>Creates the case for one row.</summary>
    /// <param name="diagnosticMessageSink">Where problems are reported.</param>
    /// <param name="defaultMethodDisplay">How the method is shown.</param>
    /// <param name="defaultMethodDisplayOptions">How the method name is shown.</param>
    /// <param name="testMethod">The theory.</param>
    /// <param name="testMethodArguments">The row: the feature file and the scenario's name.</param>
    public UatScenarioTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        object[] testMethodArguments)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
    {
        // Set before the runner looks for the method's C# source, which it only does when this is empty.
        if (Scenario(testMethodArguments) is var (file, name) && UatScenarioSourceFile.Of(file, name) is { } source)
        {
            SourceInformation = source;
        }
    }

    /// <inheritdoc />
    protected override void Initialize()
    {
        base.Initialize();

        // Computed again from the row after deserializing, so it needs no serialization of its own.
        if (Scenario(TestMethodArguments) is var (file, name) && name.Length > 0)
        {
            Traits[BddScenarioAddress.Trait] = [BddScenarioAddress.Of(UatScenarioSourceFile.RelativeToConfig(file), name)];

            // One runner over a folder still files each scenario under its own module, journeys and tier.
            UatScenarioTraits.AddTo(Traits, UatScenarioTraits.Of(file, name));

            // A scenario its suite's config skips here (a head, @browser, a skip rule) is reported skipped with the reason, and
            // never run; reported as passed, a run's counts said it ran (.my/test-selection/next-plan.md, item 2).
            if (UatScenarioSkip.Of(file, name) is { } reason)
            {
                SkipReason = reason;
            }
        }
    }

    /// <inheritdoc />
    public override Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        // Time paused in the debugger would count against the timeout and fail a scenario still going.
        if (System.Diagnostics.Debugger.IsAttached)
        {
            Timeout = 0;
        }

        return base.RunAsync(diagnosticMessageSink, messageBus, constructorArguments, aggregator, cancellationTokenSource);
    }

    private static (string File, string Name)? Scenario(object[]? arguments)
        => arguments is [string file, string name, ..] ? (file, name) : null;
}

/// <summary>Whether the suite's config skips a scenario, decided as the runner decides it, from its tags.</summary>
public static class UatScenarioSkip
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BddConfig?> Configs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The reason the config next to the scenarios skips the scenario now; null when it runs.</summary>
    public static string? Of(string file, string scenarioName)
    {
        var configPath = BddScenarioSource.GetConfigFilePath();
        var config = Configs.GetOrAdd(configPath, path => File.Exists(path) ? BddConfigParser.ParseFile(path) : null);
        return config is null ? null : Of(config, file, scenarioName, Environment.GetEnvironmentVariable);
    }

    /// <summary>
    /// The reason <paramref name="config"/> skips the scenario for its head or tags, with these environment variables; null when
    /// it runs, or when only a skip rule would skip it (the run decides those).
    /// </summary>
    public static string? Of(BddConfig config, string file, string scenarioName, Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(config);
        var scenario = BddMarkdownParser.ParseFile(file).Document?.Scenarios.FirstOrDefault(candidate => candidate.Name == scenarioName);
        if (scenario is null)
        {
            return null;
        }

        // A skip rule's variable may be set by the fixture as it starts, after discovery (Tomorrow's TOMORROW_OWN_DATABASE): only the
        // run can decide those, so here only a skip without a rule (a head, @browser) counts.
        var decision = config.EvaluateSkip(scenario.Tags, getEnvironmentVariable);
        return decision is { ShouldSkip: true, Rule: null } ? decision.Reason ?? "Skipped by the suite's config." : null;
    }
}

// Where a case's scenario is: its file relative to the config (in the build output, as the tests
// read it), and the scenario's line in the project's own copy of the file.
internal static class UatScenarioSourceFile
{
    public static string RelativeToConfig(string file)
        => Path.GetRelativePath(Path.GetDirectoryName(BddScenarioSource.GetConfigFilePath())!, Path.GetFullPath(file)).Replace('\\', '/');

    public static ISourceInformation? Of(string file, string scenarioName)
    {
        if (ProjectCopy(file) is not { } source ||
            BddMarkdownParser.ParseFile(source).Document?.Scenarios.FirstOrDefault(scenario => scenario.Name == scenarioName) is not { } scenario)
        {
            return null;
        }

        return new SourceInformation { FileName = source, LineNumber = scenario.Source.LineNumber };
    }

    // The build output's folder sits under the project's (bin/<configuration>/<tfm>); a file the
    // project links from elsewhere has no copy there, and keeps the C# method as its source.
    private static string? ProjectCopy(string file)
    {
        var output = Path.GetDirectoryName(BddScenarioSource.GetConfigFilePath())!;
        var relative = Path.GetRelativePath(output, Path.GetFullPath(file));
        for (var folder = new DirectoryInfo(output).Parent; folder is not null; folder = folder.Parent)
        {
            if (folder.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) && folder.Parent is { } project)
            {
                var source = Path.Combine(project.FullName, relative);
                return File.Exists(source) ? source : null;
            }
        }

        return null;
    }
}
