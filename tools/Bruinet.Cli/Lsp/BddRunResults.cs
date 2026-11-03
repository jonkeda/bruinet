using System.Text.Json;
using Bruinet.Core.Artifacts;
using Bruinet.Bdd.Preview;

namespace Bruinet.Bdd.LanguageServer;

/// <summary>
/// The newest run of each scenario of a file, found through the runs' artifact manifests
/// (<c>TestResults/&lt;run-id&gt;/manifest.json</c>, AD-007), not by guessing a run id. A report
/// counts for a file when its suite and its file (relative to the <c>Features</c> folder) match.
/// </summary>
internal sealed class BddRunResults
{
    // Enough runs to find a file's last run without reading years of history on each preview.
    private const int RunsSearched = 40;

    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.General) { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, (DateTime Written, TestArtifactManifest? Manifest)> _manifests = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <c>TestResults</c> folder a suite's runs write: at the nearest folder above its project
    /// with <c>.git</c> or a <c>.sln</c>, the walk the run makes from its output folder
    /// (<c>DefaultTestArtifactPathProvider</c>). Not the open solution's: a solution may list
    /// projects outside its folder. A runner a host starts (the server's, the Presenter's) is told
    /// it, so a run from a copy of the output writes here too.
    /// </summary>
    /// <param name="projectDir">The suite's project folder.</param>
    public static string RootFor(string projectDir) => TestArtifactOptions.RootFor(projectDir);

    /// <summary>The newest run of each scenario of a file in a suite, by scenario name.</summary>
    /// <param name="root">The suite's results folder (<see cref="RootFor"/>).</param>
    /// <param name="suite">The suite's name (its project's).</param>
    /// <param name="filePath">The feature file.</param>
    /// <param name="accept">Only runs this accepts count (AI9: the newest that failed, the newest that passed); every run when null.</param>
    public IReadOnlyDictionary<string, BddScenarioRun> For(string root, string suite, string filePath, Func<BddScenarioRun, bool>? accept = null)
    {
        Dictionary<string, BddScenarioRun> runs = new(StringComparer.Ordinal);
        if (!Directory.Exists(root))
        {
            return runs;
        }

        var key = FeatureKey(filePath, BddScenarioFiles.ScenarioRoot(filePath));
        var manifests = Directory.EnumerateDirectories(root)
            .Select(run => Path.Combine(run, "manifest.json"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(RunsSearched);
        foreach (var manifestPath in manifests)
        {
            if (Read(manifestPath) is not { } manifest)
            {
                continue;
            }

            var runDirectory = Path.GetDirectoryName(manifestPath)!;
            foreach (var record in manifest.Suites.SelectMany(s => s.Artifacts).Reverse())
            {
                if (record.Kind != "uat-scenario" ||
                    runs.ContainsKey(record.Name) ||
                    !string.Equals(record.Metadata.GetValueOrDefault("suite"), suite, StringComparison.OrdinalIgnoreCase) ||
                    record.Metadata.GetValueOrDefault("file") is not { } file ||
                    !string.Equals(FeatureKey(file, null), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var reportPath = Path.IsPathRooted(record.Path) ? record.Path : Path.Combine(runDirectory, record.Path);
                if (File.Exists(reportPath) && BddScenarioRun.Parse(File.ReadAllText(reportPath), record.CreatedAtUtc) is { } run && accept?.Invoke(run) != false)
                {
                    runs[record.Name] = run;
                }
            }
        }

        return runs;
    }

    /// <summary>
    /// Deletes a suite's scenario results in every run under <paramref name="root"/>: of a feature
    /// file, or of some scenarios of it, or all of the suite's. Only scenario results go: each
    /// record's report and the screenshot it names, when they are inside the run. The manifest is
    /// written again without them, and a run left with nothing but its own files is deleted. A run
    /// written to just now may still be going, and is skipped.
    /// </summary>
    /// <param name="root">The suite's results folder (<see cref="RootFor"/>).</param>
    /// <param name="suite">The suite's name (its project's).</param>
    /// <param name="filePath">The feature file; null for all of the suite's results.</param>
    /// <param name="scenarios">The scenarios' names (an outline's rows with their <c>[n]</c>); null for all of the file's.</param>
    /// <param name="dryRun">Counts only; nothing is changed.</param>
    public BddDeleteResult Delete(string root, string suite, string? filePath, IReadOnlyCollection<string>? scenarios, bool dryRun)
    {
        var result = new BddDeleteResult();
        if (!Directory.Exists(root))
        {
            return result;
        }

        var key = filePath is null ? null : FeatureKey(filePath, BddScenarioFiles.ScenarioRoot(filePath));
        foreach (var run in Directory.EnumerateDirectories(root).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (TestArtifactManifestWriter.Read(run) is not { } manifest)
            {
                continue;
            }

            var matches = manifest.Suites
                .SelectMany(entry => entry.Artifacts.Where(Matches).Select(record => (Suite: entry, Record: record)))
                .ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            var written = File.GetLastWriteTimeUtc(Path.Combine(run, "manifest.json"));
            if (DateTime.UtcNow - written < RunStillGoing)
            {
                result.Skipped.Add(new BddSkippedRun(Path.GetFileName(run), "it was written to in the last half minute, so it may still be going"));
                continue;
            }

            var files = matches
                .SelectMany(match => new[] { match.Record.Path, match.Record.Metadata.GetValueOrDefault("evidencePath") })
                .OfType<string>()
                .Select(path => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(run, path)))
                .Where(path => Inside(run, path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.FilesDeleted += files.Count;
            foreach (var (entry, record) in matches)
            {
                entry.Artifacts.Remove(record);
            }

            manifest.Suites.RemoveAll(entry => entry.Artifacts.Count == 0);
            var empty = manifest.Suites.Count == 0 && OnlyRunFilesLeft(run, files);
            if (empty)
            {
                result.RunsDeleted++;
            }
            else
            {
                result.RunsChanged++;
            }

            if (dryRun)
            {
                continue;
            }

            if (empty)
            {
                Directory.Delete(run, recursive: true);
                continue;
            }

            foreach (var file in files)
            {
                File.Delete(file);
            }

            TestArtifactManifestWriter.Save(run, manifest);
            RemoveEmptyFolders(run);
        }

        return result;

        bool Matches(TestArtifactRecord record)
            => record.Kind == "uat-scenario" &&
               string.Equals(record.Metadata.GetValueOrDefault("suite"), suite, StringComparison.OrdinalIgnoreCase) &&
               (key is null || (record.Metadata.GetValueOrDefault("file") is { } file && string.Equals(FeatureKey(file, null), key, StringComparison.OrdinalIgnoreCase))) &&
               (scenarios is null || scenarios.Contains(record.Name, StringComparer.Ordinal));
    }

    // A manifest written this recently may belong to a test process still running.
    private static readonly TimeSpan RunStillGoing = TimeSpan.FromSeconds(30);

    // Never a path outside the run, whatever a record says.
    private static bool Inside(string run, string path)
        => path.StartsWith(Path.GetFullPath(run).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // Only the run's manifest and summary left once these files go: nothing of anyone else's.
    private static bool OnlyRunFilesLeft(string run, IReadOnlyCollection<string> going)
        => Directory.EnumerateFiles(run, "*", SearchOption.AllDirectories)
            .Where(file => !going.Contains(file, StringComparer.OrdinalIgnoreCase))
            .All(file => Path.GetDirectoryName(file)!.Equals(Path.GetFullPath(run).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
                         Path.GetFileName(file) is "manifest.json" or "summary.md");

    private static void RemoveEmptyFolders(string run)
    {
        foreach (var folder in Directory.EnumerateDirectories(run, "*", SearchOption.AllDirectories).OrderByDescending(folder => folder.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
    }

    // A file as a report and a source tree both name it: its path under the Features folder.
    private static string FeatureKey(string path, string? scenarioRoot)
    {
        var normalized = path.Replace('\\', '/');
        if (scenarioRoot is not null)
        {
            return Path.GetRelativePath(scenarioRoot, path).Replace('\\', '/');
        }

        var at = normalized.LastIndexOf(BddFileFormat.FeatureFolder + "/", StringComparison.OrdinalIgnoreCase);
        return at >= 0 ? normalized[(at + BddFileFormat.FeatureFolder.Length + 1)..] : Path.GetFileName(normalized);
    }

    private TestArtifactManifest? Read(string manifestPath)
    {
        var written = File.GetLastWriteTimeUtc(manifestPath);
        if (_manifests.TryGetValue(manifestPath, out var cached) && cached.Written == written)
        {
            return cached.Manifest;
        }

        TestArtifactManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<TestArtifactManifest>(File.ReadAllText(manifestPath), ManifestJson);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            manifest = null;
        }

        _manifests[manifestPath] = (written, manifest);
        return manifest;
    }
}

/// <summary>What deleting results did, or would do in a dry run.</summary>
internal sealed class BddDeleteResult
{
    /// <summary>Runs that kept other results, their manifest written again.</summary>
    public int RunsChanged { get; set; }

    /// <summary>Runs left with nothing, deleted whole.</summary>
    public int RunsDeleted { get; set; }

    /// <summary>Reports and screenshots deleted.</summary>
    public int FilesDeleted { get; set; }

    /// <summary>Runs not touched, and why.</summary>
    public List<BddSkippedRun> Skipped { get; } = [];
}

/// <summary>A run not touched.</summary>
internal sealed record BddSkippedRun(string Run, string Reason);
