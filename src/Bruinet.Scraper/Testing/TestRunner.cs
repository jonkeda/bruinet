using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Bruinet.Scraper.Testing;

/// <summary>
/// Runs a drafted test once: <c>dotnet test</c> on its project with a filter, built first, as a person
/// would from a terminal. The result is the summary line's counts and, when it did not pass, the end of
/// the output.
/// </summary>
public static partial class TestRunner
{
    /// <summary>How long a run may take before it is ended.</summary>
    public static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(15);

    /// <summary>How many of the output's last lines a result keeps when the run did not pass.</summary>
    public const int TailLines = 60;

    /// <summary>Builds the project and runs the tests the filter selects.</summary>
    /// <param name="projectFile">The test project's <c>.csproj</c>.</param>
    /// <param name="filter">The <c>dotnet test --filter</c>.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task<TestRunResult> RunAsync(string projectFile, string filter, CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "test", projectFile, "--filter", filter, "-v:minimal", "-nologo", "/nr:false" })
            info.ArgumentList.Add(argument);
        var lines = new List<string>();
        using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet did not start.");
        void Add(string? line)
        {
            if (line is null)
                return;
            lock (lines)
                lines.Add(line);
        }
        process.OutputDataReceived += (_, e) => Add(e.Data);
        process.ErrorDataReceived += (_, e) => Add(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(RunLimit);
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        string[] output;
        lock (lines)
            output = [.. lines];
        return Read(projectFile, filter, process.ExitCode, output);
    }

    /// <summary>A run's result from its exit code and output.</summary>
    public static TestRunResult Read(string projectFile, string filter, int exitCode, IReadOnlyList<string> output)
    {
        int passed = 0, failed = 0, skipped = 0, total = 0;
        foreach (var line in output)
        {
            if (Summary().Match(line) is not { Success: true } m)
                continue;
            failed += Int(m.Groups["failed"].Value);
            passed += Int(m.Groups["passed"].Value);
            skipped += Int(m.Groups["skipped"].Value);
            total += Int(m.Groups["total"].Value);
        }
        var ok = exitCode == 0 && failed == 0 && passed > 0;
        var why = ok ? null
            : total == 0 ? "No test ran: nothing matched the filter, or the project did not build."
            : $"{failed} of {total} failed.";
        return new TestRunResult(projectFile, filter, ok, passed, failed, skipped, why, ok ? [] : output.TakeLast(TailLines).ToArray());
    }

    private static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(Passed|Failed)!\s+-\s+Failed:\s+(?<failed>\d+),\s+Passed:\s+(?<passed>\d+),\s+Skipped:\s+(?<skipped>\d+),\s+Total:\s+(?<total>\d+)")]
    private static partial Regex Summary();
}

/// <summary>A drafted test run once.</summary>
/// <param name="Project">The project it ran in.</param>
/// <param name="Filter">The filter that chose it.</param>
/// <param name="Passed">Whether it ran and passed.</param>
/// <param name="PassedCount">How many tests passed.</param>
/// <param name="FailedCount">How many failed.</param>
/// <param name="SkippedCount">How many were skipped.</param>
/// <param name="Problem">Why it did not pass, or null.</param>
/// <param name="Output">The end of the output, when it did not pass.</param>
public sealed record TestRunResult(string Project, string Filter, bool Passed, int PassedCount, int FailedCount, int SkippedCount, string? Problem, IReadOnlyList<string> Output);
