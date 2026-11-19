using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;

namespace Bruinet.Scraper.Testing;

/// <summary>One test of a run: its class, its name, whether it passed, and what it said when it did not.</summary>
/// <param name="Class">The test's class, without its namespace.</param>
/// <param name="Name">The test's full name, with its arguments.</param>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Message">The failure's message; null when it passed.</param>
/// <param name="Seconds">How long it took.</param>
public sealed record TestOutcome(string Class, string Name, bool Passed, string? Message, double Seconds);

/// <summary>A run's tests, one by one; and why nothing ran when nothing did.</summary>
/// <param name="Tests">Each test.</param>
/// <param name="Problem">Why the run gave no results: nothing matched, or the project did not build. Null when tests ran.</param>
public sealed record TestOutcomes(IReadOnlyList<TestOutcome> Tests, string? Problem);

public static partial class TestRunner
{
    /// <summary>
    /// Runs the tests a filter selects without building, and gives each test's outcome with its message: read from
    /// the results file the run writes, not from what it prints.
    /// </summary>
    /// <param name="projectFile">The test project's <c>.csproj</c>, built.</param>
    /// <param name="filter">The <c>dotnet test --filter</c>.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task<TestOutcomes> RunEachAsync(string projectFile, string filter, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(Path.GetTempPath(), "bruinet-factory", "results-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { "test", projectFile, "--no-build", "--filter", filter, "-v:quiet", "-nologo", "/nr:false", "--logger", "trx;LogFileName=results.trx", "--results-directory", folder })
                info.ArgumentList.Add(argument);
            var lines = new List<string>();
            using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet did not start.");
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (lines) lines.Add(e.Data); };
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

            var file = Path.Combine(folder, "results.trx");
            if (!File.Exists(file))
            {
                string[] tail;
                lock (lines)
                    tail = [.. lines.TakeLast(8)];
                return new TestOutcomes([], "No test ran: nothing matched the filter, or the run did not start. " + string.Join(' ', tail));
            }

            var tests = ReadTrx(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));
            return new TestOutcomes(tests, tests.Count == 0 ? "No test ran: nothing matched the filter." : null);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>The tests of a results file (<c>.trx</c>).</summary>
    public static IReadOnlyList<TestOutcome> ReadTrx(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var root = XDocument.Parse(xml).Root;
        if (root is null)
            return [];
        var space = root.Name.Namespace;
        // A test's class is in its definition; the result names the definition by the test's id.
        var classes = root.Descendants(space + "UnitTest").ToDictionary(
            test => (string?)test.Attribute("id") ?? "",
            test => (string?)test.Element(space + "TestMethod")?.Attribute("className") ?? "",
            StringComparer.OrdinalIgnoreCase);
        return [.. root.Descendants(space + "UnitTestResult").Select(result =>
        {
            var name = (string?)result.Attribute("testName") ?? "";
            var full = classes.GetValueOrDefault((string?)result.Attribute("testId") ?? "", "");
            var type = full.Length > 0 ? full : name.Contains('(', StringComparison.Ordinal) ? name[..name.IndexOf('(', StringComparison.Ordinal)] : name;
            var passed = string.Equals((string?)result.Attribute("outcome"), "Passed", StringComparison.OrdinalIgnoreCase);
            var message = (string?)result.Element(space + "Output")?.Element(space + "ErrorInfo")?.Element(space + "Message");
            var seconds = TimeSpan.TryParse((string?)result.Attribute("duration"), CultureInfo.InvariantCulture, out var took) ? took.TotalSeconds : 0;
            return new TestOutcome(type[(type.LastIndexOf('.') + 1)..], name, passed, passed ? null : message ?? "failed", Math.Round(seconds, 1));
        })];
    }
}
