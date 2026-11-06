using System.Diagnostics;
using Bruinet.Bdd.Runner;
using Xunit;

namespace Bruinet.Uat.Tests;

/// <summary>
/// Warm runners (AD-025): this test project's output, copied to a folder of its own
/// as if it were a suite's build output, with a runner kept ready from a copy of that.
/// </summary>
public sealed class BddWarmRunnersTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "BruinetWarmRunnerTests", Guid.NewGuid().ToString("N"));
    private string _output = null!;
    private string _feature = null!;
    private string _config = null!;
    private BddWarmRunners _runners = null!;

    private string Pages => Path.Combine(_output, Path.GetFileName(typeof(RunnerProcessFixture).Assembly.Location));

    public Task InitializeAsync()
    {
        _output = Path.Combine(_root, "output");
        var built = Path.GetDirectoryName(typeof(RunnerProcessFixture).Assembly.Location)!;
        foreach (var file in Directory.EnumerateFiles(built, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_output, Path.GetRelativePath(built, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        _config = Path.Combine(_root, "uat.config.md");
        _feature = Path.Combine(_root, "warm.feature.md");
        File.WriteAllText(_config, $"""
            # UAT Config

            ## Runtime

            | Field | Value |
            | --- | --- |
            | Target | MAUI |
            | Fixture | {nameof(RunnerProcessFixture)} |

            ## Assemblies

            | Kind | Assembly |
            | --- | --- |
            | Pages | {Path.GetFileName(Pages)} |
            """);
        File.WriteAllText(_feature, """
            # Feature: Warm

            ## Scenario: Counts to two

            ```gherkin
            Given the runner counts
            When the runner counts
            Then the runner has counted 2 times
            ```

            ## Scenario: Knows where it runs

            ```gherkin
            Then the runner's base directory is the suite's output
            ```
            """);
        _runners = new BddWarmRunners(Path.Combine(_root, "copies"));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _runners.DisposeAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A runner not ended yet; the temp folder is cleaned by the OS.
        }
    }

    [Fact(Timeout = 120000)]
    public async Task AWarmRunner_IsTakenAtOnce_FromACopy_AndABuildOfTheOutputGoesThroughMeanwhile()
    {
        var start = new BddRunnerStart(Pages);
        await _runners.WarmAsync(start);

        // As a build writes the output while the runner waits: it would fail if the runner held the file.
        File.Copy(typeof(RunnerProcessFixture).Assembly.Location, Pages, overwrite: true);
        var clock = Stopwatch.StartNew();
        var runner = await _runners.TakeAsync(new BddRunnerStart(Pages));
        var took = clock.Elapsed;

        // The output did not change (the same bytes and time), so the waiting runner is the one taken.
        Assert.True(took < TimeSpan.FromSeconds(1), $"Taking the warm runner took {took}.");
        var opened = await runner.RequestAsync(new BddRunnerOpen(_config, Pages, _feature, "Knows where it runs"));
        Assert.True(opened is BddRunnerOpened, (opened as BddRunnerFailed)?.Message);
        var step = Assert.IsType<BddRunnerStep>(await runner.RequestAsync(new BddRunnerNext()));
        Assert.True(step.Status == BddStepResultStatus.Passed, step.Message);
        Assert.IsType<BddRunnerDone>(await runner.RequestAsync(new BddRunnerClose()));
        _runners.Return(runner);
        Assert.True(_runners.IsWarm(start));
    }

    [Fact(Timeout = 120000)]
    public async Task AChangedOutput_ReplacesTheWaitingRunner_WithOneFromANewCopy()
    {
        var start = new BddRunnerStart(Pages);
        await _runners.WarmAsync(start);
        var first = await _runners.TakeAsync(start);
        var firstProcess = first.Process.Id;
        _runners.Return(first);

        File.SetLastWriteTimeUtc(Pages, DateTime.UtcNow);
        var second = await _runners.TakeAsync(start);

        Assert.NotEqual(firstProcess, second.Process.Id);
        Assert.IsType<BddRunnerOpened>(await second.RequestAsync(new BddRunnerOpen(_config, Pages, _feature, "Counts to two")));
        Assert.IsType<BddRunnerDone>(await second.RequestAsync(new BddRunnerClose()));
        _runners.Return(second);
        await WaitForExitAsync(firstProcess);
    }

    [Fact]
    public void ACopy_LinksWhatTheEarlierCopyHasUnchanged_AndCleaningKeepsWhatIsInUse()
    {
        var copies = Path.Combine(_root, "plain");
        var first = BddShadowCopy.Make(_output, BddShadowCopy.NewFolder(_output, copies));
        File.SetLastWriteTimeUtc(Pages, DateTime.UtcNow);

        var second = BddShadowCopy.Make(_output, BddShadowCopy.NewFolder(_output, copies));

        Assert.Equal(Directory.EnumerateFiles(_output, "*", SearchOption.AllDirectories).Count(), Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories).Count());
        Assert.Equal(File.GetLastWriteTimeUtc(Pages), File.GetLastWriteTimeUtc(Path.Combine(second, Path.GetFileName(Pages))));
        BddShadowCopy.Clean(_output, [second], copies);
        Assert.False(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }

    [Fact]
    public void Cleaning_RemovesEndedProcessesCopies_OfEveryOutput_AndKeepsARunningOnes()
    {
        var copies = Path.Combine(_root, "others");
        var ended = Path.Combine(copies, "GONEOUTPUT", $"{int.MaxValue}-1", "copy");
        var running = Path.Combine(copies, "OTHEROUTPUT", $"{Environment.ProcessId}-9", "copy");
        Directory.CreateDirectory(ended);
        Directory.CreateDirectory(running);

        BddShadowCopy.CleanEnded(copies);

        Assert.False(Directory.Exists(Path.Combine(copies, "GONEOUTPUT")));
        Assert.True(Directory.Exists(running));
    }

    private static async Task WaitForExitAsync(int process)
    {
        try
        {
            using var ended = Process.GetProcessById(process);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ended.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException)
        {
            // Ended already.
        }
    }
}
